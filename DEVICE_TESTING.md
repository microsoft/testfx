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

## Execution Modes

### Activity Mode (Default)
- Uses `dotnet run --device` to deploy and launch
- Tests run in MainActivity.OnCreate
- App exits via `Java.Lang.JavaSystem.Exit(exitCode)`
- Pros: Simple, leverages existing `dotnet run` infrastructure
- Cons: App may not signal completion reliably in some scenarios

### Instrumentation Mode (`-p:UseInstrumentation=true`)
- Uses `dotnet build -t:Install` to deploy
- Uses `adb shell am instrument -w` to run TestInstrumentation class
- `-w` flag waits for completion
- Instrumentation.Finish() signals completion with result code
- Pros: More reliable completion detection, proper exit codes
- Cons: Requires separate build and run steps

## What's Missing ❌

| Feature | Status | Blocker |
|---------|--------|---------|
| `--device` CLI argument | ❌ | Needs SDK change to `dotnet test` |
| `--project` CLI argument | ❌ | Needs SDK change to `dotnet test` |
| `--list-devices` argument | ❌ | Needs SDK change (already in `dotnet run`) |

## Architecture

### Activity Mode (Default)

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Directory.Build.targets                           │
│  - Detects device TFM (net10.0-android)                    │
│  - Overrides VSTest target                                  │
│  - Delegates to `dotnet run --device`                       │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _RunTestsOnDeviceViaDotnetRun Target                       │
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
│  _CollectDeviceTestResults Target                          │
│  - adb shell run-as ... ls -t files/TestResults/           │
│  - adb shell run-as ... cat <latest.trx>                   │
│  - adb logcat -d > <ProjectName>_logcat.txt                │
│  - Saves to bin/Debug/net10.0-android/TestResults/         │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  Output                                                     │
│  - Console: Test results streamed via logcat               │
│  - TRX: bin/.../TestResults/BlankAndroid.trx               │
│  - Logcat: bin/.../TestResults/BlankAndroid_logcat.txt     │
│  - Exit code: 0 (success) or non-zero (failures)           │
└─────────────────────────────────────────────────────────────┘
```

### Instrumentation Mode (`-p:UseInstrumentation=true`)

```
dotnet test BlankAndroid.csproj -f net10.0-android -p:DeviceId=emulator-5554 -p:UseInstrumentation=true
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  MSBuild: Directory.Build.targets                           │
│  - Detects device TFM (net10.0-android)                    │
│  - Overrides VSTest target                                  │
│  - UseInstrumentation=true → delegates to adb instrument   │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  _RunTestsOnDeviceViaInstrumentation Target                │
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
│  - adb logcat -d > <ProjectName>_logcat.txt                │
│  - Saves to bin/Debug/net10.0-android/TestResults/         │
└─────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────┐
│  Output                                                     │
│  - Console: Instrumentation output + logcat                │
│  - TRX: bin/.../TestResults/BlankAndroid.trx               │
│  - Logcat: bin/.../TestResults/BlankAndroid_logcat.txt     │
│  - Exit code: 0 (success) or non-zero (failures)           │
└─────────────────────────────────────────────────────────────┘
```

## Key Files

### samples/public/BlankAndroid/

| File | Purpose |
|------|---------|
| `BlankAndroid.csproj` | Project with MTP + TRX configuration |
| `Directory.Build.targets` | MSBuild targets - supports both Activity and Instrumentation modes |
| `MainActivity.cs` | Activity mode entry point - runs MTP tests and exits |
| `TestInstrumentation.cs` | Instrumentation mode entry point - runs MTP tests with proper completion signaling |
| `DeviceTestReporter.cs` | MTP extensions for test output to logcat |
| `DeviceTests.cs` | Sample MSTest tests |
| `AndroidManifest.xml` | Android manifest with Instrumentation registration |

## How It Works

### Activity Mode Entry Point (MainActivity)

When the app launches via `dotnet run --device`, `MainActivity.OnCreate` immediately runs the test platform:

```csharp
[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Configure test results directory
        var filesDir = FilesDir?.AbsolutePath ?? "/data/local/tmp";
        var testResultsDir = Path.Combine(filesDir, "TestResults");
        
        var args = new[]
        {
            "--results-directory", testResultsDir,
            "--report-trx"
        };

        // Run MTP tests
        int exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);

        // Exit with test result code
        Java.Lang.JavaSystem.Exit(exitCode);
    }
}
```

### Instrumentation Mode Entry Point (TestInstrumentation)

When launched via `adb shell am instrument -w`, `TestInstrumentation` runs tests with proper completion signaling:

```csharp
[Instrumentation(Name = "blankandroid.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Start(); // Triggers OnStart
    }

    public override async void OnStart()
    {
        base.OnStart();

        int exitCode = 1;
        Bundle results = new Bundle();

        try
        {
            var context = TargetContext;
            var filesDir = context?.FilesDir?.AbsolutePath ?? "/data/local/tmp";
            var testResultsDir = Path.Combine(filesDir, "TestResults");
            Directory.CreateDirectory(testResultsDir);

            var args = new[] { "--results-directory", testResultsDir, "--report-trx" };
            exitCode = await MicrosoftTestingPlatformEntryPoint.Main(args);

            results.PutInt("exitCode", exitCode);
            results.PutString("status", exitCode == 0 ? "SUCCESS" : "FAILURE");
        }
        catch (Exception ex)
        {
            results.PutString("error", ex.ToString());
        }
        finally
        {
            // Signal completion - adb instrument -w will wait for this
            Finish(exitCode == 0 ? Result.Ok : Result.Canceled, results);
        }
    }
}
```

### MSBuild Targets

The `Directory.Build.targets` overrides `VSTest` to support both modes:

```xml
<Target Name="VSTest" Condition="'$(_IsDeviceTestProject)' == 'true'">
  <!-- Choose mode based on UseInstrumentation property -->
  <CallTarget Targets="_RunTestsOnDeviceViaDotnetRun" Condition="'$(UseInstrumentation)' != 'true'" />
  <CallTarget Targets="_RunTestsOnDeviceViaInstrumentation" Condition="'$(UseInstrumentation)' == 'true'" />
  <CallTarget Targets="_CollectDeviceTestResults" />
</Target>

<!-- Mode 1: Activity mode via dotnet run -->
<Target Name="_RunTestsOnDeviceViaDotnetRun">
  <Exec Command="&quot;$(DotnetDevicePath)&quot; run --project &quot;$(MSBuildProjectFullPath)&quot; -f $(TargetFramework) --device $(DeviceId)" />
</Target>

<!-- Mode 2: Instrumentation mode via adb instrument -->
<Target Name="_RunTestsOnDeviceViaInstrumentation">
  <!-- Build and install -->
  <Exec Command="&quot;$(DotnetDevicePath)&quot; build &quot;$(MSBuildProjectFullPath)&quot; -f $(TargetFramework) -t:Install -p:AdbTarget=&quot;$(_AdbDevice)&quot;" />
  <!-- Run instrumentation (-w waits for completion) -->
  <Exec Command="adb $(_AdbDevice) shell am instrument -w $(ApplicationId)/$(RootNamespace).TestInstrumentation" />
</Target>
```

## Path to Success

### ✅ Phase 1: COMPLETE - Working Prototype using `dotnet run --device`
- [x] MSBuild targets intercept `dotnet test` for device projects
- [x] Delegate execution to `dotnet run --device` (.NET 11)
- [x] `dotnet run` handles build, deploy, execute, and output streaming
- [x] App runs MTP tests via `MainActivity.OnCreate`
- [x] MTP extensions report test results via logcat
- [x] Exit code propagates correctly via `Java.Lang.JavaSystem.Exit()`
- [x] TRX file collection from device
- [x] Logcat collection for debugging

### 🔄 Phase 2: IN PROGRESS - CLI Parity with `dotnet run`

**Required:** Add `--device` and `--project` flags to `dotnet test` CLI

The .NET SDK already supports these for `dotnet run`. We need the same for `dotnet test`:

```bash
# dotnet run (works today in .NET 11):
dotnet run --project X.csproj -f net10.0-android --device emulator-5554

# dotnet test (goal):
dotnet test --project X.csproj -f net10.0-android --device emulator-5554
```

**Implementation options:**
1. **SDK Change:** Add `--device` parsing to `dotnet test` command
2. **MSBuild Pass-through:** SDK passes `--device` as MSBuild property `$(DeviceId)`

### 📋 Phase 3: Future Enhancements
- [ ] `--list-devices` support (already in `dotnet run`)
- [ ] Code coverage collection from device
- [ ] iOS support (same pattern with test host app)

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
   - Device test logs (`DeviceTests` tag)
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
**Status:** ✅ Working prototype using `dotnet run --device` + TRX/logcat collection, awaiting SDK CLI integration for `dotnet test --device`
