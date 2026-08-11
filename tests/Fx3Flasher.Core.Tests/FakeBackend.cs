using System;
using System.Collections.Generic;
using System.Threading;
using Fx3Flasher.Core.Devices;
using Fx3Flasher.Core.Logging;
using Fx3Flasher.Core.Models;

namespace Fx3Flasher.Core.Tests
{
    /// <summary>Configurable fake backend for orchestration tests, with call capture.</summary>
    internal sealed class FakeBackend : IFx3DeviceBackend
    {
        public List<Fx3DeviceInfo> DevicesToReturn { get; } = new List<Fx3DeviceInfo>();
        public List<Fx3DeviceInfo> AfterOperationDevices { get; set; }
        public DeviceOperationResult ProgramResult { get; set; } = DeviceOperationResult.Ok("ok");
        public DeviceOperationResult EraseResult { get; set; } = DeviceOperationResult.Ok("ok");
        public int ProgramCalls { get; private set; }
        public int EraseCalls { get; private set; }

        private bool _operationPerformed;

        public IReadOnlyList<Fx3DeviceInfo> Enumerate()
        {
            if (_operationPerformed && AfterOperationDevices != null)
            {
                return AfterOperationDevices;
            }

            return DevicesToReturn;
        }

        public DeviceOperationResult ProgramEeprom(
            Fx3DeviceInfo device, string imageFilePath,
            IProgress<OperationProgress> progress, CancellationToken cancellationToken)
        {
            ProgramCalls++;
            _operationPerformed = true;
            return ProgramResult;
        }

        public DeviceOperationResult EraseEeprom(
            Fx3DeviceInfo device, string eraseImageFilePath,
            IProgress<OperationProgress> progress, CancellationToken cancellationToken)
        {
            EraseCalls++;
            _operationPerformed = true;
            return EraseResult;
        }
    }

    internal sealed class CapturingLogger : IOperationLogger
    {
        public List<OperationLogEntry> Entries { get; } = new List<OperationLogEntry>();

        public void Log(LogSeverity severity, string message, int deviceIndex = -1)
        {
            Entries.Add(new OperationLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Severity = severity,
                Message = message,
                DeviceIndex = deviceIndex
            });
        }
    }
}
