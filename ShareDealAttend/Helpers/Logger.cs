using System.IO;
using System.Text;

namespace ShareDealAttend.Helpers;

/// <summary>
/// Tiny append-only file logger. One file per day under
/// %LocalAppData%\ShareDeal Attend\logs\app-YYYYMMDD.log.
/// Thread-safe and deliberately never throws — logging must not crash the kiosk.
/// </summary>
internal static class Logger
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        if (ex is null)
            Write("ERROR", message);
        else
            Write("ERROR", $"{message}{Environment.NewLine}{ex}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureCreated();
            var file = Path.Combine(AppPaths.LogsDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

            lock (Gate)
            {
                File.AppendAllText(file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Intentionally ignored: a failed log write must never take down the app.
        }
    }
}
