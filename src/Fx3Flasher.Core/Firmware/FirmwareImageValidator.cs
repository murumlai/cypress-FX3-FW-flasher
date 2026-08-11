using System;
using Fx3Flasher.Core.Models;

namespace Fx3Flasher.Core.Firmware
{
    /// <summary>
    /// Validates a Cypress FX3 boot image (.img) structurally and against a board profile.
    /// The FX3 image format is: 'CY' signature, control/type bytes, a list of
    /// [length(words), address, data] sections terminated by a zero-length record whose
    /// address is the entry point, followed by a 32-bit checksum of all payload words.
    /// </summary>
    public sealed class FirmwareImageValidator
    {
        private const byte SignatureC = 0x43; // 'C'
        private const byte SignatureY = 0x59; // 'Y'
        private const byte NormalImageType = 0xB0;

        public ImageValidationResult Validate(FirmwareImage image, BoardProfile profile)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            var result = new ImageValidationResult();
            byte[] data = image.Data;

            if (data.Length < 8)
            {
                result.AddError("Image is too small to be a valid FX3 boot image.");
                return result;
            }

            if (data[0] != SignatureC || data[1] != SignatureY)
            {
                result.AddError("Missing FX3 'CY' image signature; file is not an FX3 boot image.");
                return result;
            }

            if (data[3] != NormalImageType)
            {
                result.AddWarning(string.Format(
                    "Unexpected image type 0x{0:X2} (expected 0x{1:X2} for a normal firmware image).",
                    data[3], NormalImageType));
            }

            ParseSections(data, result);

            if (profile != null)
            {
                ApplyProfilePolicy(image, profile, result);
            }

            return result;
        }

        private static void ParseSections(byte[] data, ImageValidationResult result)
        {
            int offset = 4;
            uint checksum = 0;
            long payloadBytes = 0;

            while (true)
            {
                if (offset + 8 > data.Length)
                {
                    result.AddError("Image truncated while reading a section header.");
                    return;
                }

                uint lengthWords = ReadUInt32(data, offset);
                uint address = ReadUInt32(data, offset + 4);
                offset += 8;

                if (lengthWords == 0)
                {
                    // Zero-length record: 'address' is the entry point, checksum already consumed above.
                    result.EntryAddress = address;
                    if (offset + 4 > data.Length)
                    {
                        result.AddError("Image truncated before trailing checksum.");
                        return;
                    }

                    uint fileChecksum = ReadUInt32(data, offset);
                    offset += 4;
                    result.ChecksumValid = fileChecksum == checksum;
                    if (!result.ChecksumValid)
                    {
                        result.AddError(string.Format(
                            "Image checksum mismatch (file 0x{0:X8}, computed 0x{1:X8}).",
                            fileChecksum, checksum));
                    }

                    result.PayloadBytes = payloadBytes;

                    if (offset != data.Length)
                    {
                        result.AddWarning(string.Format(
                            "{0} trailing byte(s) after the image checksum.", data.Length - offset));
                    }

                    return;
                }

                long sectionBytes = (long)lengthWords * 4L;
                if (offset + sectionBytes > data.Length)
                {
                    result.AddError("Image section length exceeds file size; image is corrupt.");
                    return;
                }

                for (long i = 0; i < lengthWords; i++)
                {
                    checksum += ReadUInt32(data, offset + (int)(i * 4));
                }

                payloadBytes += sectionBytes;
                offset += (int)sectionBytes;
            }
        }

        private static void ApplyProfilePolicy(FirmwareImage image, BoardProfile profile, ImageValidationResult result)
        {
            long max = profile.EffectiveMaxImageSize;
            if (max > 0 && image.Length > max)
            {
                result.AddError(string.Format(
                    "Image size {0} bytes exceeds the profile limit of {1} bytes for board '{2}'.",
                    image.Length, max, profile.Name));
            }

            if (profile.EepromSizeBytes > 0 && image.Length > profile.EepromSizeBytes)
            {
                result.AddError(string.Format(
                    "Image size {0} bytes exceeds EEPROM capacity {1} bytes for board '{2}'.",
                    image.Length, profile.EepromSizeBytes, profile.Name));
            }

            if (profile.RequireChecksum && !result.ChecksumValid)
            {
                result.AddError("Board profile requires a valid image checksum before programming.");
            }
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));
        }
    }
}
