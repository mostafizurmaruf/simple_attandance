using System.IO;
using Microsoft.Win32;

namespace ShareDealAttend.Helpers;

/// <summary>
/// Registers the app for auto-start under the current user's
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run key.
///
/// This is "self-healing": the app re-asserts the desired state on every launch
/// (see <see cref="Reconcile"/>), so even if the machine-wide HKLM entry the
/// installer wrote is cleared — or the per-user HKCU value never got written on a
/// new machine — the kiosk still comes back on the next sign-in. The user's
/// explicit on/off choice from the tray menu is persisted to a small preference
/// file and honored on later launches. All operations are best-effort and never throw.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppPaths.ProductName; // "ShareDeal Attend"

    /// <summary>%LocalAppData%\ShareDeal Attend\startup.pref — "1"=on, "0"=off.</summary>
    private static string PrefFile => Path.Combine(AppPaths.RootDir, "startup.pref");

    /// <summary>
    /// Ensures the HKCU Run value points at the current EXE. Rewrites it if the
    /// path has changed (e.g. after an upgrade to a new install location).
    /// </summary>
    public static void EnsureRegistered()
    {
        try
        {
            var desired = $"\"{AppPaths.ExePath}\"";

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                Logger.Warn("Could not open/create HKCU Run key for startup registration.");
                return;
            }

            var current = key.GetValue(ValueName) as string;
            if (!string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, desired, RegistryValueKind.String);
                Logger.Info($"Startup registered (HKCU): {desired}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to register startup entry.", ex);
        }
    }

    /// <summary>
    /// Re-asserts the desired auto-start state on every launch (self-healing).
    /// The desired state is the user's persisted preference if they have ever
    /// toggled it; otherwise it falls back to <paramref name="configDefault"/>
    /// (appsettings.json -> RegisterStartup). When enabled, the HKCU Run value is
    /// (re)written even if it was missing or cleared — which is what makes the
    /// kiosk reliably come back on a fresh machine. Best-effort; never throws.
    /// </summary>
    public static void Reconcile(bool configDefault)
    {
        bool enabled = ReadPreference() ?? configDefault;

        if (enabled)
            EnsureRegistered();
        else
            Unregister();

        // Persist the resolved state so the first-run default becomes sticky and
        // later launches don't depend on appsettings.json staying unchanged.
        SavePreference(enabled);
    }

    /// <summary>
    /// Applies an explicit on/off choice from the UI and remembers it, so the
    /// decision survives restarts and overrides the appsettings.json default.
    /// </summary>
    public static void SetEnabled(bool enable)
    {
        if (enable)
            EnsureRegistered();
        else
            Unregister();

        SavePreference(enable);
    }

    /// <summary>Reads the saved on/off preference, or null if never set.</summary>
    private static bool? ReadPreference()
    {
        try
        {
            if (!File.Exists(PrefFile))
                return null;

            return File.ReadAllText(PrefFile).Trim() == "1";
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to read startup preference.", ex);
            return null;
        }
    }

    /// <summary>Persists the on/off preference. Best-effort.</summary>
    private static void SavePreference(bool enabled)
    {
        try
        {
            File.WriteAllText(PrefFile, enabled ? "1" : "0");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save startup preference.", ex);
        }
    }

    /// <summary>
    /// Returns true when the HKCU Run value exists and points at this EXE,
    /// i.e. the app is currently set to start with Windows for this user.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string current
                   && !string.IsNullOrWhiteSpace(current);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to read startup entry.", ex);
            return false;
        }
    }

    /// <summary>Removes the HKCU Run value. Best-effort.</summary>
    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Logger.Info("Startup entry removed (HKCU).");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to remove startup entry.", ex);
        }
    }
}
