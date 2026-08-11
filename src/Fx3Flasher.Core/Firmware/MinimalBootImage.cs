using System.Collections.Generic;

namespace Fx3Flasher.Core.Firmware
{
    /// <summary>
    /// Builds a small valid FX3 boot image used as an EEPROM write probe. Cypress DownloadFw derives
    /// the target EEPROM density from the image length, so the probe is padded to ~4 KB: large enough
    /// to map to a 2-address-byte density (matching the AT24CM01) yet entirely within the first bank.
    /// </summary>
    public static class MinimalBootImage
    {
        // ~4 KB probe: 1024 zero data words keep the write inside EEPROM bank 0 (below 64 KB).
        private const int DataWords = 1024;

        public static byte[] Build()
        {
            var bytes = new List<byte>();
            bytes.Add(0x43); // 'C'
            bytes.Add(0x59); // 'Y'
            bytes.Add(0x00); // image control
            bytes.Add(0xB0); // normal image type

            const uint address = 0x40003000;
            const uint entry = 0x40003000;

            AppendUInt32(bytes, DataWords);
            AppendUInt32(bytes, address);
            for (int i = 0; i < DataWords; i++)
            {
                AppendUInt32(bytes, 0); // zero payload
            }

            AppendUInt32(bytes, 0);       // terminator length
            AppendUInt32(bytes, entry);
            AppendUInt32(bytes, 0);       // checksum == sum of zero payload words

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
