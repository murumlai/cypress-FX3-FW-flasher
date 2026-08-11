using System;

namespace Fx3Flasher.Core.Devices
{
    /// <summary>Result of a backend operation (program/erase/verify), decoupled from CyUSB error codes.</summary>
    public sealed class DeviceOperationResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        /// <summary>Raw backend status string (e.g. Cypress error code name), for diagnostics.</summary>
        public string RawStatus { get; private set; }

        private DeviceOperationResult(bool success, string message, string rawStatus)
        {
            Success = success;
            Message = message;
            RawStatus = rawStatus;
        }

        public static DeviceOperationResult Ok(string message, string rawStatus = null)
        {
            return new DeviceOperationResult(true, message, rawStatus);
        }

        public static DeviceOperationResult Fail(string message, string rawStatus = null)
        {
            return new DeviceOperationResult(false, message, rawStatus);
        }
    }

    /// <summary>Progress callback payload for long-running operations.</summary>
    public sealed class OperationProgress
    {
        public int Percent { get; set; }
        public string Stage { get; set; }
    }
}
