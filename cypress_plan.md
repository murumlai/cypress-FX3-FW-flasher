## Plan: Cypress FX3 EEPROM Flasher

Build a Windows desktop flasher as a C# WPF application on .NET focused on Cypress FX3 devices, especially CYUSB3014-based boards, with the primary objective of programming, erasing, and verifying persistent I2C EEPROM firmware on both blank and pre-programmed devices. The first implementation should prove the reliable FX3 EEPROM path before broadening the architecture. The existing CyUSB.dll in this folder should be treated as a high-priority candidate dependency because Cypress/Infineon-proven USB access is likely preferable for production EEPROM programming.

**Steps**
1. Phase 1: Validate the FX3 EEPROM programming path before UI work. Build a thin spike that can enumerate FX3 devices, read USB descriptors, distinguish blank bootloader devices from pre-programmed application devices, and prove a path to access I2C EEPROM programming commands. This is the main technical gate for the project.
2. Confirm how pre-programmed devices enter a programmable state. The plan must account for application firmware that may not expose the same endpoints as the blank FX3 bootloader. Supported paths may include an application-defined vendor command, a reset/re-enumeration command, a hardware boot-mode action, or a dedicated recovery/programming mode.
3. Define a board-profile model driven by external configuration so supported boards can be identified by a mix of VID/PID pairs, expected descriptors, bootloader signatures, application signatures, and optional serial or location data. Profiles should include EEPROM size, I2C address, page size, write delay, boot mode, reserved regions, allowed image IDs, allowed operations, maximum image size, expected image checksum or signature policy, and whether full-chip erase is permitted.
4. Phase 2: Create the WPF solution skeleton for a single desktop app: UI shell, application services, domain models, FX3 USB adapter, EEPROM programmer, image validation, operation pipeline, and structured logging. Keep the USB access layer isolated so CyUSB.dll or another proven transport can be swapped without changing the UI or workflow logic.
5. Implement the device inventory/orchestration layer. It should continuously discover FX3 devices, assign stable session indexes when multiple devices are attached, surface device state/VID/PID/serial/descriptors/location in the UI, and prevent unsafe actions when a device does not match a supported board profile.
6. Phase 3: Implement the FX3 EEPROM program flow for `.img` files produced by the internal tool. Validate the image before enabling programming, write the EEPROM using profile-specific geometry, read back the programmed contents, verify the image, reset or prompt for power cycle as needed, and confirm the device re-enumerates into the expected state.
7. Add a fail-closed EEPROM safety gate before any write or erase operation. The tool must refuse to touch EEPROM unless the connected device uniquely matches a supported board profile, the loaded image passes all structural and policy checks, the target address range is inside writable regions, protected regions are excluded, the selected erase/program mode is allowed by the profile, and the operator confirms destructive actions with the exact selected device index and board identity shown.
8. Implement erase as a profile-controlled operation. The safer default should be boot-image invalidation or clearing the boot header so the device returns to blank bootloader behavior. Full EEPROM erase should be a separate explicit mode and should protect reserved manufacturing, serial, calibration, or configuration regions unless the board profile allows overwriting them.
9. Phase 4: Build the production-oriented WPF operator workflow around the shared orchestration layer. Include device list with indexes, device details pane, file-load workflow, action buttons for refresh/erase/program/verify, progress reporting, cancellation, operation history, and exportable logs. Show explicit safety messaging for unsupported boards, missing EEPROM support, invalid images, ambiguous device state, protected-region conflicts, destructive-operation confirmation, and re-enumeration requirements.
10. Add a recovery-focused job pipeline with serialized per-device operations, timeouts, safe retry boundaries, clear state transitions, and durable operation logs. Once an erase or program job starts, bind the job to the selected device path, serial, and physical location when available, and abort if the device disappears or reappears ambiguously. Do not auto-retry EEPROM writes after partial failure unless the programmer can prove the current EEPROM state by read-back and the board profile declares the retry safe.
11. Phase 5: Verify end-to-end with real FX3 devices. Cover blank and pre-programmed devices; multi-device indexing; erase-to-blank; program-and-verify; read-back mismatch handling; unplug/replug/re-enumeration; unsupported board rejection; invalid-image rejection; protected-region rejection; full-erase lockout; preserved-region behavior; partial-write failure recovery; and operator log export.

**Relevant files**
- c:\Users\lloganat\source\repos\Cypress\CyUSB.dll - existing Cypress USB library and a strong candidate for the first FX3 USB transport spike.
- Planned solution root under c:\Users\lloganat\source\repos\Cypress for the WPF app, FX3 USB adapter, EEPROM programmer, image validation, board-profile config, logs, and installer/runtime packaging.

**Verification**
1. Run a low-level USB spike against at least one blank FX3 device and one pre-programmed FX3 device to prove discovery, descriptor reads, state classification, and the path into EEPROM programming mode.
2. Prove EEPROM write, read-back, verify, boot-header erase, and re-enumeration handling on real hardware before investing heavily in UI polish.
3. Add automated tests for `.img` validation, board-profile matching, checksum/signature policy, image-size limits, EEPROM geometry calculations, writable-range checks, preserved-region rules, full-erase permission checks, device-state classification, and operation-state transitions.
4. Add integration/manual test scripts for multi-device indexing, selected-device identity lock, wrong-image rejection, unsafe-range rejection, protected-region rejection, full-erase lockout, erase-to-blank, program-and-verify, read-back mismatch handling, partial-write failure recovery, re-enumeration after write, and log export.
5. Perform packaging validation on a clean Windows machine to confirm the chosen USB stack and CyUSB.dll deployment model are reliable for operator use.

**Decisions**
- Platform: C# WPF on .NET.
- Delivery shape: single desktop app only.
- Device scope: FX3 EEPROM programming and erase is the MVP objective.
- Boot storage scope: persistent I2C EEPROM programming is required.
- Firmware formats: FX3 should start with `.img` from the existing internal tool.
- Board identification: use a mix of VID/PID matching, descriptor inspection, bootloader/application signatures, and external supported-device configuration.
- UX depth: production-ready operator tool with progress, logs, safeguards, verify, and exportable history.
- EEPROM safety policy: all write and erase actions must fail closed unless the board, image, address range, erase mode, and operator confirmation satisfy the active board profile.
- Dependency stance: prefer the most reliable FX3 EEPROM programming path, including CyUSB.dll or vendor-proven APIs if needed. Keep it behind an adapter so the transport can be replaced later.

**Further Considerations**
1. Key technical risk: pre-programmed devices may not expose a programmer-compatible interface. The project needs a confirmed recovery/programming-entry path for those devices before the UI can honestly promise erase/program support.
2. Key safety risk: blank vs flashed detection is only reliable when each supported board profile defines known bootloader and application signatures. The plan should include a profile-authoring step as soon as sample hardware identifiers are available.
3. Key erase risk: full EEPROM erase can destroy board-specific manufacturing data. Default erase should invalidate the boot image unless the board profile explicitly permits full erase.
4. Key image risk: an unsafe or mismatched image can leave the board unbootable. The image validator should reject unknown image layouts, images larger than the configured EEPROM, images targeting a different board/profile, and any image that would overwrite protected regions.
5. Key go/no-go gate: if the USB spike cannot reliably write and read back the FX3 I2C EEPROM, switch immediately to the most proven Cypress/Infineon-backed path rather than continuing with an unreliable transport.
6. Future scope: FX2 support can be added later as a separate backend after the FX3 EEPROM workflow is stable and validated on real boards.