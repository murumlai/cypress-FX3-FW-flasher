using System;
using System.Collections.Generic;
using System.Threading;
using CyUSB;
using Fx3Flasher.Core.Devices;
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

                FX3_FWDWNLOAD_ERROR_CODE code = fx3.DownloadFw(imageFilePath, FX3_FWDWNLOAD_MEDIA_TYPE.I2CE2PROM);

                Report(progress, 90, "Finalizing");
                DeviceOperationResult result = CyUsbErrorMap.ToResult(
                    fx3, code, "EEPROM programmed successfully.");

                Report(progress, 100, result.Success ? "Done" : "Failed");
                return result;
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

                FX3_FWDWNLOAD_ERROR_CODE code = fx3.DownloadFw(eraseImageFilePath, FX3_FWDWNLOAD_MEDIA_TYPE.I2CE2PROM);

                Report(progress, 90, "Finalizing");
                DeviceOperationResult result = CyUsbErrorMap.ToResult(
                    fx3, code, "Erase image written; device will return to blank bootloader after power cycle.");

                Report(progress, 100, result.Success ? "Done" : "Failed");
                return result;
            }
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
