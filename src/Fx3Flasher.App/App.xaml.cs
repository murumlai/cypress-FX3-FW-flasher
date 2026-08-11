using System;
using System.IO;
using System.Windows;
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
    }
}
