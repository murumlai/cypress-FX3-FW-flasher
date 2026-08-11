using System;
using System.Collections.Generic;

namespace Fx3Flasher.Core.Models
{
    /// <summary>A single USB VID/PID identity pair.</summary>
    public sealed class UsbId
    {
        public int VendorId { get; set; }
        public int ProductId { get; set; }

        public bool Matches(int vid, int pid)
        {
            return vid == VendorId && pid == ProductId;
        }

        public override string ToString()
        {
            return string.Format("{0:X4}:{1:X4}", VendorId, ProductId);
        }
    }

    /// <summary>
    /// Externally configured description of a supported FX3 board, including EEPROM geometry,
    /// identity signatures, protected regions and the operations that are permitted on it.
    /// </summary>
    public sealed class BoardProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Identities seen when the device is in blank bootloader mode.</summary>
        public List<UsbId> BootloaderIds { get; set; } = new List<UsbId>();

        /// <summary>Identities seen when the device is running programmed application firmware.</summary>
        public List<UsbId> ApplicationIds { get; set; } = new List<UsbId>();

        public long EepromSizeBytes { get; set; }
        public int I2cAddress { get; set; }
        public int PageSizeBytes { get; set; }
        public int WriteDelayMs { get; set; }

        /// <summary>Largest firmware image, in bytes, that may be written to this board.</summary>
        public long MaxImageSizeBytes { get; set; }

        /// <summary>Regions that must never be overwritten unless <see cref="AllowFullErase"/> is set.</summary>
        public List<EepromRegion> ReservedRegions { get; set; } = new List<EepromRegion>();

        /// <summary>Operations permitted by policy for this board.</summary>
        public bool AllowProgram { get; set; } = true;
        public bool AllowErase { get; set; } = true;
        public bool AllowFullErase { get; set; } = false;

        /// <summary>Optional allow-list of image identifiers embedded by the internal image tool.</summary>
        public List<string> AllowedImageIds { get; set; } = new List<string>();

        /// <summary>Whether an image checksum must validate before programming is permitted.</summary>
        public bool RequireChecksum { get; set; } = true;

        public bool MatchesBootloader(int vid, int pid)
        {
            return Contains(BootloaderIds, vid, pid);
        }

        public bool MatchesApplication(int vid, int pid)
        {
            return Contains(ApplicationIds, vid, pid);
        }

        public bool MatchesAny(int vid, int pid)
        {
            return MatchesBootloader(vid, pid) || MatchesApplication(vid, pid);
        }

        private static bool Contains(List<UsbId> ids, int vid, int pid)
        {
            if (ids == null)
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != null && ids[i].Matches(vid, pid))
                {
                    return true;
                }
            }

            return false;
        }

        public long EffectiveMaxImageSize
        {
            get
            {
                if (MaxImageSizeBytes > 0 && MaxImageSizeBytes < EepromSizeBytes)
                {
                    return MaxImageSizeBytes;
                }

                return EepromSizeBytes > 0 ? EepromSizeBytes : Math.Max(MaxImageSizeBytes, 0);
            }
        }
    }
}
