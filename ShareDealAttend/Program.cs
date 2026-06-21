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

            // Self-heal the per-user auto-start entry on every launch.
            if (config.RegisterStartup)
                StartupManager.EnsureRegistered();

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
}
