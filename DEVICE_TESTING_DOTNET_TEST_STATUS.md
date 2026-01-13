# dotnet test on Device Projects - Current Status

## What We Tried

```bash
cd samples/public/BlankAndroid
dotnet test
```

## Result

```
error: Testing with VSTest target is no longer supported by Microsoft.Testing.Platform 
on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet 
test experience. For more information, see https://aka.ms/dotnet-test-mtp-error
```

## Why This Happens

1. **global.json opts into MTP mode:**
   ```json
   {
     "test": {
       "runner": "Microsoft.Testing.Platform"
     }
   }
   ```

2. **Device projects can't use VSTest path:**
   - BlankAndroid is a `net10.0-android` project
   - It's configured as a test project (`IsTestingPlatformApplication=true`)
   - SDK 10 blocks VSTest target when MTP is enabled
   - But MTP's standard test execution doesn't work for device projects

3. **Device projects need special handling:**
   - Can't directly execute the DLL/EXE on the development machine
   - Must be deployed to a device/emulator first
   - Must be launched via platform-specific tools (adb for Android)
   - Must collect results from device storage

## What's Needed

### Option 1: Use MSBuild Test Infrastructure (Recommended)

The new SDK 10 has `UseMSBuildTestInfrastructure` but it's currently `false` for this project.
We need to:

1. **Enable MSBuild test infrastructure for device projects**
   ```xml
   <PropertyGroup Condition="'$(TargetPlatformIdentifier)' == 'android' OR '$(TargetPlatformIdentifier)' == 'ios'">
     <UseMSBuildTestInfrastructure>true</UseMSBuildTestInfrastructure>
   </PropertyGroup>
   ```

2. **Implement InvokeDeviceTestingPlatformTask**
   - Extend `InvokeTestingPlatformTask` to detect device TFMs
   - Delegate to device-specific deployment and execution logic
   - Wire up our device manager/deployer/runner/collector

3. **Create Device MSBuild Targets**
   - Import device-specific targets for Android/iOS
   - Handle `--device` and `--list-devices` CLI arguments
   - Pass through to our device infrastructure

### Option 2: Temporary Workaround (Manual Testing)

Until MSBuild integration is complete, you can:

1. **Build the APK:**
   ```bash
   dotnet build BlankAndroid.csproj
   ```

2. **Deploy manually:**
   ```bash
   adb install -r bin/Debug/net10.0-android/com.companyname.BlankAndroid-Signed.apk
   ```

3. **Run on device:**
   ```bash
   adb shell am start -n com.companyname.BlankAndroid/.MainActivity
   ```

4. **Collect results:**
   ```bash
   adb pull /data/data/com.companyname.BlankAndroid/files/TestResults/ ./TestResults/
   ```

### Option 3: Direct Executable Approach (Not Recommended)

Try to make the Android project produce a desktop-runnable test host, but this defeats
the purpose of device testing - we want to test ON the actual device, not in a simulator
on the desktop.

## What Works Right Now

✅ **Build:** `dotnet build BlankAndroid.csproj` - **SUCCESS**
   - Generates signed APK
   - Includes test framework and tests
   - Ready for device deployment

✅ **Device Infrastructure:**
   - AndroidDeviceManager can discover devices
   - AndroidDeviceDeployer can install APKs
   - AndroidDeviceTestRunner can launch and monitor
   - AndroidArtifactCollector can retrieve results

❌ **dotnet test:** Blocked by SDK 10 VSTest target check

## Recommendation

**Proceed with implementing MSBuild integration (Option 1).**

This is the proper, long-term solution that aligns with:
- The MAUI device testing spec
- SDK 10's new MSBuild test infrastructure
- Microsoft.Testing.Platform's architecture

**Estimated effort:** 2-3 hours to wire up the MSBuild tasks and targets.

## Files to Modify

1. `src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/InvokeTestingPlatformTask.cs`
   - Detect device TFM (`net10.0-android`, `net10.0-ios`)
   - Delegate to device testing path

2. `src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets`
   - Add device detection logic
   - Enable `UseMSBuildTestInfrastructure` for device projects
   - Import device-specific targets

3. Create new: `src/Platform/Microsoft.Testing.Platform.MSBuild/Tasks/InvokeDeviceTestingPlatformTask.cs`
   - Instantiate device manager/deployer/runner/collector
   - Orchestrate device test workflow
   - Report results back to MSBuild

4. Create new: `src/Platform/Microsoft.Testing.Platform.MSBuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.Device.targets`
   - Define DeviceTestingPlatform target
   - Wire up properties (DeviceId, etc.)
   - Integrate with Test target

## Next Command to Try

After MSBuild integration is complete:

```bash
# Discover devices
dotnet test --list-devices

# Run on specific device
dotnet test --device "emulator-5554"

# Run with coverage
dotnet test --collect "Code Coverage"
```

---

**Current Status:** Build works, test execution blocked by missing MSBuild integration.
**Next Step:** Implement InvokeDeviceTestingPlatformTask and device targets.
