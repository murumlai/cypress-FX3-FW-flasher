# Cypress FX3 EEPROM Flasher

A Windows desktop tool for programming, erasing and verifying firmware on the I2C EEPROM of
Cypress/Infineon **FX3** devices (e.g. CYUSB3014 boards), built on the Cypress `CyUSB.dll`
managed library.

## Features

- Detects connected FX3 devices and assigns each a stable index (supports multiple devices).
- Classifies each device as **blank bootloader** or **programmed** (via the FX3 bootloader state).
- Programs a firmware `.img` to the I2C EEPROM.
- Erases a device back to blank bootloader by writing a dedicated erase image.
- Verifies operations by re-enumeration (programmed / blank state after the operation).
- Includes bench diagnostics: RAM download test and staged I2C EEPROM probe.
- Fail-closed safety gate: no write or erase proceeds unless the device matches a supported
  board profile, the image passes validation, the operation is permitted, and the operator
  confirms the exact device.
- Color-coded operation log with clear/export.

## Requirements

- Windows with **.NET Framework 4.7.2** runtime (standard on Windows 10/11).
- Cypress **CyUSB3** driver bound to the FX3 device.
- The app runs as a **32-bit** process — `CyUSB.dll` only works in x86.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/Fx3Flasher.Core` | Domain models, `.img` validator, safety gate, orchestration, logging (transport-agnostic). |
| `src/Fx3Flasher.Cypress` | FX3 backend over `CyUSB.dll`: enumerate, detect, program, erase. |
| `src/Fx3Flasher.App` | WPF operator UI (MVVM). |
| `tests/Fx3Flasher.Core.Tests` | xUnit tests for validator, safety gate, profiles and orchestration. |
| `config/supported-boards.json` | External board profiles (identities, EEPROM geometry, policy). |
| `CyUSB.dll` | Cypress managed USB library (referenced by the backend). |

## Build and run

```powershell
# Build everything (Release)
dotnet build Fx3Flasher.slnx -c Release

# Run the app
src\Fx3Flasher.App\bin\Release\net472\Fx3Flasher.App.exe

# Run tests
dotnet test
```

The solution targets `net472` and builds with the .NET SDK (`dotnet build`); no Visual Studio
required. The WPF app is pinned to `PlatformTarget=x86`.

## Board profiles

Supported devices are described in `config/supported-boards.json`, copied next to the executable
on build. A profile identifies a board by USB VID/PID and declares EEPROM geometry and policy:

```json
{
  "name": "CYUSB3014 Reference Board",
  "bootloaderIds": [ { "vendorId": 1204, "productId": 243 } ],
  "applicationIds": [],
  "eepromSizeBytes": 131072,
  "i2cAddress": 80,
  "pageSizeBytes": 256,
  "writeDelayMs": 5,
  "maxImageSizeBytes": 131072,
  "allowProgram": true,
  "allowErase": true,
  "allowFullErase": false,
  "requireChecksum": true,
  "eraseImagePath": null
}
```

- `bootloaderIds` / `applicationIds` — identities seen when blank vs programmed. VID/PID are
  decimal (`1204` = `0x04B4`, `243` = `0x00F3`).
- `eepromSizeBytes`, `pageSizeBytes`, `i2cAddress` — EEPROM geometry (declared, not read from
  the chip). The reference board uses a Microchip **AT24CM01** (128 KB, 256-byte page, 0x50).
- `allowFullErase` — must be explicitly enabled to permit destructive full-chip erase.
- `eraseImagePath` — default erase image used by the Erase action.

## Firmware images

Program images must be Cypress FX3 boot images (`.img`) starting with the `CY` signature. The
validator checks the signature, section structure, trailing checksum, and size against the
board profile before any write is allowed.

For I2C EEPROM programming, Cypress `CyUSB.dll` derives the EEPROM size from `bImageCTL`
(image byte 2), specifically `sizeCode = (bImageCTL >> 1) & 0x07`. Size code `6` is the
64 KB path; size code `7` enables the Microchip 128 KB dual-bank behavior used by AT24CM01.
If a valid image targets a 128 KB profile but carries the 64 KB code, the app prepares a
temporary copy with the size bits patched to code `7` before calling `DownloadFw(I2CE2PROM)`.
The original firmware file is never modified.

Known APM image observations from bench work:

- `APM.APM.3.10.C.img` is a valid FX3 `CY` image and passes validation, but its original
  image-control byte encoded the 64 KB path. The app now patches the temporary programming
  copy for the configured AT24CM01 profile.
- `APM.APM.fakeff.img` is raw `0xFF` data, not a Cypress boot image. `DownloadFw` cannot write
  it directly because it has no `CY` signature; using it as a raw erase file would require a
  separate raw-I2C writer/protocol.

## Diagnostics

The UI exposes two bench diagnostics:

- **Test (RAM)** downloads the selected `.img` to FX3 RAM only. This is non-persistent and
  verifies the USB driver, device handle, and image path independently of the EEPROM.
- **Detect EEPROM** sends a staged first-bank I2C EEPROM probe and reports where the FX3/CyUSB
  transfer fails. This is not read-only: it writes a small probe image and asks for confirmation.

Current diagnosis notes for the AT24CM01 board:

- RAM download succeeds, so the USB path and firmware image are usable.
- EEPROM programming/probing currently fails on the first-bank 4 KB write with `BytesWritten=0`.
  That happens before the 64 KB bank boundary, so the remaining likely causes are hardware or
  bootloader I2C access: write-protect asserted, address strapping mismatch, SDA/SCL/pull-up or
  voltage issue, bus contention, or a faulty EEPROM.
- For AT24CM01, confirm `WP` is low, the device acknowledges the expected boot address path
  (`0x50`/`0x51` for the 128 KB dual-bank part), and SDA/SCL activity is present while running
  Detect EEPROM.

## Erase and verification notes

`CyUSB.dll` exposes no native EEPROM erase or byte-level read-back. Therefore:

- **Erase** is performed by programming a dedicated erase `.img` that returns the board to blank
  bootloader (configured per board via `eraseImagePath` or selected in the UI).
- **Verify** confirms the device re-enumerates into the expected state (programmed after a
  program, blank bootloader after an erase) rather than reading EEPROM bytes back.

## Safety model

Every write/erase is gated fail-closed. The operation is blocked unless all hold: a unique
supported-profile match, a valid image (for programming), the operation permitted by the
profile, an unambiguous device state, and explicit operator confirmation showing the device
index and identity. Full-chip erase is locked out unless the profile opts in.
