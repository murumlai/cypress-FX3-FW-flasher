using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Fx3Flasher.App.Mvvm;
using Fx3Flasher.App.Services;
using Fx3Flasher.Core.Devices;
using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Logging;
using Fx3Flasher.Core.Models;
using Fx3Flasher.Core.Profiles;
using Fx3Flasher.Core.Safety;
using Fx3Flasher.Core.Services;

namespace Fx3Flasher.App.ViewModels
{
    /// <summary>Primary view model driving device discovery, programming, erasing and logging.</summary>
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IUiInteraction _interaction;
        private readonly FlasherService _service;
        private readonly BoardProfileStore _profiles;

        private Fx3DeviceInfo _selectedDevice;
        private string _imagePath;
        private string _eraseImagePath;
        private bool _isBusy;
        private int _progressPercent;
        private string _progressStage = string.Empty;
        private string _statusText = "Ready.";
        private CancellationTokenSource _cts;

        public MainViewModel(
            IFx3DeviceBackend backend,
            BoardProfileStore profiles,
            IUiInteraction interaction,
            Dispatcher dispatcher)
        {
            _profiles = profiles ?? throw new ArgumentNullException("profiles");
            _interaction = interaction ?? throw new ArgumentNullException("interaction");

            LogEntries = new ObservableCollection<OperationLogEntry>();
            var logger = new UiLogger(LogEntries, dispatcher);
            _service = new FlasherService(
                backend, profiles, new EepromSafetyGate(), new FirmwareImageValidator(), logger);

            Devices = new ObservableCollection<Fx3DeviceInfo>();

            RefreshCommand = new RelayCommand(RefreshDevices, () => !IsBusy);
            BrowseImageCommand = new RelayCommand(BrowseImage, () => !IsBusy);
            BrowseEraseImageCommand = new RelayCommand(BrowseEraseImage, () => !IsBusy);
            ProgramCommand = new RelayCommand(ProgramAsync, CanProgram);
            EraseCommand = new RelayCommand(EraseAsync, CanErase);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy);
            ClearLogCommand = new RelayCommand(() => LogEntries.Clear(), () => !IsBusy);
            ExportLogCommand = new RelayCommand(ExportLog, () => LogEntries.Count > 0);

            RefreshDevices();
        }

        public ObservableCollection<Fx3DeviceInfo> Devices { get; }
        public ObservableCollection<OperationLogEntry> LogEntries { get; }

        public ICommand RefreshCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand BrowseEraseImageCommand { get; }
        public ICommand ProgramCommand { get; }
        public ICommand EraseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ExportLogCommand { get; }

        public Fx3DeviceInfo SelectedDevice
        {
            get { return _selectedDevice; }
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(SelectedDeviceDetails));
                    OnPropertyChanged(nameof(HasSelectedDevice));
                }
            }
        }

        public bool HasSelectedDevice
        {
            get { return _selectedDevice != null; }
        }

        public string ImagePath
        {
            get { return _imagePath; }
            set { SetProperty(ref _imagePath, value); }
        }

        public string EraseImagePath
        {
            get { return _eraseImagePath; }
            set { SetProperty(ref _eraseImagePath, value); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                }
            }
        }

        public bool IsIdle
        {
            get { return !_isBusy; }
        }

        public int ProgressPercent
        {
            get { return _progressPercent; }
            private set { SetProperty(ref _progressPercent, value); }
        }

        public string ProgressStage
        {
            get { return _progressStage; }
            private set { SetProperty(ref _progressStage, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public string SelectedDeviceDetails
        {
            get
            {
                Fx3DeviceInfo d = _selectedDevice;
                if (d == null)
                {
                    return "No device selected.";
                }

                BoardProfile profile = _service.ResolveProfile(d);
                var sb = new StringBuilder();
                sb.AppendLine("Index:    " + d.Index);
                sb.AppendLine("Name:     " + d.FriendlyName);
                sb.AppendLine("USB ID:   " + d.UsbIdText);
                sb.AppendLine("Serial:   " + (string.IsNullOrEmpty(d.SerialNumber) ? "(none)" : d.SerialNumber));
                sb.AppendLine("State:    " + d.State);
                sb.AppendLine("Profile:  " + (d.ProfileName ?? "(unsupported)"));
                sb.AppendLine("Path:     " + d.DevicePath);
                if (profile != null)
                {
                    sb.AppendLine(string.Format("EEPROM:   {0} bytes, page {1}, I2C 0x{2:X2}",
                        profile.EepromSizeBytes, profile.PageSizeBytes, profile.I2cAddress));
                    sb.AppendLine(string.Format("Ops:      program={0} erase={1} fullErase={2}",
                        profile.AllowProgram, profile.AllowErase, profile.AllowFullErase));
                }

                return sb.ToString().TrimEnd();
            }
        }

        private bool CanProgram()
        {
            return !IsBusy
                && _selectedDevice != null
                && _selectedDevice.IsSupported
                && !string.IsNullOrEmpty(_imagePath);
        }

        private bool CanErase()
        {
            if (IsBusy || _selectedDevice == null || !_selectedDevice.IsSupported)
            {
                return false;
            }

            BoardProfile profile = _service.ResolveProfile(_selectedDevice);
            bool hasEraseImage = !string.IsNullOrEmpty(_eraseImagePath)
                || (profile != null && !string.IsNullOrEmpty(profile.EraseImagePath));
            return hasEraseImage && profile != null && profile.AllowErase;
        }

        private async void RefreshDevices()
        {
            try
            {
                var list = await Task.Run(() => _service.Refresh());
                int previousIndex = _selectedDevice != null ? _selectedDevice.Index : -1;

                Devices.Clear();
                foreach (Fx3DeviceInfo d in list)
                {
                    Devices.Add(d);
                }

                SelectedDevice = null;
                foreach (Fx3DeviceInfo d in Devices)
                {
                    if (d.Index == previousIndex)
                    {
                        SelectedDevice = d;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _interaction.ShowError("Device refresh failed:\n" + ex.Message);
            }
        }

        private void BrowseImage()
        {
            string path = _interaction.OpenImageFile("Select firmware image (.img)");
            if (!string.IsNullOrEmpty(path))
            {
                ImagePath = path;
            }
        }

        private void BrowseEraseImage()
        {
            string path = _interaction.OpenImageFile("Select erase image (.img)");
            if (!string.IsNullOrEmpty(path))
            {
                EraseImagePath = path;
            }
        }

        private async void ProgramAsync()
        {
            Fx3DeviceInfo device = _selectedDevice;
            if (device == null)
            {
                return;
            }

            string message = string.Format(
                "PROGRAM EEPROM on:\n[{0}] {1}  {2}\nProfile: {3}\n\nImage:\n{4}\n\nProceed?",
                device.Index, device.FriendlyName, device.UsbIdText, device.ProfileName, _imagePath);
            if (!_interaction.Confirm("Confirm Program", message))
            {
                return;
            }

            await RunOperation(progress => _service.Program(device, _imagePath, true, progress, _cts.Token));
        }

        private async void EraseAsync()
        {
            Fx3DeviceInfo device = _selectedDevice;
            if (device == null)
            {
                return;
            }

            BoardProfile profile = _service.ResolveProfile(device);
            string erasePath = !string.IsNullOrEmpty(_eraseImagePath)
                ? _eraseImagePath
                : (profile != null ? profile.EraseImagePath : null);

            string message = string.Format(
                "ERASE to blank bootloader on:\n[{0}] {1}  {2}\nProfile: {3}\n\nErase image:\n{4}\n\nThis is destructive. Proceed?",
                device.Index, device.FriendlyName, device.UsbIdText, device.ProfileName, erasePath);
            if (!_interaction.Confirm("Confirm Erase", message))
            {
                return;
            }

            await RunOperation(progress => _service.Erase(device, erasePath, true, progress, _cts.Token));
        }

        private async Task RunOperation(Func<IProgress<OperationProgress>, FlasherOperationResult> operation)
        {
            IsBusy = true;
            ProgressPercent = 0;
            ProgressStage = "Starting";
            _cts = new CancellationTokenSource();

            var progress = new Progress<OperationProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressStage = p.Stage;
            });

            try
            {
                FlasherOperationResult result = await Task.Run(() => operation(progress));
                if (result.Success)
                {
                    StatusText = result.Verified ? "Completed and verified." : "Completed (verification inconclusive).";
                }
                else
                {
                    StatusText = "Failed: " + result.Message;
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = "Error.";
                _interaction.ShowError(ex.ToString());
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                IsBusy = false;
                ProgressStage = string.Empty;
                RefreshDevices();
            }
        }

        private void Cancel()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
        }

        private void ExportLog()
        {
            string suggested = "fx3-flasher-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
            string path = _interaction.SaveTextFile(suggested);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                foreach (OperationLogEntry entry in LogEntries)
                {
                    sb.AppendLine(entry.ToString());
                }

                File.WriteAllText(path, sb.ToString());
                StatusText = "Log exported: " + Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                _interaction.ShowError("Failed to export log:\n" + ex.Message);
            }
        }
    }
}
