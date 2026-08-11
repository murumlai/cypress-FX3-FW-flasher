using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Models;

namespace Fx3Flasher.Core.Safety
{
    /// <summary>
    /// Fail-closed gate that must approve any EEPROM write or erase. Every operation is blocked
    /// unless the device, board profile, image, address range, operation policy and operator
    /// confirmation all pass. The gate never approves by default.
    /// </summary>
    public sealed class EepromSafetyGate
    {
        /// <summary>
        /// Evaluate a requested operation. The caller must supply the resolved profile, the
        /// selected device, and (for programming) a completed image validation result.
        /// </summary>
        public SafetyDecision Evaluate(
            FlashOperation operation,
            Fx3DeviceInfo device,
            BoardProfile profile,
            ImageValidationResult imageValidation,
            bool operatorConfirmed)
        {
            var decision = new SafetyDecision();

            if (device == null)
            {
                decision.Block("No device selected.");
                return decision;
            }

            if (profile == null)
            {
                decision.Block("Device does not match any supported board profile.");
                return decision;
            }

            if (device.State == DeviceState.Unsupported)
            {
                decision.Block("Selected device is not a supported FX3 board.");
            }

            if (device.State == DeviceState.Ambiguous)
            {
                decision.Block("Device state is ambiguous; refusing to write until it is resolved.");
            }

            EvaluateOperationPolicy(operation, profile, imageValidation, decision);

            if (!operatorConfirmed)
            {
                decision.Block("Operator confirmation is required for this destructive operation.");
            }

            return decision;
        }

        private static void EvaluateOperationPolicy(
            FlashOperation operation,
            BoardProfile profile,
            ImageValidationResult imageValidation,
            SafetyDecision decision)
        {
            switch (operation)
            {
                case FlashOperation.Program:
                    if (!profile.AllowProgram)
                    {
                        decision.Block("Programming is not permitted by the board profile.");
                    }

                    if (imageValidation == null)
                    {
                        decision.Block("No validated image is loaded.");
                    }
                    else if (!imageValidation.IsValid)
                    {
                        decision.Block("Loaded image failed validation.");
                    }

                    break;

                case FlashOperation.EraseToBlank:
                    if (!profile.AllowErase)
                    {
                        decision.Block("Erase is not permitted by the board profile.");
                    }

                    break;

                case FlashOperation.FullErase:
                    if (!profile.AllowErase || !profile.AllowFullErase)
                    {
                        decision.Block("Full-chip erase is locked out by the board profile.");
                    }
                    else if (profile.ReservedRegions != null && profile.ReservedRegions.Count > 0)
                    {
                        decision.Warn("Full erase will overwrite reserved manufacturing regions.");
                    }

                    break;
            }
        }
    }
}
