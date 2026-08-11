using System.Collections.Generic;

namespace Fx3Flasher.Core.Firmware
{
    /// <summary>
    /// Builds a minimal valid FX3 boot image used as a non-diagnostic write probe: a 'CY' header,
    /// a single one-word section at a valid code address, a zero-length terminator with entry point,
    /// and the trailing checksum.
    /// </summary>
    public static class MinimalBootImage
    {
        public static byte[] Build()
        {
            var bytes = new List<byte>();
            bytes.Add(0x43); // 'C'
            bytes.Add(0x59); // 'Y'
            bytes.Add(0x00); // image control
            bytes.Add(0xB0); // normal image type

            const uint address = 0x40003000;
            const uint entry = 0x40003000;
            const uint data = 0x00000000;

            AppendUInt32(bytes, 1);       // one 32-bit word
            AppendUInt32(bytes, address);
            AppendUInt32(bytes, data);

            AppendUInt32(bytes, 0);       // terminator length
            AppendUInt32(bytes, entry);
            AppendUInt32(bytes, data);    // checksum == sum of the single data word (0)

            return bytes.ToArray();
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
