using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace FreewriteWindows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            MessageBox.Show(
                $"Freewrite hit an error and needs to close.\n\n{args.Exception.Message}",
                "Freewrite",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogCrash("AppDomain", ex);
            }
        };

        try
        {
            var appDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(appDir))
            {
                Directory.SetCurrentDirectory(appDir);
            }

            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            LogCrash("Startup", ex);
            MessageBox.Show(
                $"Freewrite could not start.\n\n{ex.Message}",
                "Freewrite",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void LogCrash(string source, Exception exception)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Freewrite",
                "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(
                logPath,
                $"[{DateTime.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort logging only.
        }
    }
}
