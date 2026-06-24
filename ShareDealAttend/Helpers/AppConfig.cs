using System.IO;
using System.Text.Json;

namespace ShareDealAttend.Helpers;

/// <summary>
/// Strongly-typed view of appsettings.json. Always returns a usable object:
/// missing file, missing keys or malformed JSON all fall back to safe defaults
/// so the kiosk still launches and shows the attendance page.
/// </summary>
public sealed class AppConfig
{
    public string StartUrl { get; set; } = "https://sharedealnow.com/attend/";

    /// <summary>Borderless, maximized window when true. Default: a normal,
    /// small, resizable window the user can maximize manually.</summary>
    public bool Fullscreen { get; set; } = false;

    /// <summary>Self-register under HKCU\...\Run on every launch when true.</summary>
    public bool RegisterStartup { get; set; } = true;

    /// <summary>Block user-initiated close (Alt+F4, X) when true. Default off
    /// so the window can be closed normally.</summary>
    public bool PreventAccidentalClose { get; set; } = false;

    /// <summary>
    /// Optional host allow-list. When non-empty, navigations and new-window
    /// requests to hosts outside this list are blocked. Empty = allow all.
    /// </summary>
    public List<string> AllowedHosts { get; set; } = new();

    /// <summary>
    /// Daily times ("HH:mm", 24-hour) at which the kiosk window is force-shown:
    /// restored if minimized/hidden, brought to the foreground, and reloaded.
    /// Lets the page reappear at shift times even if someone minimised it.
    /// Empty list = feature off. Default: 06:00 and 18:00.
    /// </summary>
    public List<string> ReopenTimes { get; set; } = new() { "06:00", "18:00" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads appsettings.json from beside the EXE. Any problem logs a warning
    /// and returns defaults so the app keeps working.
    /// </summary>
    public static AppConfig Load()
    {
        var path = Path.Combine(AppPaths.ExeDir, "appsettings.json");

        try
        {
            if (!File.Exists(path))
            {
                Logger.Warn($"appsettings.json not found at '{path}'. Using defaults.");
                return new AppConfig();
            }

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (cfg is null)
            {
                Logger.Warn("appsettings.json deserialized to null. Using defaults.");
                return new AppConfig();
            }

            if (string.IsNullOrWhiteSpace(cfg.StartUrl))
                cfg.StartUrl = new AppConfig().StartUrl;

            cfg.AllowedHosts ??= new();
            cfg.ReopenTimes ??= new();
            Logger.Info($"Config loaded. StartUrl='{cfg.StartUrl}', Fullscreen={cfg.Fullscreen}.");
            return cfg;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to read appsettings.json. Using defaults.", ex);
            return new AppConfig();
        }
    }
}
