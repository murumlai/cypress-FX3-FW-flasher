namespace Fx3Flasher.Core.Models
{
    /// <summary>Firmware download target media, mirroring the Cypress FX3 media types.</summary>
    public enum FlashMedia
    {
        Ram = 0,
        I2cEeprom = 1,
        SpiFlash = 2
    }
}
