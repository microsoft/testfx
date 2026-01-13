# Device Testing Implementation Plan for Microsoft.Testing.Platform

## Overview
This document outlines the implementation plan for adding device testing support to Microsoft.Testing.Platform, enabling `dotnet test` to work with iOS, Android, macOS Catalyst, and Windows device projects.

**Spec Reference:** https://github.com/dotnet/maui/pull/33117

## Goals
1. ✅ Unified CLI Experience: Enable `dotnet test` for device projects
2. ✅ MTP-First: Use Microsoft.Testing.Platform mode
3. ✅ Device Discovery: List and select target devices
4. ✅ Code Coverage on Devices: Collect coverage data from device tests
5. ✅ File-Based Artifacts: TRX and coverage files pulled from device sandbox (MVP)

## Architecture

### New Components

#### 1. Microsoft.Testing.Platform.Device (Core Abstractions)
**Location:** `src/Platform/Microsoft.Testing.Platform.Device/`

**Interfaces:**
```csharp
namespace Microsoft.Testing.Platform.Device;

public interface IDeviceManager
{
    Task<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(DeviceFilter filter, CancellationToken cancellationToken);
    Task<DeviceInfo?> SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken);
}

public interface IDeviceDeployer
{
    Task<DeploymentResult> DeployAsync(DeviceInfo device, DeploymentOptions options, CancellationToken cancellationToken);
    Task<bool> UninstallAsync(DeviceInfo device, string appId, CancellationToken cancellationToken);
}

public interface IDeviceTestRunner
{
    Task<TestRunResult> RunTestsAsync(DeviceInfo device, TestRunOptions options, CancellationToken cancellationToken);
}

public interface IArtifactCollector
{
    Task<ArtifactCollection> CollectArtifactsAsync(DeviceInfo device, string appId, CancellationToken cancellationToken);
}

public record DeviceInfo(string Id, string Name, DeviceType Type, PlatformType Platform, DeviceState State);
public enum DeviceType { Emulator, Simulator, Physical }
public enum PlatformType { Android, iOS, MacCatalyst, Windows }
public enum DeviceState { Online, Offline, Booting }
```

#### 2. Microsoft.Testing.Platform.Device.Android
**Location:** `src/Platform/Microsoft.Testing.Platform.Device.Android/`

**Implementation:**
- `AndroidDeviceManager` - Uses ADB to discover devices/emulators
- `AndroidDeviceDeployer` - Deploys APK using `adb install`
- `AndroidDeviceTestRunner` - Launches app with `am start`, monitors via logcat
- `AndroidArtifactCollector` - Pulls files from `/data/data/{package}/files/TestResults/` using `adb pull`

**Dependencies:**
- Wrap ADB commands via `Process`
- Parse `adb devices` output
- Handle logcat streaming for test output

#### 3. Microsoft.Testing.Platform.Device.iOS
**Location:** `src/Platform/Microsoft.Testing.Platform.Device.iOS/`

**Implementation:**
- `iOSDeviceManager` - Uses `xcrun simctl list` for simulators, `mlaunch` for physical devices
- `iOSDeviceDeployer` - Deploys app bundle using `xcrun simctl install` or `mlaunch --installdev`
- `iOSDeviceTestRunner` - Launches app via `xcrun simctl launch` or `mlaunch --launchdev`
- `iOSArtifactCollector` - Retrieves files from app sandbox container

**Dependencies:**
- Xcode command-line tools (simctl)
- mlaunch for physical devices
- macOS only

#### 4. MSBuild Integration Extensions
**Location:** `src/Platform/Microsoft.Testing.Platform.MSBuild/`

**New Files:**
- `Tasks/InvokeDeviceTestingPlatformTask.cs` - Orchestrates device testing
- `buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.Device.targets` - Device-specific targets

**MSBuild Properties:**
```xml
<PropertyGroup>
  <IsDeviceTestProject Condition="'$(TargetFramework.Contains('android'))' OR '$(TargetFramework.Contains('ios'))'">true</IsDeviceTestProject>
  <DeviceTestTargetPlatform Condition="'$(TargetFramework.Contains('android'))'">Android</DeviceTestTargetPlatform>
  <DeviceTestTargetPlatform Condition="'$(TargetFramework.Contains('ios'))'">iOS</DeviceTestTargetPlatform>
  <DeviceId><!-- Set via --device CLI flag or environment variable --></DeviceId>
</PropertyGroup>
```

**New Target:**
```xml
<Target Name="InvokeDeviceTestingPlatform" Condition="'$(IsDeviceTestProject)' == 'true'">
  <InvokeDeviceTestingPlatformTask 
    TargetPath="$(TargetPath)"
    DeviceTestTargetPlatform="$(DeviceTestTargetPlatform)"
    DeviceId="$(DeviceId)"
    TestArchitecture="$(_TestArchitecture)"
    ... />
</Target>
```

### Modified Components

#### Microsoft.Testing.Platform.MSBuild
**Changes to `InvokeTestingPlatformTask.cs`:**
- Detect if project is a device test project (via TFM containing `android`/`ios`)
- If device project, delegate to `InvokeDeviceTestingPlatformTask`
- Otherwise, continue with existing desktop test logic

**Changes to `buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets`:**
- Add device detection logic
- Import device-specific targets when applicable
- Pass device-related properties through MSBuild

## Implementation Phases

### Phase 1: Core Abstractions ✅ (Current Phase)
**Tasks:**
- [x] Create implementation plan
- [ ] Create `Microsoft.Testing.Platform.Device` project
- [ ] Define core interfaces (`IDeviceManager`, `IDeviceDeployer`, etc.)
- [ ] Define data models (`DeviceInfo`, `TestRunResult`, etc.)
- [ ] Add unit tests for abstractions

**Files to Create:**
- `src/Platform/Microsoft.Testing.Platform.Device/Microsoft.Testing.Platform.Device.csproj`
- `src/Platform/Microsoft.Testing.Platform.Device/IDeviceManager.cs`
- `src/Platform/Microsoft.Testing.Platform.Device/IDeviceDeployer.cs`
- `src/Platform/Microsoft.Testing.Platform.Device/IDeviceTestRunner.cs`
- `src/Platform/Microsoft.Testing.Platform.Device/IArtifactCollector.cs`
- `src/Platform/Microsoft.Testing.Platform.Device/Models/*.cs`

**Deliverable:** Core device testing abstractions compiled and documented

---

### Phase 2: Android Implementation
**Tasks:**
- [ ] Create `Microsoft.Testing.Platform.Device.Android` project
- [ ] Implement `AndroidDeviceManager` (ADB device discovery)
- [ ] Implement `AndroidDeviceDeployer` (APK installation)
- [ ] Implement `AndroidDeviceTestRunner` (app launch & monitoring)
- [ ] Implement `AndroidArtifactCollector` (adb pull results)
- [ ] Add integration tests with Android emulator

**Dependencies:**
- ADB must be available in PATH or via `ANDROID_HOME`
- Android SDK installed

**Files to Create:**
- `src/Platform/Microsoft.Testing.Platform.Device.Android/Microsoft.Testing.Platform.Device.Android.csproj`
- `src/Platform/Microsoft.Testing.Platform.Device.Android/AndroidDeviceManager.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.Android/AndroidDeviceDeployer.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.Android/AndroidDeviceTestRunner.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.Android/AndroidArtifactCollector.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.Android/AdbClient.cs` (ADB wrapper)

**Deliverable:** Working Android device testing on emulators

---

### Phase 3: iOS Implementation (macOS Only)
**Tasks:**
- [ ] Create `Microsoft.Testing.Platform.Device.iOS` project
- [ ] Implement `iOSDeviceManager` (simctl/mlaunch device discovery)
- [ ] Implement `iOSDeviceDeployer` (app installation)
- [ ] Implement `iOSDeviceTestRunner` (app launch & monitoring)
- [ ] Implement `iOSArtifactCollector` (sandbox file retrieval)
- [ ] Add integration tests with iOS simulator

**Dependencies:**
- Xcode with command-line tools
- macOS only (cannot test on Windows)

**Files to Create:**
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/Microsoft.Testing.Platform.Device.iOS.csproj`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/iOSDeviceManager.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/iOSDeviceDeployer.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/iOSDeviceTestRunner.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/iOSArtifactCollector.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/SimCtlClient.cs`
- `src/Platform/Microsoft.Testing.Platform.Device.iOS/MLaunchClient.cs`

**Deliverable:** Working iOS device testing on simulators

---

### Phase 4: MSBuild Integration
**Tasks:**
- [ ] Create `InvokeDeviceTestingPlatformTask.cs`
- [ ] Modify `InvokeTestingPlatformTask.cs` to detect device projects
- [ ] Create device-specific MSBuild targets
- [ ] Add device property validation
- [ ] Pass `--device` CLI argument through MSBuild
- [ ] Test with multi-targeting projects

**Files to Modify:**
- `src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/InvokeTestingPlatformTask.cs`

**Files to Create:**
- `src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/InvokeDeviceTestingPlatformTask.cs`
- `src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.Device.targets`

**Deliverable:** `dotnet test` working with device projects

---

### Phase 5: Sample Projects & E2E Tests
**Tasks:**
- [ ] Convert `samples/public/BlankAndroid` to MSTest device test project
- [ ] Convert `samples/public/BlankiOS` to MSTest device test project
- [ ] Add sample tests that exercise device APIs (sensors, filesystem, etc.)
- [ ] Create E2E test suite that:
  - Discovers devices
  - Deploys test app
  - Runs tests
  - Collects results
  - Validates TRX output
- [ ] Add documentation for device testing

**Files to Modify:**
- `samples/public/BlankAndroid/BlankAndroid.csproj` - Add MSTest.Sdk
- `samples/public/BlankAndroid/MainActivity.cs` - Add test framework initialization
- `samples/public/BlankiOS/BlankiOS.csproj` - Add MSTest.Sdk
- `samples/public/BlankiOS/AppDelegate.cs` - Add test framework initialization

**Files to Create:**
- `samples/public/BlankAndroid/DeviceTests.cs`
- `samples/public/BlankiOS/DeviceTests.cs`
- `docs/device-testing-guide.md`

**Deliverable:** End-to-end device testing working with real samples

---

### Phase 6: CLI Enhancements
**Tasks:**
- [ ] Implement `--list-devices` flag
- [ ] Implement `--device <name>` flag
- [ ] Add device selection prompt (interactive mode)
- [ ] Add proper error messages for missing SDKs
- [ ] Add logging for device operations
- [ ] Test on CI/CD pipelines

**Deliverable:** Full CLI experience matching spec

---

## Testing Strategy

### Unit Tests
- Test device managers with mocked ADB/simctl responses
- Test artifact collectors with sample file structures
- Test MSBuild task logic

### Integration Tests
- Require actual emulators/simulators running
- Test full deployment → execution → collection flow
- Run in CI only when device infrastructure available

### E2E Tests
- Use sample projects as E2E test cases
- Validate complete `dotnet test` workflow
- Check generated TRX files and coverage

## Success Criteria
- [ ] `dotnet test -f net10.0-android` discovers and runs tests on Android emulator
- [ ] `dotnet test -f net10.0-ios` discovers and runs tests on iOS simulator
- [ ] `dotnet test --list-devices` shows available devices
- [ ] `dotnet test --device "Pixel 7"` runs on specific device
- [ ] `dotnet test --collect "Code Coverage"` retrieves coverage from device
- [ ] TRX files correctly generated from device test results
- [ ] Works with multi-targeting projects
- [ ] All existing tests still pass

## Known Limitations (MVP)
- ✅ File-based artifact collection only (no live streaming)
- ✅ No automatic emulator boot-up (must be pre-started)
- ✅ iOS requires macOS (cannot test on Windows)
- ✅ Physical device testing may require additional setup
- ❌ No Test Explorer integration (future phase)
- ❌ No parallel device execution (future phase)

## Current Status
**Phase:** 1 - Core Abstractions
**Next Steps:** Create device abstraction projects and define interfaces

## Questions & Decisions Needed
1. Should device test projects use `MSTest.Sdk` or regular MSTest packages?
   - **Decision:** Use MSTest.Sdk for simplified configuration
2. How to handle device boot-up? Prompt user or auto-start?
   - **Decision:** Prompt user, don't auto-start (matches `dotnet run` behavior per spec)
3. Should we create a unified device package or separate per-platform?
   - **Decision:** Separate per-platform to avoid unnecessary dependencies
4. How to handle test timeouts on devices?
   - **Decision:** Use existing `--test-timeout` mechanisms, default 30 minutes

## Resources
- Spec: https://github.com/dotnet/maui/pull/33117
- MTP Docs: https://aka.ms/mtp-overview
- Android SDK: https://github.com/dotnet/android
- iOS SDK: https://github.com/dotnet/ios
