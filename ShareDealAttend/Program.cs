using System.IO;
using System.Threading;
using ShareDealAttend.Helpers;

namespace ShareDealAttend;

internal static class Program
{
    // Named mutex keeps a single kiosk instance per machine session.
    private const string SingleInstanceMutexName = "Global\\ShareDealAttend_SingleInstance_8F3C2A14";
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main()
    {
        AppPaths.EnsureCreated();

        // ---- Single instance ------------------------------------------------
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNew);
        if (!isNew)
        {
            Logger.Info("Another instance is already running. Exiting.");
            return;
        }

        // ---- Global crash handling -----------------------------------------
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Error("Unhandled AppDomain exception.", ex);
        };

        Application.ThreadException += (_, e) =>
        {
            Logger.Error("Unhandled UI thread exception.", e.Exception);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Logger.Info("==== ShareDeal Attend starting ====");

        try
        {
            ApplicationConfiguration.Initialize();

            var config = AppConfig.Load();

            // Apply the auto-start preference once, on first run. After that the
            // user controls it from the tray menu ("Start with Windows") and we
            // never override their choice on later launches.
            SeedStartupOnFirstRun(config);

            Application.Run(new MainForm(config));
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error during startup.", ex);
            MessageBox.Show(
                "ShareDeal Attend could not start.\r\n\r\n" +
                "The error has been logged. Please contact your administrator.",
                "ShareDeal Attend",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Logger.Info("==== ShareDeal Attend exiting ====");
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
    }

    /// <summary>
    /// On the very first launch, register (or not) for auto-start based on the
    /// config default, then drop a marker so we never touch it again — leaving
    /// the user's tray-menu toggle as the single source of truth afterwards.
    /// </summary>
    private static void SeedStartupOnFirstRun(AppConfig config)
    {
        try
        {
            var marker = Path.Combine(AppPaths.RootDir, ".startup-seeded");
            if (File.Exists(marker))
                return;

            if (config.RegisterStartup)
                StartupManager.EnsureRegistered();

            File.WriteAllText(marker, "seeded");
            Logger.Info($"Startup seeded on first run (RegisterStartup={config.RegisterStartup}).");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to seed startup preference on first run.", ex);
        }
    }
}
