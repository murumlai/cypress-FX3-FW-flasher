using CyUSB;
using Fx3Flasher.Core.Devices;

namespace Fx3Flasher.Cypress
{
    /// <summary>Maps Cypress FX3 firmware-download error codes to friendly operation results.</summary>
    internal static class CyUsbErrorMap
    {
        public static DeviceOperationResult ToResult(
            CyFX3Device device,
            FX3_FWDWNLOAD_ERROR_CODE code,
            string successMessage)
        {
            string raw = code.ToString();
            if (code == FX3_FWDWNLOAD_ERROR_CODE.SUCCESS)
            {
                return DeviceOperationResult.Ok(successMessage, raw);
            }

            string detail = Describe(code);
            string libraryText = SafeGetFwErrorString(device, code);
            if (!string.IsNullOrEmpty(libraryText))
            {
                detail = detail + " (" + libraryText + ")";
            }

            detail = detail + " [code " + raw + "]";
            return DeviceOperationResult.Fail(detail, raw);
        }

        private static string Describe(FX3_FWDWNLOAD_ERROR_CODE code)
        {
            switch (code)
            {
                case FX3_FWDWNLOAD_ERROR_CODE.INVALID_MEDIA_TYPE:
                    return "The FX3 rejected the target media type.";
                case FX3_FWDWNLOAD_ERROR_CODE.INVALID_FWSIGNATURE:
                    return "Image signature is invalid; refusing to write.";
                case FX3_FWDWNLOAD_ERROR_CODE.DEVICE_CREATE_FAILED:
                    return "Failed to open the FX3 device for programming.";
                case FX3_FWDWNLOAD_ERROR_CODE.INCORRECT_IMAGE_LENGTH:
                    return "Image length is incorrect for the FX3 boot format.";
                case FX3_FWDWNLOAD_ERROR_CODE.INVALID_FILE:
                    return "The firmware file could not be read.";
                case FX3_FWDWNLOAD_ERROR_CODE.SPILASH_ERASE_FAILED:
                    return "SPI flash erase failed.";
                case FX3_FWDWNLOAD_ERROR_CODE.CORRUPT_FIRMWARE_IMAGE_FILE:
                    return "Firmware image file is corrupt.";
                case FX3_FWDWNLOAD_ERROR_CODE.EXCEED_IMAGE_LENGTH:
                    return "Image exceeds the maximum programmable length.";
                case FX3_FWDWNLOAD_ERROR_CODE.I2CE2PROM_UNKNOWN_I2C_SIZE:
                    return "The FX3 could not determine the I2C EEPROM size.";
                default:
                    return "Firmware programming failed.";
            }
        }

        private static string SafeGetFwErrorString(CyFX3Device device, FX3_FWDWNLOAD_ERROR_CODE code)
        {
            if (device == null)
            {
                return null;
            }

            try
            {
                return device.GetFwErrorString(code);
            }
            catch
            {
                return null;
            }
        }
    }
}
