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

    /// <summary>Borderless, maximized window when true.</summary>
    public bool Fullscreen { get; set; } = true;

    /// <summary>Self-register under HKCU\...\Run on every launch when true.</summary>
    public bool RegisterStartup { get; set; } = true;

    /// <summary>Block user-initiated close (Alt+F4, X) when true.</summary>
    public bool PreventAccidentalClose { get; set; } = true;

    /// <summary>
    /// Optional host allow-list. When non-empty, navigations and new-window
    /// requests to hosts outside this list are blocked. Empty = allow all.
    /// </summary>
    public List<string> AllowedHosts { get; set; } = new();

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
