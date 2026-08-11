using System.Windows;
using Microsoft.Win32;

namespace Fx3Flasher.App.Services
{
    /// <summary>WPF implementation of user dialogs.</summary>
    public sealed class WpfInteraction : IUiInteraction
    {
        public bool Confirm(string title, string message)
        {
            MessageBoxResult result = MessageBox.Show(
                message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            return result == MessageBoxResult.Yes;
        }

        public string OpenImageFile(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "FX3 firmware image (*.img)|*.img|All files (*.*)|*.*",
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string SaveTextFile(string suggestedName)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export log",
                FileName = suggestedName,
                Filter = "Log file (*.log)|*.log|Text file (*.txt)|*.txt|All files (*.*)|*.*"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
