using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Models;
using Fx3Flasher.Core.Safety;
using Xunit;

namespace Fx3Flasher.Core.Tests
{
    public class EepromSafetyGateTests
    {
        private static Fx3DeviceInfo Device(DeviceState state = DeviceState.BlankBootloader)
        {
            return new Fx3DeviceInfo { Index = 0, ProfileName = "Test", State = state };
        }

        private static BoardProfile Profile()
        {
            return new BoardProfile { Name = "Test", AllowProgram = true, AllowErase = true, AllowFullErase = false };
        }

        private static ImageValidationResult ValidImage()
        {
            byte[] data = Fx3ImageBuilder.BuildValid(0x40003000, 0x40003000, new uint[] { 1, 2 });
            return new FirmwareImageValidator().Validate(new FirmwareImage("ok.img", data), Profile());
        }

        private static ImageValidationResult InvalidImage()
        {
            byte[] data = Fx3ImageBuilder.WithBrokenChecksum(0x40003000, 0x40003000, new uint[] { 1 });
            return new FirmwareImageValidator().Validate(new FirmwareImage("bad.img", data), Profile());
        }

        [Fact]
        public void Program_Allowed_WhenEverythingSatisfied()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.Program, Device(), Profile(), ValidImage(), operatorConfirmed: true);

            Assert.True(d.IsAllowed);
        }

        [Fact]
        public void Program_Blocked_WithoutOperatorConfirmation()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.Program, Device(), Profile(), ValidImage(), operatorConfirmed: false);

            Assert.False(d.IsAllowed);
        }

        [Fact]
        public void Program_Blocked_WithoutProfile()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.Program, Device(), null, ValidImage(), operatorConfirmed: true);

            Assert.False(d.IsAllowed);
        }

        [Fact]
        public void Program_Blocked_WhenImageInvalid()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.Program, Device(), Profile(), InvalidImage(), operatorConfirmed: true);

            Assert.False(d.IsAllowed);
        }

        [Fact]
        public void FullErase_Blocked_WhenProfileDisallows()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.FullErase, Device(), Profile(), null, operatorConfirmed: true);

            Assert.False(d.IsAllowed);
        }

        [Fact]
        public void Program_Blocked_WhenDeviceAmbiguous()
        {
            SafetyDecision d = new EepromSafetyGate().Evaluate(
                FlashOperation.Program, Device(DeviceState.Ambiguous), Profile(), ValidImage(), operatorConfirmed: true);

            Assert.False(d.IsAllowed);
        }
    }
}
