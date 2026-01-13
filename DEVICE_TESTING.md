# Device Testing Implementation Status

## Goal
Make `dotnet test` work exactly like `dotnet run` for device projects:

```bash
# Current dotnet run (works in .NET 11):
dotnet run --project MyTests.csproj -f net10.0-android --device emulator-5554

# Goal for dotnet test:
dotnet test --project MyTests.csproj -f net10.0-android --device emulator-5554
```

## Current Status: ✅ Working with Android Instrumentation

```bash
# This works TODAY:
dotnet test BlankAndroid.csproj -f net10.0-android \
  -p:DeviceId=emulator-5554 \
  -p:DotnetDevicePath=/path/to/dotnet11

# Output (with 30-second long-running test):
# ╔══════════════════════════════════════════════════════════════╗
# ║               DEVICE TESTING (Microsoft.Testing.Platform)    ║
# ╠══════════════════════════════════════════════════════════════╣
# ║  Project:    BlankAndroid
# ║  Framework:  net10.0-android
# ║  Device:     emulator-5554
# ╚══════════════════════════════════════════════════════════════╝
# 
# Running tests via Android Instrumentation...
# Instrumentation: com.companyname.BlankAndroid/blankandroid.TestInstrumentation
# 
# INSTRUMENTATION_RESULT: exitCode=0
# INSTRUMENTATION_RESULT: status=SUCCESS
# INSTRUMENTATION_RESULT: testResultsDir=/data/user/0/com.companyname.BlankAndroid/files/TestResults
# INSTRUMENTATION_CODE: -1
# 
# Test results: bin/Debug/net10.0-android/TestResults/BlankAndroid.trx
# ✓ Tests completed successfully
```

## What Works ✅

| Feature | Status | Implementation |
|---------|--------|----------------|
| Build device test project | ✅ | Standard MSBuild |
| Deploy to device/emulator | ✅ | `adb install` |
| Execute tests on device | ✅ | Android Instrumentation + MTP |
| **Long-running tests** | ✅ | Instrumentation waits for completion (tested with 30s test) |
| Test results to logcat | ✅ | `IDataConsumer` extension |
| Session start/end events | ✅ | `ITestSessionLifetimeHandler` |
| Pass/Fail/Error output | ✅ | Logcat filtering |
| Exit code propagation | ✅ | Via `Instrumentation.Finish()` |
| **TRX file collection** | ✅ | `adb shell run-as ... cat` |
| **Logcat collection** | ✅ | `adb logcat -d` saved to TestResults |

## What's Missing ❌

| Feature | Status | Blocker |
|---------|--------|---------|
| `--device` CLI argument | ❌ | Needs SDK change |
| `--project` CLI argument | ❌ | Needs SDK change |
| `--list-devices` argument | ❌ | Needs SDK change (provided by SDK) |

## Architecture

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Directory.Build.targets                           │
│  - Detects device TFM (net10.0-android)                    │
│  - Overrides VSTest target                                  │
│  - Build → Deploy → Instrument                              │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _DeployToDevice Target                                     │
│  - adb install -r <apk>                                     │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _RunTestsOnDevice Target                                   │
│  - adb shell am instrument -w                               │
│    com.companyname.BlankAndroid/blankandroid.TestInstrumentation │
│  - Waits for tests to complete                              │
│  - Returns INSTRUMENTATION_CODE: -1 (success) or 0 (crash)  │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  TestInstrumentation.cs (Android Instrumentation)           │
│  - OnCreate → Start()                                       │
│  - OnStart → MicrosoftTestingPlatformEntryPoint.Main()     │
│  - Finish(Result.Ok/Canceled, results)                      │
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
│  - Console: INSTRUMENTATION_RESULT: status=SUCCESS          │
│  - Logcat:  MTP.TestResults: ✓ Passed: TestName            │
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
| `TestInstrumentation.cs` | Android Instrumentation for test execution |
| `MainActivity.cs` | App entry point (minimal, tests run via Instrumentation) |
| `DeviceTestReporter.cs` | MTP extensions for test output |
| `DeviceTests.cs` | Sample MSTest tests |
| `AndroidManifest.xml` | Declares Instrumentation component |

## Android Instrumentation

The key to making tests wait properly is using Android's Instrumentation framework:

```csharp
[Instrumentation(Name = "blankandroid.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    // Required constructor for Android .NET interop
    public TestInstrumentation(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer) { }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Start(); // Triggers OnStart on a new thread
    }

    public override async void OnStart()
    {
        base.OnStart();
        
        // Run MTP tests
        int exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);
        
        // Signal completion - INSTRUMENTATION_CODE: -1 = success
        Finish(exitCode == 0 ? Result.Ok : Result.Canceled, results);
    }
}
```

The AndroidManifest.xml declares the instrumentation:
```xml
<instrumentation
  android:name="blankandroid.TestInstrumentation"
  android:targetPackage="com.companyname.BlankAndroid"
  android:label="Test Instrumentation" />
```

## Path to Success

### ✅ Phase 1: COMPLETE - Working Prototype with Instrumentation
- [x] MSBuild targets intercept `dotnet test` for device projects
- [x] Deploy APK via `adb install`
- [x] Run tests via `adb shell am instrument -w`
- [x] Android Instrumentation properly waits for test completion
- [x] MTP extensions report test results via logcat
- [x] Exit code propagates correctly via `Instrumentation.Finish()`
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
- [ ] iOS support (same pattern with XCTest Instrumentation)

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

## TRX and Logcat Collection Details

### TRX Collection
The TRX file is collected using:
1. `adb shell run-as <app-id> ls -t files/TestResults/` - Get latest TRX filename
2. `adb shell run-as <app-id> cat files/TestResults/<file.trx>` - Read file content
3. Save to `bin/Debug/net10.0-android/TestResults/<ProjectName>.trx`

### Logcat Collection
Full device logcat is saved for debugging purposes:
1. `adb logcat -d > TestResults/<ProjectName>_logcat.txt`
2. Captured after test execution completes
3. Contains all Android logs including:
   - MTP test output (`MTP.TestResults`, `MTP.TestSession` tags)
   - .NET runtime logs (`DOTNET` tag)
   - Test instrumentation logs (`TestInstrumentation` tag)
   - Crash information if tests fail

### Output Files
After test execution, the `TestResults` directory contains:
```
bin/Debug/net10.0-android/TestResults/
├── BlankAndroid.trx           # Standard TRX test results
└── BlankAndroid_logcat.txt    # Full device logcat for debugging
```

This works because:
- `run-as` allows accessing app's private storage without root
- `cat` outputs file content to stdout which can be redirected locally
- Works with debuggable APKs (debug builds)

## References

- [MAUI Device Testing Spec](https://github.com/dotnet/maui/pull/33117)
- [Microsoft.Testing.Platform](https://aka.ms/mtp-overview)
- [dotnet run --device (.NET 11)](https://github.com/dotnet/sdk)
- [Android Instrumentation](https://developer.android.com/reference/android/app/Instrumentation)

---
**Last Updated:** 2026-01-13  
**Status:** ✅ Working prototype with Android Instrumentation + TRX collection, awaiting SDK CLI integration for `dotnet test --device`
