using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Fx3Flasher.App.Services;
using Fx3Flasher.App.ViewModels;
using Fx3Flasher.Core.Profiles;
using Fx3Flasher.Cypress;

namespace Fx3Flasher.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            string configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "config", "supported-boards.json");

            BoardProfileStore profiles;
            try
            {
                profiles = BoardProfileStore.LoadFromFile(configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load board profiles from:\n" + configPath + "\n\n" + ex.Message,
                    "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error);
                profiles = new BoardProfileStore();
            }

            var backend = new CypressFx3Backend(profiles);
            var interaction = new WpfInteraction();
            var viewModel = new MainViewModel(backend, profiles, interaction, Dispatcher);

            var window = new MainWindow { DataContext = viewModel };
            window.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportFatal(e.Exception);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportFatal(e.ExceptionObject as Exception);
        }

        private static void ReportFatal(Exception ex)
        {
            string text = ex != null ? ex.ToString() : "Unknown error.";
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fx3-flasher-crash.log");
                File.AppendAllText(logPath,
                    DateTime.Now.ToString("s") + Environment.NewLine + text + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // Logging must never mask the original failure.
            }

            MessageBox.Show(text, "Fx3 Flasher - unexpected error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
