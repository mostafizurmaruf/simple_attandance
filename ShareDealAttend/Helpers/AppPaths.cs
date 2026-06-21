using System.IO;

namespace ShareDealAttend.Helpers;

/// <summary>
/// Central place for the per-user, writable folders the app uses.
/// Everything lives under %LocalAppData%\ShareDeal Attend so the app never
/// needs write access to its Program Files install directory.
/// </summary>
internal static class AppPaths
{
    /// <summary>Friendly product name, reused for folders and the Run value.</summary>
    public const string ProductName = "ShareDeal Attend";

    /// <summary>%LocalAppData%\ShareDeal Attend</summary>
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductName);

    /// <summary>%LocalAppData%\ShareDeal Attend\logs</summary>
    public static string LogsDir { get; } = Path.Combine(RootDir, "logs");

    /// <summary>
    /// WebView2 user-data folder (cookies, cache, logged-in session).
    /// %LocalAppData%\ShareDeal Attend\WebView2
    /// </summary>
    public static string WebView2Dir { get; } = Path.Combine(RootDir, "WebView2");

    /// <summary>Full path to the EXE that is currently running.</summary>
    public static string ExePath { get; } =
        Environment.ProcessPath ?? Application.ExecutablePath;

    /// <summary>Folder the EXE lives in (where appsettings.json sits in production).</summary>
    public static string ExeDir { get; } =
        Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory;

    /// <summary>
    /// Creates every writable folder up-front. Safe to call repeatedly;
    /// failures are swallowed because logging itself depends on these paths.
    /// </summary>
    public static void EnsureCreated()
    {
        TryCreate(RootDir);
        TryCreate(LogsDir);
        TryCreate(WebView2Dir);
    }

    private static void TryCreate(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            // Nothing useful to do here — the caller must tolerate missing folders.
        }
    }
}
