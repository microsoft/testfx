# Device Testing Implementation Status

## Goal
Make `dotnet test` work exactly like `dotnet run` for device projects:

```bash
# Current dotnet run (works in .NET 11):
dotnet run --project MyTests.csproj -f net10.0-android --device emulator-5554

# Goal for dotnet test:
dotnet test --project MyTests.csproj -f net10.0-android --device emulator-5554
```

## Current Status: ✅ Working with MSBuild Properties

```bash
# This works TODAY:
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11

# Output (with 30-second long-running test):
# MTP.TestSession: ║  Started: 2026-01-13 19:04:20  ║
# MTP.TestResults: ▶ Running: SimpleTest_ShouldPass
# MTP.TestResults: ✓ Passed:  SimpleTest_ShouldPass
# MTP.TestResults: ✓ Passed:  AndroidPlatformTest
# MTP.TestResults: ✓ Passed:  StringTest_ShouldPass
# MTP.TestResults: ▶ Running: LongRunningTest_30Seconds
# ... (waits 30 seconds) ...
# MTP.TestResults: ✓ Passed:  LongRunningTest_30Seconds
# MTP.TestSession:   Test Run Completed - Duration: 30.13s
# Collecting test results from device...
# Test results: bin/Debug/net10.0-android/TestResults/BlankAndroid.trx
# ✓ Tests completed with exit code: 0
```

## What Works ✅

| Feature | Status | Implementation |
|---------|--------|----------------|
| Build device test project | ✅ | Standard MSBuild |
| Deploy to device/emulator | ✅ | Via `dotnet run --device` |
| Execute tests on device | ✅ | Microsoft.Testing.Platform |
| **Long-running tests** | ✅ | Tests wait for completion (tested with 30s test) |
| Test results to console | ✅ | `IDataConsumer` extension |
| Session start/end events | ✅ | `ITestSessionLifetimeHandler` |
| Pass/Fail/Error output | ✅ | Logcat → Console filtering |
| Exit code propagation | ✅ | Non-zero on failures |
| **TRX file collection** | ✅ | `adb shell run-as ... cat` |

## What's Missing ❌

| Feature | Status | Blocker |
|---------|--------|---------|
| `--device` CLI argument | ❌ | Needs SDK change |
| `--project` CLI argument | ❌ | Needs SDK change |
| `--list-devices` argument | ❌ | Needs SDK change |

## Architecture

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Directory.Build.targets                           │
│  - Detects device TFM (net10.0-android)                    │
│  - Overrides VSTest target                                  │
│  - Calls: dotnet run --project X --device Y                │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  dotnet run --device (SDK .NET 11)                         │
│  - Builds APK                                               │
│  - Deploys to device via ADB                               │
│  - Launches app                                             │
│  - Streams logcat output                                    │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  App on Device (MainActivity.cs)                           │
│  - Calls MicrosoftTestingPlatformEntryPoint.Main()         │
│  - MTP discovers and runs tests                            │
│  - TRX file generated via --report-trx                     │
│  - Exits with test result code                             │
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
│  _CollectDeviceTestResults Target                          │
│  - adb shell run-as ... ls -t files/TestResults/           │
│  - adb shell run-as ... cat <latest.trx>                   │
│  - Saves to bin/Debug/net10.0-android/TestResults/         │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  Output                                                     │
│  - Console: MTP.TestResults: ✓ Passed: TestName            │
│  - TRX: bin/.../TestResults/BlankAndroid.trx               │
│  - Exit code: 0 (success) or non-zero (failures)           │
└─────────────────────────────────────────────────────────────┘
```

## Key Files

### samples/public/BlankAndroid/

| File | Purpose |
|------|---------|
| `BlankAndroid.csproj` | Project with MTP + TRX configuration |
| `Directory.Build.targets` | MSBuild targets for device test + TRX collection |
| `MainActivity.cs` | Entry point with `--report-trx` |
| `DeviceTestReporter.cs` | MTP extensions for test output |
| `DeviceTests.cs` | Sample MSTest tests |

## Path to Success

### ✅ Phase 1: COMPLETE - Working Prototype
- [x] MSBuild targets intercept `dotnet test` for device projects
- [x] Invoke `dotnet run --device` for deployment and execution
- [x] MTP extensions report test results via logcat
- [x] Console output shows pass/fail status
- [x] Exit code propagates correctly
- [x] **TRX file collection from device**

### 🔄 Phase 2: IN PROGRESS - CLI Parity with `dotnet run`

**Required:** Add `--device` flag to `dotnet test` CLI

The .NET SDK already supports `--device` for `dotnet run`. We need the same for `dotnet test`:

```bash
# dotnet run (works today):
dotnet run --project X.csproj -f net10.0-android --device emulator-5554

# dotnet test (goal):
dotnet test --project X.csproj -f net10.0-android --device emulator-5554
```

**Implementation options:**
1. **SDK Change:** Add `--device` parsing to `dotnet test` command
2. **MSBuild Pass-through:** SDK passes `--device` as MSBuild property

### 📋 Phase 3: Future Enhancements
- [ ] `--list-devices` support (provided by SDK)
- [ ] Code coverage collection from device
- [ ] iOS support (same pattern)

## Usage

### Current (with MSBuild properties)
```bash
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11
```

### With Environment Variables
```bash
export DEVICE_ID=emulator-5554
export DOTNET_DEVICE_PATH=/path/to/dotnet11
dotnet test BlankAndroid.csproj -f net10.0-android
```

### Goal (CLI arguments)
```bash
dotnet test --project BlankAndroid.csproj -f net10.0-android --device emulator-5554
```

## TRX Collection Details

The TRX file is collected using:
1. `adb shell run-as <app-id> ls -t files/TestResults/` - Get latest TRX filename
2. `adb shell run-as <app-id> cat files/TestResults/<file.trx>` - Read file content
3. Save to `bin/Debug/net10.0-android/TestResults/<ProjectName>.trx`

This works because:
- `run-as` allows accessing app's private storage without root
- `cat` outputs file content to stdout which can be redirected locally
- Works with debuggable APKs (debug builds)

## References

- [MAUI Device Testing Spec](https://github.com/dotnet/maui/pull/33117)
- [Microsoft.Testing.Platform](https://aka.ms/mtp-overview)
- [dotnet run --device (.NET 11)](https://github.com/dotnet/sdk)

---
**Last Updated:** 2026-01-13  
**Status:** Working prototype with TRX collection, awaiting SDK CLI integration
