using Microsoft.Win32;

namespace ShareDealAttend.Helpers;

/// <summary>
/// Registers the app for auto-start under the current user's
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run key.
///
/// This is "self-healing": the app re-asserts the value on every launch, so even
/// if the machine-wide HKLM entry the installer wrote is cleared, the kiosk still
/// comes back on the next sign-in. All operations are best-effort and never throw.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppPaths.ProductName; // "ShareDeal Attend"

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
