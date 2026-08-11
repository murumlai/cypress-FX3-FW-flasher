using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Fx3Flasher.Core.Logging;

namespace Fx3Flasher.App.Services
{
    /// <summary>Routes operation log entries to an observable collection on the UI thread.</summary>
    public sealed class UiLogger : IOperationLogger
    {
        private readonly ObservableCollection<OperationLogEntry> _entries;
        private readonly Dispatcher _dispatcher;

        public UiLogger(ObservableCollection<OperationLogEntry> entries, Dispatcher dispatcher)
        {
            _entries = entries ?? throw new ArgumentNullException("entries");
            _dispatcher = dispatcher ?? throw new ArgumentNullException("dispatcher");
        }

        public void Log(LogSeverity severity, string message, int deviceIndex = -1)
        {
            var entry = new OperationLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Severity = severity,
                Message = message,
                DeviceIndex = deviceIndex
            };

            if (_dispatcher.CheckAccess())
            {
                _entries.Add(entry);
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => _entries.Add(entry)));
            }
        }
    }
}
