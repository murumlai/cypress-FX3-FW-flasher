using System.Collections.Generic;
using System.IO;

namespace Fx3Flasher.Core.Tests
{
    /// <summary>Builds synthetic Cypress FX3 boot images for validator tests.</summary>
    internal static class Fx3ImageBuilder
    {
        /// <summary>
        /// Build a well-formed FX3 image with a single data section.
        /// Layout: 'CY' + ctl + type, then [lengthWords][address][data], then [0][entry][checksum].
        /// </summary>
        public static byte[] BuildValid(uint address, uint entry, uint[] dataWords, byte imageType = 0xB0)
        {
            var bytes = new List<byte>();
            bytes.Add(0x43); // 'C'
            bytes.Add(0x59); // 'Y'
            bytes.Add(0x00); // image control
            bytes.Add(imageType);

            AppendUInt32(bytes, (uint)dataWords.Length);
            AppendUInt32(bytes, address);

            uint checksum = 0;
            foreach (uint word in dataWords)
            {
                AppendUInt32(bytes, word);
                checksum += word;
            }

            AppendUInt32(bytes, 0); // terminator length
            AppendUInt32(bytes, entry);
            AppendUInt32(bytes, checksum);

            return bytes.ToArray();
        }

        public static byte[] WithBrokenChecksum(uint address, uint entry, uint[] dataWords)
        {
            byte[] image = BuildValid(address, entry, dataWords);
            image[image.Length - 1] ^= 0xFF; // corrupt the trailing checksum
            return image;
        }

        public static string WriteTemp(byte[] data)
        {
            string path = Path.Combine(Path.GetTempPath(), "fx3test-" + System.Guid.NewGuid().ToString("N") + ".img");
            File.WriteAllBytes(path, data);
            return path;
        }

        private static void AppendUInt32(List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 24) & 0xFF));
        }
    }
}
