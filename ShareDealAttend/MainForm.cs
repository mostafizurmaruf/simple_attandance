using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ShareDealAttend.Helpers;

namespace ShareDealAttend;

/// <summary>
/// Fullscreen, locked-down WebView2 host that shows the attendance page and
/// nothing else: no chrome, no context menu, no dev tools, no escape hatches
/// except the hidden admin exit (Ctrl+Shift+Alt+Q).
/// </summary>
public partial class MainForm : Form
{
    private readonly AppConfig _config;

    // True only while we deliberately let the form close (hidden exit / fatal init).
    private bool _allowExit;
    // Avoid recursively re-showing the offline page when it finishes loading.
    private bool _showingOffline;
    private bool _coreReady;

    // Tray icon hosts the "Start with Windows" on/off toggle and Exit.
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _startupMenuItem;

    // Visible on-screen mirror of the same "Start with Windows" toggle.
    private CheckBox? _startupCheckBox;

    // Daily "force-show the window" scheduler.
    private System.Windows.Forms.Timer? _reopenTimer;
    private readonly List<TimeSpan> _reopenTimes = new();
    // Stamp ("yyyyMMddHHmm") of the last scheduled reopen we fired, so a single
    // scheduled minute triggers exactly once even though the timer ticks often.
    private string _lastReopenStamp = string.Empty;

    public MainForm(AppConfig config)
    {
        _config = config;
        InitializeComponent();

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        KeyDown += MainForm_KeyDown;
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
    }

    private void ApplyWindowMode()
    {
        if (_config.Fullscreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
        }
        else
        {
            // Development / testing mode: a normal, closable window.
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            TopMost = false;
            ShowInTaskbar = true;
        }
    }

    // ---- On-screen startup toggle ------------------------------------------

    private void SetupStartupCheckBox()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = System.Drawing.Color.FromArgb(0xF1, 0xF5, 0xF9),
            Padding = new Padding(8, 4, 8, 4),
        };

        _startupCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Checked = StartupManager.IsEnabled(),
            Dock = DockStyle.Left,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };
        _startupCheckBox.CheckedChanged += StartupCheckBox_CheckedChanged;

        bar.Controls.Add(_startupCheckBox);
        Controls.Add(bar);

        // Keep the WebView at the front of the docking order so it fills the
        // area left over below the top bar instead of overlapping it.
        _webView.BringToFront();
    }

    private void StartupCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        bool enable = _startupCheckBox!.Checked;
        StartupManager.SetEnabled(enable);

        // Mirror the change into the tray menu (no event loop: setting Checked
        // on a menu item does not raise Click).
        if (_startupMenuItem is not null)
            _startupMenuItem.Checked = enable;

        Logger.Info($"Startup {(enable ? "enabled" : "disabled")} from main window checkbox.");
    }

    // ---- Tray icon: startup toggle + exit ----------------------------------

    private void SetupTrayIcon()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open", null, (_, _) => RestoreWindow());

        _startupMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled(),
        };
        _startupMenuItem.Click += StartupMenuItem_Click;

        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });

        menu.Items.Add(openItem);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = Icon ?? System.Drawing.SystemIcons.Application,
            Text = "ShareDeal Attend",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();
    }

    private void StartupMenuItem_Click(object? sender, EventArgs e)
    {
        // CheckOnClick has already flipped the state by the time this fires.
        bool enable = _startupMenuItem!.Checked;
        StartupManager.SetEnabled(enable);

        // Keep the on-screen checkbox in sync (setting Checked to the same value
        // is a no-op, so this won't re-trigger the registration twice).
        if (_startupCheckBox is not null)
            _startupCheckBox.Checked = enable;

        // Keep the on-screen checkbox in sync (setting Checked to the same value
        // is a no-op, so this won't re-trigger the registration twice).
        if (_startupCheckBox is not null)
            _startupCheckBox.Checked = enable;

        Logger.Info($"Startup {(enable ? "enabled" : "disabled")} from tray menu.");
    }

    private void RestoreWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }

    // ---- Daily reopen scheduler --------------------------------------------

    private void SetupReopenSchedule()
    {
        _reopenTimes.Clear();
        foreach (var raw in _config.ReopenTimes)
        {
            // Accept "HH:mm" (or "H:mm") 24-hour times; skip anything malformed.
            if (TimeSpan.TryParseExact(raw?.Trim(), new[] { @"hh\:mm", @"h\:mm" },
                    System.Globalization.CultureInfo.InvariantCulture, out var t))
                _reopenTimes.Add(t);
            else if (!string.IsNullOrWhiteSpace(raw))
                Logger.Warn($"Ignoring invalid ReopenTimes entry: '{raw}'.");
        }

        if (_reopenTimes.Count == 0)
        {
            Logger.Info("No valid ReopenTimes configured; daily reopen disabled.");
            return;
        }

        Logger.Info($"Daily reopen scheduled at: {string.Join(", ", _reopenTimes)}.");

        // Tick every 20s so we reliably catch the target minute without busy work.
        _reopenTimer = new System.Windows.Forms.Timer { Interval = 20_000 };
        _reopenTimer.Tick += ReopenTimer_Tick;
        _reopenTimer.Start();
    }

    private void ReopenTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var nowHm = new TimeSpan(now.Hour, now.Minute, 0);
        if (!_reopenTimes.Contains(nowHm))
            return;

        // Fire at most once per scheduled minute.
        var stamp = now.ToString("yyyyMMddHHmm");
        if (stamp == _lastReopenStamp)
            return;
        _lastReopenStamp = stamp;

        Logger.Info($"Scheduled reopen triggered at {now:HH:mm}.");
        ForceShowWindow();
    }

    /// <summary>
    /// Force the kiosk back into view: un-minimise, re-apply the configured
    /// window mode, pull it to the foreground, and reload the attendance page.
    /// </summary>
    private void ForceShowWindow()
    {
        Show();
        ApplyWindowMode();

        // Briefly assert TopMost to win the foreground even if another app had it.
        bool wasTopMost = TopMost;
        TopMost = true;
        Activate();
        BringToFront();
        TopMost = wasTopMost;

        if (_coreReady && !_showingOffline)
        {
            Logger.Info($"Reloading start URL on scheduled reopen: {_config.StartUrl}");
            _webView.CoreWebView2.Navigate(_config.StartUrl);
        }
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        ApplyWindowMode();
        SetupTrayIcon();
        SetupStartupCheckBox();
        SetupReopenSchedule();

        try
        {
            // Keep all browser state (cookies/login/cache) in a per-user folder
            // so the app needs no write access to Program Files.
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppPaths.WebView2Dir);

            await _webView.EnsureCoreWebView2Async(env);
            _coreReady = true;

            ConfigureWebViewLockdown();
            WireWebViewEvents();

            Logger.Info($"Navigating to start URL: {_config.StartUrl}");
            _webView.CoreWebView2.Navigate(_config.StartUrl);
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 initialization failed.", ex);
            ShowInitFailure(ex);
        }
    }

    private void ConfigureWebViewLockdown()
    {
        var s = _webView.CoreWebView2.Settings;
        s.AreDefaultContextMenusEnabled = false;     // no right-click menu
        s.AreDevToolsEnabled = false;                // no F12 dev tools
        s.AreBrowserAcceleratorKeysEnabled = false;  // no Ctrl+P, Ctrl+F, F5, etc.
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;              // no Ctrl+scroll zoom
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsSwipeNavigationEnabled = false;          // no touch back/forward
    }

    private void WireWebViewEvents()
    {
        var core = _webView.CoreWebView2;
        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.WebMessageReceived += Core_WebMessageReceived;
        core.ProcessFailed += Core_ProcessFailed;
    }

    // ---- Host allow-list ---------------------------------------------------

    private bool IsHostAllowed(string url)
    {
        if (_config.AllowedHosts.Count == 0)
            return true; // empty list = allow everything

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        foreach (var allowed in _config.AllowedHosts)
        {
            if (string.IsNullOrWhiteSpace(allowed)) continue;
            var host = uri.Host;
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ---- WebView2 events ---------------------------------------------------

    private void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Always allow our in-memory offline page (about:blank / data).
        if (_showingOffline) return;

        if (!e.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return; // allow internal schemes (e.g. the rendered offline page)

        if (!IsHostAllowed(e.Uri))
        {
            Logger.Warn($"Blocked navigation to disallowed host: {e.Uri}");
            e.Cancel = true;
        }
    }

    private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_showingOffline)
        {
            // The offline page itself just finished rendering — don't recurse.
            _showingOffline = false;
            return;
        }

        if (!e.IsSuccess)
        {
            Logger.Warn($"Navigation failed: {e.WebErrorStatus}");
            ShowOfflinePage();
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Never open an external browser / popup — keep everything in this view.
        e.Handled = true;

        if (IsHostAllowed(e.Uri))
        {
            Logger.Info($"Redirecting new-window request into main view: {e.Uri}");
            _webView.CoreWebView2.Navigate(e.Uri);
        }
        else
        {
            Logger.Warn($"Blocked new-window request to disallowed host: {e.Uri}");
        }
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try { message = e.TryGetWebMessageAsString(); }
        catch { return; }

        if (string.Equals(message, "retry", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("User requested retry from offline page.");
            _webView.CoreWebView2.Navigate(_config.StartUrl);
        }
    }

    private void Core_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Logger.Error($"WebView2 process failed: {e.ProcessFailedKind}");
        if (_coreReady)
            ShowOfflinePage();
    }

    // ---- Offline / failure UI ---------------------------------------------

    private void ShowOfflinePage()
    {
        if (!_coreReady) return;
        _showingOffline = true;
        _webView.CoreWebView2.NavigateToString(BuildOfflineHtml());
    }

    private static string BuildOfflineHtml()
    {
        // A self-contained page. "Try Again" posts a message the host handles.
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Connection problem</title>
<style>
  html,body{height:100%;margin:0}
  body{display:flex;align-items:center;justify-content:center;
       font-family:Segoe UI,Arial,sans-serif;background:#0f172a;color:#e2e8f0}
  .card{text-align:center;max-width:520px;padding:40px}
  .icon{font-size:64px;margin-bottom:16px}
  h1{font-size:26px;margin:0 0 12px}
  p{font-size:16px;line-height:1.5;color:#94a3b8;margin:0 0 28px}
  button{font-size:16px;padding:12px 28px;border:0;border-radius:8px;
         background:#2563eb;color:#fff;cursor:pointer}
  button:hover{background:#1d4ed8}
</style>
</head>
<body>
  <div class="card">
    <div class="icon">&#128268;</div>
    <h1>Can't reach the attendance page</h1>
    <p>Please check the internet connection. The page will load again
       automatically once you're back online.</p>
    <button onclick="retry()">Try Again</button>
  </div>
<script>
  function retry(){
    try { window.chrome.webview.postMessage('retry'); } catch(e){}
  }
  // Auto-retry every 15 seconds in case the network comes back unattended.
  setInterval(retry, 15000);
</script>
</body>
</html>
""");
        return sb.ToString();
    }

    private void ShowInitFailure(Exception ex)
    {
        Logger.Error("Showing WebView2 init-failure message.", ex);
        MessageBox.Show(
            "The attendance app could not start its browser component.\r\n\r\n" +
            "Make sure the Microsoft Edge WebView2 Runtime is installed, then " +
            "restart the app. The error has been logged.",
            "ShareDeal Attend",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

        // Without WebView2 there's nothing to show — let the form close.
        _allowExit = true;
        Close();
    }

    // ---- Keyboard lockdown -------------------------------------------------

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // Hidden admin exit: Ctrl + Shift + Alt + Q
        if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.Q)
        {
            Logger.Info("Hidden admin exit triggered.");
            _allowExit = true;
            e.Handled = true;
            Close();
            return;
        }

        // Block the usual escape / inspection / print shortcuts.
        bool block =
            (e.Alt && e.KeyCode == Keys.F4) ||                          // Alt+F4
            e.KeyCode == Keys.F5 ||                                     // refresh
            e.KeyCode == Keys.F12 ||                                    // dev tools
            (e.Control && e.KeyCode == Keys.P) ||                       // print
            (e.Control && e.KeyCode == Keys.F) ||                       // find
            (e.Control && e.KeyCode == Keys.J) ||                       // downloads
            (e.Control && e.KeyCode == Keys.R) ||                       // reload
            (e.Control && e.KeyCode == Keys.N) ||                       // new window
            (e.Control && e.KeyCode == Keys.W) ||                       // close tab
            (e.Control && e.Shift && e.KeyCode == Keys.I) ||           // dev tools
            (e.Control && e.Shift && e.KeyCode == Keys.J) ||           // console
            (e.Control && e.Shift && e.KeyCode == Keys.C) ||           // inspect
            (e.Alt && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)); // back/fwd

        if (block)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    // ---- Prevent accidental close ------------------------------------------

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_config.PreventAccidentalClose || _allowExit)
            return;

        // Block user-initiated closes (X button, Alt+F4, task switch close).
        if (e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            Logger.Info($"Cancelled user-initiated close ({e.CloseReason}).");
            e.Cancel = true;
        }
    }
}
