using System;
using System.Collections.Generic;
using System.Threading;
using Fx3Flasher.Core.Models;

namespace Fx3Flasher.Core.Devices
{
    /// <summary>
    /// Transport-agnostic contract for discovering FX3 devices and performing EEPROM operations.
    /// Implemented by the Cypress/CyUSB backend, and mockable for tests.
    /// </summary>
    public interface IFx3DeviceBackend
    {
        /// <summary>Enumerate currently attached FX3 devices with stable indexes.</summary>
        IReadOnlyList<Fx3DeviceInfo> Enumerate();

        /// <summary>Program a firmware image file to the device's I2C EEPROM.</summary>
        DeviceOperationResult ProgramEeprom(
            Fx3DeviceInfo device,
            string imageFilePath,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Erase the device to blank bootloader state by programming a dedicated erase image
        /// (a valid FX3 boot image whose payload returns the device to USB boot).
        /// </summary>
        DeviceOperationResult EraseEeprom(
            Fx3DeviceInfo device,
            string eraseImageFilePath,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken);
    }
}
