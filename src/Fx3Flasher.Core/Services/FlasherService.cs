using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fx3Flasher.Core.Devices;
using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Logging;
using Fx3Flasher.Core.Models;
using Fx3Flasher.Core.Profiles;
using Fx3Flasher.Core.Safety;

namespace Fx3Flasher.Core.Services
{
    /// <summary>
    /// Orchestrates FX3 EEPROM operations: profile resolution, image validation, the fail-closed
    /// safety gate, backend execution, and re-enumeration-based verification, with logging throughout.
    /// This layer is transport-agnostic and depends only on <see cref="IFx3DeviceBackend"/>.
    /// </summary>
    public sealed class FlasherService
    {
        private readonly IFx3DeviceBackend _backend;
        private readonly BoardProfileStore _profiles;
        private readonly EepromSafetyGate _gate;
        private readonly FirmwareImageValidator _validator;
        private readonly IOperationLogger _logger;

        public FlasherService(
            IFx3DeviceBackend backend,
            BoardProfileStore profiles,
            EepromSafetyGate gate,
            FirmwareImageValidator validator,
            IOperationLogger logger)
        {
            _backend = backend ?? throw new ArgumentNullException("backend");
            _profiles = profiles ?? throw new ArgumentNullException("profiles");
            _gate = gate ?? throw new ArgumentNullException("gate");
            _validator = validator ?? throw new ArgumentNullException("validator");
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public IReadOnlyList<Fx3DeviceInfo> Refresh()
        {
            IReadOnlyList<Fx3DeviceInfo> devices = _backend.Enumerate();
            _logger.Log(LogSeverity.Info, string.Format("Found {0} device(s).", devices.Count));
            return devices;
        }

        public BoardProfile ResolveProfile(Fx3DeviceInfo device)
        {
            if (device == null)
            {
                return null;
            }

            bool ambiguous;
            return _profiles.Resolve(device.VendorId, device.ProductId, out ambiguous);
        }

        public ImageValidationResult ValidateImage(FirmwareImage image, Fx3DeviceInfo device)
        {
            BoardProfile profile = ResolveProfile(device);
            return _validator.Validate(image, profile);
        }

        public FlasherOperationResult Program(
            Fx3DeviceInfo device,
            string imageFilePath,
            bool operatorConfirmed,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            BoardProfile profile = ResolveProfile(device);

            FirmwareImage image;
            try
            {
                image = FirmwareImage.Load(imageFilePath);
            }
            catch (Exception ex)
            {
                return LogAndFail(device, "Failed to load image: " + ex.Message);
            }

            ImageValidationResult validation = _validator.Validate(image, profile);
            LogValidation(device, validation);

            SafetyDecision safety = _gate.Evaluate(
                FlashOperation.Program, device, profile, validation, operatorConfirmed);
            if (!safety.IsAllowed)
            {
                return LogBlocked(device, "Program blocked by safety gate.", safety);
            }

            _logger.Log(LogSeverity.Info, "Programming EEPROM: " + image.FileName, device.Index);
            DeviceOperationResult op = _backend.ProgramEeprom(device, imageFilePath, progress, cancellationToken);
            if (!op.Success)
            {
                return LogAndFail(device, op.Message, safety);
            }

            bool verified = VerifyState(device, DeviceState.Programmed);
            _logger.Log(
                verified ? LogSeverity.Success : LogSeverity.Warning,
                verified
                    ? "Program verified: device re-enumerated as programmed."
                    : "Programmed, but re-enumeration did not confirm the programmed state.",
                device.Index);

            return new FlasherOperationResult
            {
                Success = true,
                Message = op.Message,
                Safety = safety,
                Verified = verified
            };
        }

        public FlasherOperationResult Erase(
            Fx3DeviceInfo device,
            string eraseImageFilePath,
            bool operatorConfirmed,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            BoardProfile profile = ResolveProfile(device);
            string erasePath = eraseImageFilePath;
            if (string.IsNullOrEmpty(erasePath) && profile != null)
            {
                erasePath = profile.EraseImagePath;
            }

            if (string.IsNullOrEmpty(erasePath) || !File.Exists(erasePath))
            {
                return LogAndFail(device,
                    "No valid erase image is available. Configure the board profile's EraseImagePath or select one.");
            }

            SafetyDecision safety = _gate.Evaluate(
                FlashOperation.EraseToBlank, device, profile, null, operatorConfirmed);
            if (!safety.IsAllowed)
            {
                return LogBlocked(device, "Erase blocked by safety gate.", safety);
            }

            _logger.Log(LogSeverity.Info, "Erasing EEPROM to blank bootloader.", device.Index);
            DeviceOperationResult op = _backend.EraseEeprom(device, erasePath, progress, cancellationToken);
            if (!op.Success)
            {
                return LogAndFail(device, op.Message, safety);
            }

            bool verified = VerifyState(device, DeviceState.BlankBootloader);
            _logger.Log(
                verified ? LogSeverity.Success : LogSeverity.Warning,
                verified
                    ? "Erase verified: device re-enumerated as blank bootloader."
                    : "Erase written, but re-enumeration did not confirm blank bootloader (power cycle may be required).",
                device.Index);

            return new FlasherOperationResult
            {
                Success = true,
                Message = op.Message,
                Safety = safety,
                Verified = verified
            };
        }

        /// <summary>Re-enumerate and confirm the device now reports the expected state.</summary>
        private bool VerifyState(Fx3DeviceInfo device, DeviceState expected)
        {
            IReadOnlyList<Fx3DeviceInfo> devices = _backend.Enumerate();

            // Prefer matching by serial number when the device exposes a stable one.
            if (!string.IsNullOrEmpty(device.SerialNumber))
            {
                foreach (Fx3DeviceInfo d in devices)
                {
                    if (string.Equals(d.SerialNumber, device.SerialNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        return d.State == expected;
                    }
                }
            }

            // Otherwise, if exactly one device of the same profile is present, verify its state.
            Fx3DeviceInfo singleton = null;
            int count = 0;
            foreach (Fx3DeviceInfo d in devices)
            {
                if (!string.IsNullOrEmpty(device.ProfileName)
                    && string.Equals(d.ProfileName, device.ProfileName, StringComparison.Ordinal))
                {
                    singleton = d;
                    count++;
                }
            }

            return count == 1 && singleton.State == expected;
        }

        private void LogValidation(Fx3DeviceInfo device, ImageValidationResult validation)
        {
            foreach (string w in validation.Warnings)
            {
                _logger.Log(LogSeverity.Warning, "Image warning: " + w, device != null ? device.Index : -1);
            }

            foreach (string e in validation.Errors)
            {
                _logger.Log(LogSeverity.Error, "Image error: " + e, device != null ? device.Index : -1);
            }
        }

        private FlasherOperationResult LogAndFail(Fx3DeviceInfo device, string message, SafetyDecision safety = null)
        {
            _logger.Log(LogSeverity.Error, message, device != null ? device.Index : -1);
            return FlasherOperationResult.Fail(message, safety);
        }

        private FlasherOperationResult LogBlocked(Fx3DeviceInfo device, string headline, SafetyDecision safety)
        {
            _logger.Log(LogSeverity.Error, headline, device != null ? device.Index : -1);
            foreach (string b in safety.Blockers)
            {
                _logger.Log(LogSeverity.Error, "  - " + b, device != null ? device.Index : -1);
            }

            return FlasherOperationResult.Fail(headline, safety);
        }
    }
}
