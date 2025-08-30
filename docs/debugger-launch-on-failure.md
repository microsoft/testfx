# MSTest Debugger Launch on Test Failure

This feature allows developers to automatically launch a debugger when test assertions fail, making it easier to debug intermittent or hard-to-reproduce test failures by preserving the exact program state at the moment of failure.

## Benefits

- ✅ **Preserves Stack State**: Debugger attaches at exact assertion failure point
- ✅ **Universal Compatibility**: Works with Visual Studio, VS Code, and console debugging
- ✅ **Zero Performance Impact**: No overhead when feature is disabled
- ✅ **Flexible Configuration**: Multiple configuration options for different scenarios
- ✅ **Targeted Debugging**: Optional test name filtering for specific scenarios
- ✅ **Non-Breaking**: Fully backward compatible with existing test suites

## Configuration

### Option 1: Using .runsettings file (Recommended for modern .NET)

Create or modify your `.runsettings` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <TestRunParameters>
    <!-- Enable debugger launch when any test assertion fails -->
    <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
    
    <!-- Optional: Only launch debugger for tests matching this filter -->
    <Parameter name="MSTest.LaunchDebuggerTestFilter" value="FlakyIntegrationTest" />
  </TestRunParameters>
</RunSettings>
```

Run your tests with the settings file:
```bash
dotnet test --settings my.runsettings
```

### Option 2: Using app.config/web.config (.NET Framework)

Add to your test project's `app.config` or `web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="microsoft.visualstudio.testtools" 
             type="Microsoft.VisualStudio.TestTools.UnitTesting.TestConfigurationSection, 
                   Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions" />
  </configSections>
  
  <microsoft.visualstudio.testtools 
    launchDebuggerOnFailure="true" 
    debuggerLaunchTestFilter="FlakyIntegrationTest">
  </microsoft.visualstudio.testtools>
</configuration>
```

### Option 3: Environment Variables (Fallback/Legacy)

```bash
# Enable debugger launch on any test failure
export MSTEST_LAUNCH_DEBUGGER_ON_FAILURE=1
dotnet test

# Optional: Only launch debugger for specific tests
export MSTEST_LAUNCH_DEBUGGER_ON_FAILURE=1
export MSTEST_LAUNCH_DEBUGGER_TEST_FILTER="MyFlakyTest"
dotnet test
```

## Usage Example

```csharp
[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public void FlakyIntegrationTest()
    {
        // Arrange
        var service = new MyService();
        var complexInput = CreateComplexTestData();
        
        // Act
        var result = service.ProcessData(complexInput);
        
        // Assert - If this fails with debugger enabled, you can inspect:
        // - service state, complexInput values, result contents
        // - Full call stack and thread context
        Assert.AreEqual(expectedValue, result.Value);
        Assert.IsTrue(result.IsValid, "Result should be valid");
    }
}
```

## Configuration Options

| Setting | .runsettings Parameter | .config Attribute | Environment Variable | Description |
|---------|----------------------|-------------------|---------------------|-------------|
| Enable Feature | `MSTest.LaunchDebuggerOnFailure` | `launchDebuggerOnFailure` | `MSTEST_LAUNCH_DEBUGGER_ON_FAILURE` | Set to `true` or `1` to enable |
| Test Filter | `MSTest.LaunchDebuggerTestFilter` | `debuggerLaunchTestFilter` | `MSTEST_LAUNCH_DEBUGGER_TEST_FILTER` | Optional filter for specific tests |

## Configuration Precedence

The feature checks configuration sources in this order:

1. **MSTest Settings** (.runsettings TestRunParameters or .config section) - **Highest Priority**
2. **Environment Variables** - **Fallback/Legacy Support**

If MSTest settings are configured, environment variables are ignored.

## Development Workflow

### Local Development
```bash
# Create .runsettings with debugger launch enabled
echo '<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <TestRunParameters>
    <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
  </TestRunParameters>
</RunSettings>' > debug.runsettings

# Run tests with debugger launch
dotnet test --settings debug.runsettings
```

### CI/CD Pipeline
```yaml
# In CI, use normal runsettings without debugger launch
- name: Run Tests
  run: dotnet test --settings ci.runsettings
```

### Targeted Debugging
```xml
<!-- Only debug specific failing tests -->
<RunSettings>
  <TestRunParameters>
    <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
    <Parameter name="MSTest.LaunchDebuggerTestFilter" value="IntegrationTest" />
  </TestRunParameters>
</RunSettings>
```

## IDE Integration

### Visual Studio
1. Set breakpoints in your test method (optional)
2. Configure debugger launch via `.runsettings` or `app.config`
3. Run tests normally - debugger will launch on assertion failures
4. Use "Debug All Tests" or "Debug Selected Tests" for additional debugging

### VS Code
1. Configure launch settings in `.vscode/launch.json` if needed
2. Use `.runsettings` configuration
3. Run tests via Test Explorer or command line
4. Debugger will attach automatically on failures

### Command Line
Works with any debugger that can attach to .NET processes:
- Visual Studio debugger
- VS Code debugger  
- dotnet-dump for post-mortem analysis
- JetBrains Rider debugger

## Troubleshooting

### Debugger Not Launching
1. Verify configuration is correct in `.runsettings` or `.config`
2. Check that `MSTest.LaunchDebuggerOnFailure` is set to `true` or `1`
3. Ensure test is actually failing (debugger only launches on assertion failures)
4. Verify debugger is available on the system

### Performance Concerns
- Zero performance impact when disabled
- Minimal overhead when enabled (only on assertion failures)
- No impact on successful tests

### Multiple Test Runners
- Feature works with `dotnet test`, Visual Studio Test Explorer, and other MSTest-compatible runners
- Configuration method may vary by runner (prefer `.runsettings` for portability)

## Implementation Notes

The feature is implemented at the core assertion level (`Assert.ThrowAssertFailed`) so it works with all MSTest assertion methods:
- `Assert.AreEqual`, `Assert.IsTrue`, `Assert.IsNull`, etc.
- `StringAssert.*` methods
- `CollectionAssert.*` methods  
- Custom assertion extensions

The debugger launch happens before the exception is thrown, preserving the complete call stack and local variable state.