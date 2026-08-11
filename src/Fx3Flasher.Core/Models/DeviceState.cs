namespace Fx3Flasher.Core.Models
{
    /// <summary>Classification of a discovered FX3 device's persistent firmware state.</summary>
    public enum DeviceState
    {
        Unknown = 0,
        BlankBootloader = 1,
        Programmed = 2,
        Unsupported = 3,
        Ambiguous = 4
    }
}
