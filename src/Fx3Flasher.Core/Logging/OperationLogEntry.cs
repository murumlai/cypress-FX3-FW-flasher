using System;

namespace Fx3Flasher.Core.Logging
{
    public enum LogSeverity
    {
        Debug = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }

    /// <summary>A single timestamped entry in the operation history.</summary>
    public sealed class OperationLogEntry
    {
        public DateTime TimestampUtc { get; set; }
        public LogSeverity Severity { get; set; }
        public string Message { get; set; }

        /// <summary>Optional device index the entry relates to, or -1.</summary>
        public int DeviceIndex { get; set; } = -1;

        public override string ToString()
        {
            string device = DeviceIndex >= 0 ? string.Format("[dev {0}] ", DeviceIndex) : string.Empty;
            return string.Format("{0:yyyy-MM-dd HH:mm:ss} {1,-7} {2}{3}",
                TimestampUtc.ToLocalTime(), Severity.ToString().ToUpperInvariant(), device, Message);
        }
    }
}
