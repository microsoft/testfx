# Device Testing Implementation Status

## Goal
Make `dotnet test` work exactly like `dotnet run` for device projects:

```bash
# Current dotnet run (works in .NET 11):
dotnet run --project MyTests.csproj -f net10.0-android --device emulator-5554

# Goal for dotnet test:
dotnet test --project MyTests.csproj -f net10.0-android --device emulator-5554
```

## Current Status: ✅ Working with Two Modes

The implementation supports **two modes** for running tests on devices:

### Mode 1: Activity Mode (Default) - via `dotnet run --device`

Uses `dotnet run --device` to deploy and launch the app's MainActivity.

```bash
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11
```

### Mode 2: Instrumentation Mode - via `adb instrument`

Uses Android Instrumentation for more reliable test execution with proper wait-for-completion.

```bash
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11 \
  -p:UseInstrumentation=true
```

### Test Output
```
# ✓ Passed:  SimpleTest_ShouldPass
# ✓ Passed:  AndroidPlatformTest
# ✓ Passed:  StringTest_ShouldPass
# ✓ Passed:  LongRunningTest_30Seconds
#
# Test run summary: Passed!
#   total: 4
#   failed: 0
#   succeeded: 4
#   skipped: 0
#   duration: 30s 282ms
```

## What Works ✅

| Feature | Status | Implementation |
|---------|--------|----------------|
| Build device test project | ✅ | Standard MSBuild |
| Deploy to device/emulator | ✅ | Via `dotnet run --device` or `dotnet build -t:Install` |
| Execute tests on device | ✅ | MainActivity (Activity mode) or TestInstrumentation (Instrumentation mode) |
| **Long-running tests** | ✅ | App runs until tests complete, then exits |
| Test results to logcat | ✅ | `IDataConsumer` MTP extension |
| Session start/end events | ✅ | `ITestSessionLifetimeHandler` |
| Pass/Fail/Error output | ✅ | Streamed via logcat |
| Exit code propagation | ✅ | Via `Java.Lang.JavaSystem.Exit()` or `Instrumentation.Finish()` |
| **TRX file collection** | ✅ | `adb shell run-as ... cat` |
| **Logcat collection** | ✅ | `adb logcat -d` saved to TestResults |

## What's Missing ❌

| Feature | Status | Blocker |
|---------|--------|---------|
| `--device` CLI argument | ❌ | Needs SDK change to `dotnet test` |
| `--project` CLI argument | ❌ | Needs SDK change to `dotnet test` |
| `--list-devices` argument | ❌ | Needs SDK change (already in `dotnet run`) |

## Architecture

### MSBuild Integration

Device testing targets are designed to be split across SDKs:

1. **Platform-specific targets** (Android/iOS) - will live in the respective SDK repos (dotnet/android, dotnet/maui)
2. **Common MTP targets** - remain in `Microsoft.Testing.Platform.MSBuild` package

Currently for development, the Android targets are in `samples/public/BlankAndroid/Sdk.DeviceTesting.Android.targets`.

```
samples/public/BlankAndroid/
├── Sdk.DeviceTesting.Android.targets     # Android device testing targets (→ dotnet/android SDK)
├── Directory.Build.targets               # Local imports for development
├── BlankAndroid.csproj                   # Sample test project
└── ...
```

When a project targets `net*-android` and has `IsTestProject=true`, these targets will be automatically imported by the Android SDK.

### MSBuild Properties

| Property | Description | Default |
|----------|-------------|---------|
| `DeviceId` | Device/emulator ID (e.g., `emulator-5554`) | `$(DEVICE_ID)` env var |
| `DotnetDevicePath` | Path to .NET 11+ SDK with device support | `$(DOTNET_HOST_PATH)` or `dotnet` |
| `UseInstrumentation` | Use Android Instrumentation mode | `false` |
| `AndroidInstrumentationName` | Instrumentation class name | `$(RootNamespace.ToLower()).TestInstrumentation` |

### Activity Mode (Default)

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Microsoft.Testing.Platform.MSBuild.DeviceTesting.targets │
│  - Detects device TFM (net10.0-android)                    │
│  - Sets UseMSBuildTestInfrastructure=true                  │
│  - Overrides VSTest target                                  │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _RunAndroidTestsViaDotnetRun Target                        │
│  - Executes: dotnet run --project <proj> -f <tfm> --device  │
│  - dotnet run handles: build, deploy, run, logcat streaming │
│  - Waits for app to exit                                    │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MainActivity.cs (App Entry Point)                          │
│  - OnCreate → MicrosoftTestingPlatformEntryPoint.Main()    │
│  - Runs all tests                                           │
│  - Java.Lang.JavaSystem.Exit(exitCode)                      │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MTP Extensions (DeviceTestReporter.cs)                    │
│  - IDataConsumer: Logs test results to logcat              │
│  - ITestSessionLifetimeHandler: Session events             │
│  - IOutputDeviceDataProducer: Formatted output             │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _CollectAndroidTestResults Target                          │
│  - adb shell run-as ... ls -t files/TestResults/           │
│  - adb shell run-as ... cat <latest.trx>                   │
│  - adb logcat -d > <ProjectName>_logcat.txt                │
│  - Saves to bin/Debug/net10.0-android/TestResults/         │
└─────────────────────────────────────────────────────────────┘
```

### Instrumentation Mode (`-p:UseInstrumentation=true`)

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554 -p:UseInstrumentation=true
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Microsoft.Testing.Platform.MSBuild.DeviceTesting.targets │
│  - Detects device TFM (net10.0-android)                    │
│  - UseInstrumentation=true → delegates to adb instrument   │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _RunAndroidTestsViaInstrumentation Target                  │
│  1. dotnet build -t:Install (builds & deploys APK)         │
│  2. adb shell am instrument -w <instrumentation-class>     │
│     -w flag waits for instrumentation to finish            │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  TestInstrumentation.cs (Instrumentation Entry Point)       │
│  - OnCreate → Start()                                       │
│  - OnStart → MicrosoftTestingPlatformEntryPoint.Main()     │
│  - Runs all tests                                           │
│  - Finish(exitCode, results) signals completion            │
└─────────────────────────────────────────────────────────────┘
```

## Key Files

### Android Device Testing (→ dotnet/android SDK)

| File | Purpose | Future Location |
|------|---------|-----------------|
| `samples/public/BlankAndroid/Sdk.DeviceTesting.Android.targets` | All Android device testing MSBuild logic | `dotnet/android` SDK |

### Sample Project: samples/public/BlankAndroid/

| File | Purpose |
|------|---------|
| `BlankAndroid.csproj` | Simple test project with `IsTestProject=true` |
| `Sdk.DeviceTesting.Android.targets` | Android device testing targets (to be moved to dotnet/android SDK) |
| `Directory.Build.targets` | Local dev import of Sdk.DeviceTesting.Android.targets |
| `MainActivity.cs` | Activity mode entry point |
| `TestInstrumentation.cs` | Instrumentation mode entry point |
| `DeviceTestReporter.cs` | MTP extensions for logcat output |
| `DeviceTests.cs` | Sample MSTest tests |

## Creating a Device Test Project

### Minimal Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
    <ApplicationId>com.example.MyTests</ApplicationId>
    
    <!-- Mark as test project - enables all testing infrastructure -->
    <IsTestProject>true</IsTestProject>
    
    <!-- Enable Microsoft Testing Platform -->
    <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>true</GenerateTestingPlatformEntryPoint>
    
    <!-- Use MSTest Engine for device testing -->
    <UseMSTestEngine>true</UseMSTestEngine>
    <UseMSTestAdapter>false</UseMSTestAdapter>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="..." />
    <PackageReference Include="Microsoft.Testing.Platform" Version="..." />
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="..." />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="..." />
    <PackageReference Include="MSTest.Engine" Version="..." />
    <PackageReference Include="MSTest.SourceGeneration" Version="..." />
  </ItemGroup>
</Project>
```

### Required App Components

1. **MainActivity.cs** - Entry point for Activity mode
2. **TestInstrumentation.cs** - Entry point for Instrumentation mode (optional)
3. **DeviceTestReporter.cs** - MTP extensions for logcat output (optional but recommended)

## Usage

### Activity Mode (Default)
```bash
# With MSBuild properties
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11

# With environment variables
export DEVICE_ID=emulator-5554
export DOTNET_DEVICE_PATH=/path/to/dotnet11
dotnet test BlankAndroid.csproj -f net10.0-android
```

### Instrumentation Mode
```bash
# Enables more reliable test completion detection
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11 \
  -p:UseInstrumentation=true
```

### Goal (CLI arguments - requires SDK changes)
```bash
dotnet test --project BlankAndroid.csproj -f net10.0-android --device emulator-5554
```

## Path to Success

### ✅ Phase 1: COMPLETE - Working Prototype
- [x] MSBuild targets in Microsoft.Testing.Platform.MSBuild package
- [x] Auto-detection of device TFMs (android/ios)
- [x] Activity mode via `dotnet run --device`
- [x] Instrumentation mode via `adb instrument`
- [x] MTP test execution on device
- [x] Test result reporting via logcat
- [x] TRX file collection from device
- [x] Logcat collection for debugging

### 🔄 Phase 2: IN PROGRESS - CLI Parity with `dotnet run`

**Required:** Add `--device` and `--project` flags to `dotnet test` CLI

```bash
# dotnet run (works today in .NET 11):
dotnet run --project X.csproj -f net10.0-android --device emulator-5554

# dotnet test (goal):
dotnet test --project X.csproj -f net10.0-android --device emulator-5554
```

### 📋 Phase 3: Future Enhancements
- [ ] `--list-devices` support (already in `dotnet run`)
- [ ] iOS support (same pattern with test host app)
- [ ] Code coverage collection from device

## TRX and Logcat Collection

### Output Files
After test execution:
```
bin/Debug/net10.0-android/TestResults/
├── BlankAndroid.trx           # Standard TRX test results
└── BlankAndroid_logcat.txt    # Full device logcat for debugging
```

### Collection Method
- **TRX:** `adb shell run-as <app-id> cat files/TestResults/<file.trx>`
- **Logcat:** `adb logcat -d > TestResults/<ProjectName>_logcat.txt`

## References

- [MAUI Device Testing Spec](https://github.com/dotnet/maui/pull/33117)
- [Microsoft.Testing.Platform](https://aka.ms/mtp-overview)
- [dotnet run --device (.NET 11)](https://github.com/dotnet/sdk)
- [Android Instrumentation](https://developer.android.com/reference/android/app/Instrumentation)

---
**Last Updated:** 2026-01-13  
**Status:** ✅ Working prototype with Android device testing targets ready for migration to dotnet/android SDK
