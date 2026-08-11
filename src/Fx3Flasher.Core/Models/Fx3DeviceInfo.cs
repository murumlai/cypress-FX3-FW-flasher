namespace Fx3Flasher.Core.Models
{
    /// <summary>
    /// Immutable snapshot of a discovered FX3 device, including its stable session index,
    /// USB identity and classified firmware state.
    /// </summary>
    public sealed class Fx3DeviceInfo
    {
        public int Index { get; set; }
        public int VendorId { get; set; }
        public int ProductId { get; set; }
        public string SerialNumber { get; set; }
        public string FriendlyName { get; set; }
        public string DevicePath { get; set; }
        public DeviceState State { get; set; } = DeviceState.Unknown;

        /// <summary>Name of the matched board profile, or null when unsupported.</summary>
        public string ProfileName { get; set; }

        public bool IsSupported
        {
            get { return !string.IsNullOrEmpty(ProfileName) && State != DeviceState.Unsupported; }
        }

        public string UsbIdText
        {
            get { return string.Format("{0:X4}:{1:X4}", VendorId, ProductId); }
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2}) {3}", Index, FriendlyName, UsbIdText, State);
        }
    }
}
