using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Models;
using Xunit;

namespace Fx3Flasher.Core.Tests
{
    public class FirmwareImageValidatorTests
    {
        private static BoardProfile Profile(long eeprom = 262144, long maxImage = 262144)
        {
            return new BoardProfile
            {
                Name = "Test",
                EepromSizeBytes = eeprom,
                MaxImageSizeBytes = maxImage,
                RequireChecksum = true
            };
        }

        [Fact]
        public void ValidImage_PassesWithChecksum()
        {
            byte[] data = Fx3ImageBuilder.BuildValid(0x40003000, 0x40003000, new uint[] { 0x11111111, 0x22222222 });
            var image = new FirmwareImage("test.img", data);

            ImageValidationResult result = new FirmwareImageValidator().Validate(image, Profile());

            Assert.True(result.IsValid, string.Join("; ", result.Errors));
            Assert.True(result.ChecksumValid);
            Assert.Equal(0x40003000u, result.EntryAddress);
            Assert.Equal(8, result.PayloadBytes);
        }

        [Fact]
        public void MissingSignature_IsRejected()
        {
            var image = new FirmwareImage("bad.img", new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });

            ImageValidationResult result = new FirmwareImageValidator().Validate(image, Profile());

            Assert.False(result.IsValid);
        }

        [Fact]
        public void BrokenChecksum_IsRejected()
        {
            byte[] data = Fx3ImageBuilder.WithBrokenChecksum(0x40003000, 0x40003000, new uint[] { 0xDEADBEEF });
            var image = new FirmwareImage("test.img", data);

            ImageValidationResult result = new FirmwareImageValidator().Validate(image, Profile());

            Assert.False(result.IsValid);
            Assert.False(result.ChecksumValid);
        }

        [Fact]
        public void ImageLargerThanEeprom_IsRejected()
        {
            byte[] data = Fx3ImageBuilder.BuildValid(0x40003000, 0x40003000, new uint[] { 1, 2, 3, 4 });
            var image = new FirmwareImage("test.img", data);

            ImageValidationResult result = new FirmwareImageValidator().Validate(image, Profile(eeprom: 8, maxImage: 8));

            Assert.False(result.IsValid);
        }

        [Fact]
        public void UnexpectedImageType_ProducesWarningButStaysValid()
        {
            byte[] data = Fx3ImageBuilder.BuildValid(0x40003000, 0x40003000, new uint[] { 5 }, imageType: 0xB2);
            var image = new FirmwareImage("test.img", data);

            ImageValidationResult result = new FirmwareImageValidator().Validate(image, Profile());

            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
        }
    }
}
