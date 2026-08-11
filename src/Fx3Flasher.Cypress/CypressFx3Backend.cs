using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using CyUSB;
using Fx3Flasher.Core.Devices;
using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Models;
using Fx3Flasher.Core.Profiles;

namespace Fx3Flasher.Cypress
{
    /// <summary>
    /// FX3 device backend implemented over the Cypress CyUSB managed library. Handles enumeration,
    /// blank-vs-programmed classification (via the FX3 bootloader state), and I2C EEPROM programming.
    /// </summary>
    public sealed class CypressFx3Backend : IFx3DeviceBackend
    {
        private readonly BoardProfileStore _profiles;

        public CypressFx3Backend(BoardProfileStore profiles)
        {
            _profiles = profiles ?? throw new ArgumentNullException("profiles");
        }

        public IReadOnlyList<Fx3DeviceInfo> Enumerate()
        {
            var results = new List<Fx3DeviceInfo>();
            int index = 0;

            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                foreach (object item in list)
                {
                    var dev = item as USBDevice;
                    if (dev == null)
                    {
                        continue;
                    }

                    results.Add(Describe(dev, index));
                    index++;
                }
            }

            return results;
        }

        public DeviceOperationResult ProgramEeprom(
            Fx3DeviceInfo device,
            string imageFilePath,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            if (device == null)
            {
                return DeviceOperationResult.Fail("No device selected.");
            }

            if (string.IsNullOrEmpty(imageFilePath))
            {
                return DeviceOperationResult.Fail("No image file specified.");
            }

            Report(progress, 5, "Locating device");

            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                CyFX3Device fx3 = FindFx3(list, device);
                if (fx3 == null)
                {
                    return DeviceOperationResult.Fail(
                        "Selected FX3 device is no longer present or is not accessible via the CyUSB driver.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, 25, "Writing I2C EEPROM");

                string preparedImagePath = PrepareI2cEepromImage(imageFilePath, device);
                try
                {
                    FX3_FWDWNLOAD_ERROR_CODE code = fx3.DownloadFw(preparedImagePath, FX3_FWDWNLOAD_MEDIA_TYPE.I2CE2PROM);

                    Report(progress, 90, "Finalizing");
                    DeviceOperationResult result = CyUsbErrorMap.ToResult(
                        fx3, code, "EEPROM programmed successfully.");

                    Report(progress, 100, result.Success ? "Done" : "Failed");
                    return result;
                }
                finally
                {
                    DeletePreparedImage(preparedImagePath, imageFilePath);
                }
            }
        }

        public DeviceOperationResult DownloadToRam(
            Fx3DeviceInfo device,
            string imageFilePath,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            if (device == null)
            {
                return DeviceOperationResult.Fail("No device selected.");
            }

            if (string.IsNullOrEmpty(imageFilePath))
            {
                return DeviceOperationResult.Fail("No image file specified.");
            }

            Report(progress, 5, "Locating device");

            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                CyFX3Device fx3 = FindFx3(list, device);
                if (fx3 == null)
                {
                    return DeviceOperationResult.Fail(
                        "Selected FX3 device is no longer present or is not accessible via the CyUSB driver.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, 25, "Downloading to RAM");

                FX3_FWDWNLOAD_ERROR_CODE code = fx3.DownloadFw(imageFilePath, FX3_FWDWNLOAD_MEDIA_TYPE.RAM);

                Report(progress, 100, code == FX3_FWDWNLOAD_ERROR_CODE.SUCCESS ? "Done" : "Failed");
                return CyUsbErrorMap.ToResult(fx3, code, "Image downloaded to RAM (non-persistent).");
            }
        }

        public DeviceOperationResult EraseEeprom(
            Fx3DeviceInfo device,
            string eraseImageFilePath,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            if (device == null)
            {
                return DeviceOperationResult.Fail("No device selected.");
            }

            if (string.IsNullOrEmpty(eraseImageFilePath))
            {
                return DeviceOperationResult.Fail(
                    "No erase image configured. Provide a dedicated erase .img to return the board to blank bootloader.");
            }

            Report(progress, 5, "Locating device");

            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                CyFX3Device fx3 = FindFx3(list, device);
                if (fx3 == null)
                {
                    return DeviceOperationResult.Fail(
                        "Selected FX3 device is no longer present or is not accessible via the CyUSB driver.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, 25, "Writing erase image to EEPROM");

                string preparedImagePath = PrepareI2cEepromImage(eraseImageFilePath, device);
                try
                {
                    FX3_FWDWNLOAD_ERROR_CODE code = fx3.DownloadFw(preparedImagePath, FX3_FWDWNLOAD_MEDIA_TYPE.I2CE2PROM);

                    Report(progress, 90, "Finalizing");
                    DeviceOperationResult result = CyUsbErrorMap.ToResult(
                        fx3, code, "Erase image written; device will return to blank bootloader after power cycle.");

                    Report(progress, 100, result.Success ? "Done" : "Failed");
                    return result;
                }
                finally
                {
                    DeletePreparedImage(preparedImagePath, eraseImageFilePath);
                }
            }
        }

        public DeviceOperationResult DetectEeprom(
            Fx3DeviceInfo device,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            if (device == null)
            {
                return DeviceOperationResult.Fail("No device selected.");
            }

            Report(progress, 5, "Locating device");

            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                CyFX3Device fx3 = FindFx3(list, device);
                if (fx3 == null)
                {
                    return DeviceOperationResult.Fail(
                        "Selected FX3 device is no longer present or is not accessible via the CyUSB driver.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, 40, "Probing I2C EEPROM");

                DeviceOperationResult result = RunI2cEepromProbe(fx3);
                Report(progress, 100, result.Success ? "Done" : "Failed");
                return result;
            }
        }

        private static DeviceOperationResult RunI2cEepromProbe(CyFX3Device fx3)
        {
            CyControlEndPoint control = GetControlEndPoint(fx3);
            if (control == null)
            {
                return DeviceOperationResult.Fail("Could not access the FX3 control endpoint for I2C EEPROM probing.");
            }

            byte[] image = MinimalBootImage.Build();
            const int chunkSize = 4096;
            int maxPacketSize = Math.Max(control.MaxPktSize, 1);
            int fullChunks = image.Length / chunkSize;
            int remainder = image.Length % chunkSize;
            int offset = 0;

            control.TimeOut = 5000;
            control.Target = CyConst.TGT_DEVICE;
            control.ReqType = CyConst.REQ_VENDOR;
            control.Direction = CyConst.DIR_TO_DEVICE;
            control.ReqCode = 0xBA;
            control.Value = 0;
            control.Index = 0;

            for (int chunk = 0; chunk < fullChunks; chunk++)
            {
                byte[] buffer = new byte[chunkSize];
                Array.Copy(image, offset, buffer, 0, chunkSize);
                int length = chunkSize;
                if (!control.XferData(ref buffer, ref length))
                {
                    return DeviceOperationResult.Fail(
                        "I2C EEPROM probe failed during the first-bank 4 KB write. " + FormatControlStatus(control));
                }

                control.Index += (ushort)length;
                offset += chunkSize;
            }

            if (remainder > 0)
            {
                int paddedLength = remainder;
                if (paddedLength % maxPacketSize != 0)
                {
                    paddedLength += maxPacketSize - (paddedLength % maxPacketSize);
                }

                byte[] tail = new byte[paddedLength];
                Array.Copy(image, offset, tail, 0, remainder);
                if (!control.XferData(ref tail, ref paddedLength))
                {
                    return DeviceOperationResult.Fail(
                        "I2C EEPROM probe wrote the first 4 KB but failed on the final padded tail write. " + FormatControlStatus(control));
                }

                control.ReqCode = 0xBB;
                control.Direction = CyConst.DIR_FROM_DEVICE;
                if (!control.XferData(ref tail, ref paddedLength))
                {
                    return DeviceOperationResult.Fail(
                        "I2C EEPROM probe writes completed, but the FX3 status/readback command failed. " + FormatControlStatus(control));
                }
            }

            return DeviceOperationResult.Ok(
                "I2C EEPROM detected and writable: a ~4 KB first-bank probe write completed. Re-program or erase to restore the device.");
        }

        private static CyControlEndPoint GetControlEndPoint(CyFX3Device fx3)
        {
            Type type = fx3.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    "ControlEndPt", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field.GetValue(fx3) as CyControlEndPoint;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static string FormatControlStatus(CyControlEndPoint control)
        {
            return string.Format(
                "USB status: Usbd=0x{0:X8}, Nt=0x{1:X8}, LastError={2}, BytesWritten={3}.",
                control.UsbdStatus, control.NtStatus, control.LastError, control.BytesWritten);
        }

        private string PrepareI2cEepromImage(string imageFilePath, Fx3DeviceInfo device)
        {
            BoardProfile profile = ResolveProfile(device);
            if (profile == null || profile.EepromSizeBytes <= 65536)
            {
                return imageFilePath;
            }

            byte[] data = File.ReadAllBytes(imageFilePath);
            if (data.Length < 4 || data[0] != 0x43 || data[1] != 0x59)
            {
                return imageFilePath;
            }

            byte desiredControl = (byte)((data[2] & unchecked((byte)~0x0E)) | 0x0E);
            if (data[2] == desiredControl)
            {
                return imageFilePath;
            }

            data[2] = desiredControl;
            string tempPath = Path.Combine(
                Path.GetTempPath(), "fx3-i2c-" + Guid.NewGuid().ToString("N") + ".img");
            File.WriteAllBytes(tempPath, data);
            return tempPath;
        }

        private BoardProfile ResolveProfile(Fx3DeviceInfo device)
        {
            bool ambiguous;
            return device == null ? null : _profiles.Resolve(device.VendorId, device.ProductId, out ambiguous);
        }

        private static void DeletePreparedImage(string preparedImagePath, string originalImagePath)
        {
            if (string.Equals(preparedImagePath, originalImagePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try { File.Delete(preparedImagePath); }
            catch { }
        }

        private Fx3DeviceInfo Describe(USBDevice dev, int index)
        {
            int vid = dev.VendorID;
            int pid = dev.ProductID;

            bool ambiguous;
            BoardProfile profile = _profiles.Resolve(vid, pid, out ambiguous);

            DeviceState state = ClassifyState(dev, profile, ambiguous);

            return new Fx3DeviceInfo
            {
                Index = index,
                VendorId = vid,
                ProductId = pid,
                SerialNumber = dev.SerialNumber,
                FriendlyName = string.IsNullOrEmpty(dev.FriendlyName) ? dev.Name : dev.FriendlyName,
                DevicePath = dev.Path,
                State = state,
                ProfileName = profile != null ? profile.Name : null
            };
        }

        private static DeviceState ClassifyState(USBDevice dev, BoardProfile profile, bool ambiguous)
        {
            if (ambiguous)
            {
                return DeviceState.Ambiguous;
            }

            if (profile == null)
            {
                return DeviceState.Unsupported;
            }

            var fx3 = dev as CyFX3Device;
            if (fx3 != null)
            {
                try
                {
                    return fx3.IsBootLoaderRunning() ? DeviceState.BlankBootloader : DeviceState.Programmed;
                }
                catch
                {
                    return DeviceState.Unknown;
                }
            }

            // Not exposing the FX3 handle: fall back to identity-based classification.
            if (profile.MatchesBootloader(dev.VendorID, dev.ProductID))
            {
                return DeviceState.BlankBootloader;
            }

            if (profile.MatchesApplication(dev.VendorID, dev.ProductID))
            {
                return DeviceState.Programmed;
            }

            return DeviceState.Unknown;
        }

        private static CyFX3Device FindFx3(USBDeviceList list, Fx3DeviceInfo device)
        {
            foreach (object item in list)
            {
                var dev = item as USBDevice;
                if (dev == null)
                {
                    continue;
                }

                bool pathMatch = !string.IsNullOrEmpty(device.DevicePath)
                    && string.Equals(dev.Path, device.DevicePath, StringComparison.OrdinalIgnoreCase);
                bool identityMatch = dev.VendorID == device.VendorId
                    && dev.ProductID == device.ProductId
                    && string.Equals(dev.SerialNumber ?? string.Empty, device.SerialNumber ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);

                if (pathMatch || identityMatch)
                {
                    return dev as CyFX3Device;
                }
            }

            return null;
        }

        private static void Report(IProgress<OperationProgress> progress, int percent, string stage)
        {
            if (progress != null)
            {
                progress.Report(new OperationProgress { Percent = percent, Stage = stage });
            }
        }
    }
}
