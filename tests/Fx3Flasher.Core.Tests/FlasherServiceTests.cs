using Fx3Flasher.Core.Firmware;
using Fx3Flasher.Core.Models;
using Fx3Flasher.Core.Profiles;
using Fx3Flasher.Core.Safety;
using Fx3Flasher.Core.Services;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Fx3Flasher.Core.Tests
{
    public class FlasherServiceTests
    {
        private const string ProfilesJson = @"[
          {
            'name': 'Test',
            'bootloaderIds': [ { 'vendorId': 1204, 'productId': 243 } ],
            'applicationIds': [ { 'vendorId': 1204, 'productId': 244 } ],
            'eepromSizeBytes': 262144,
            'maxImageSizeBytes': 262144,
            'allowProgram': true,
            'allowErase': true,
            'requireChecksum': true
          }
        ]";

        private static BoardProfileStore Profiles()
        {
            var store = new BoardProfileStore();
            store.LoadFromJson(ProfilesJson.Replace('\'', '"'));
            return store;
        }

        private static FlasherService Service(BoardProfileStore profiles, FakeBackend backend)
        {
            return new FlasherService(
                backend, profiles, new EepromSafetyGate(), new FirmwareImageValidator(), new CapturingLogger());
        }

        private static Fx3DeviceInfo SupportedBlankDevice()
        {
            return new Fx3DeviceInfo
            {
                Index = 0,
                VendorId = 1204,
                ProductId = 243,
                SerialNumber = "SN1",
                FriendlyName = "FX3",
                DevicePath = "\\\\?\\usb#vid_04b4",
                State = DeviceState.BlankBootloader,
                ProfileName = "Test"
            };
        }

        private static string ValidImagePath()
        {
            byte[] data = Fx3ImageBuilder.BuildValid(0x40003000, 0x40003000, new uint[] { 0xAAAA5555, 0x0F0F0F0F });
            return Fx3ImageBuilder.WriteTemp(data);
        }

        [Fact]
        public void Program_Succeeds_AndVerifiesReenumeration()
        {
            var backend = new FakeBackend();
            var device = SupportedBlankDevice();
            backend.DevicesToReturn.Add(device);
            backend.AfterOperationDevices = new List<Fx3DeviceInfo>
            {
                new Fx3DeviceInfo
                {
                    Index = 0, VendorId = 1204, ProductId = 244, SerialNumber = "SN1",
                    State = DeviceState.Programmed, ProfileName = "Test"
                }
            };

            FlasherService service = Service(Profiles(), backend);
            FlasherOperationResult result = service.Program(
                device, ValidImagePath(), operatorConfirmed: true, progress: null, cancellationToken: CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Verified);
            Assert.Equal(1, backend.ProgramCalls);
        }

        [Fact]
        public void Program_Blocked_WhenNotConfirmed()
        {
            var backend = new FakeBackend();
            var device = SupportedBlankDevice();
            backend.DevicesToReturn.Add(device);

            FlasherService service = Service(Profiles(), backend);
            FlasherOperationResult result = service.Program(
                device, ValidImagePath(), operatorConfirmed: false, progress: null, cancellationToken: CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, backend.ProgramCalls);
        }

        [Fact]
        public void Program_Blocked_WhenDeviceUnsupported()
        {
            var backend = new FakeBackend();
            var device = new Fx3DeviceInfo
            {
                Index = 0, VendorId = 0x1234, ProductId = 0x5678, State = DeviceState.Unsupported
            };
            backend.DevicesToReturn.Add(device);

            FlasherService service = Service(Profiles(), backend);
            FlasherOperationResult result = service.Program(
                device, ValidImagePath(), operatorConfirmed: true, progress: null, cancellationToken: CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, backend.ProgramCalls);
        }

        [Fact]
        public void Erase_Fails_WhenNoEraseImageAvailable()
        {
            var backend = new FakeBackend();
            var device = SupportedBlankDevice();
            backend.DevicesToReturn.Add(device);

            FlasherService service = Service(Profiles(), backend);
            FlasherOperationResult result = service.Erase(
                device, eraseImageFilePath: null, operatorConfirmed: true, progress: null, cancellationToken: CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, backend.EraseCalls);
        }

        [Fact]
        public void Erase_Succeeds_AndVerifiesBlank()
        {
            var backend = new FakeBackend();
            var device = SupportedBlankDevice();
            device.State = DeviceState.Programmed;
            backend.DevicesToReturn.Add(device);
            backend.AfterOperationDevices = new List<Fx3DeviceInfo>
            {
                new Fx3DeviceInfo
                {
                    Index = 0, VendorId = 1204, ProductId = 243, SerialNumber = "SN1",
                    State = DeviceState.BlankBootloader, ProfileName = "Test"
                }
            };

            FlasherService service = Service(Profiles(), backend);
            FlasherOperationResult result = service.Erase(
                device, ValidImagePath(), operatorConfirmed: true, progress: null, cancellationToken: CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Verified);
            Assert.Equal(1, backend.EraseCalls);
        }
    }
}
