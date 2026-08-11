namespace Fx3Flasher.Core.Logging
{
    /// <summary>Sink for structured operation log entries.</summary>
    public interface IOperationLogger
    {
        void Log(LogSeverity severity, string message, int deviceIndex = -1);
    }
}
