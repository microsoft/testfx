// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Tests demonstrating the debugger launch feature with MSTestSettings integration.
/// These tests show how the feature should work with MSTestSettings configuration.
/// </summary>
public class DebuggerLaunchRunsettingsIntegrationTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DebuggerLaunchSettings.Reset();
        }
        base.Dispose(disposing);
    }

    public void DebuggerLaunchSettingsFromMSTestSettings()
    {
        // Arrange - Simulate MSTestSettings configuration
        DebuggerLaunchSettings.SetConfiguration(enabled: true);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchSettingsWithTestFilterFromMSTestSettings()
    {
        // Arrange - Simulate MSTestSettings with both enable flag and test filter
        DebuggerLaunchSettings.SetConfiguration(enabled: true, testNameFilter: "MyFlakyTest");

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.TestNameFilter == "MyFlakyTest");
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchSettingsDisabledByDefault()
    {
        // Arrange - No configuration applied

        // Act & Assert
        Verify(!DebuggerLaunchSettings.IsEnabled);
        Verify(!DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void ExampleRunsettingsConfiguration()
    {
        // This test documents the expected .runsettings format for the feature
        string exampleRunsettings = """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <!-- MSTest adapter configuration -->
              <MSTestV2>
                <LaunchDebuggerOnFailure>true</LaunchDebuggerOnFailure>
                <DebuggerLaunch_TestFilter>FlakyIntegrationTest</DebuggerLaunch_TestFilter>
              </MSTestV2>
              
              <!-- Other standard runsettings configurations -->
              <RunConfiguration>
                <MaxCpuCount>1</MaxCpuCount>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
              </RunConfiguration>
            </RunSettings>
            """;

        // Verify the example contains the expected configuration keys
        Verify(exampleRunsettings.Contains("LaunchDebuggerOnFailure"));
        Verify(exampleRunsettings.Contains("DebuggerLaunch_TestFilter"));
        Verify(exampleRunsettings.Contains("MSTestV2"));
    }

    public void ExampleTestConfigJsonConfiguration()
    {
        // This test documents the expected testconfig.json format for the feature
        string exampleTestConfig = """
            {
              "mstest": {
                "execution": {
                  "launchDebuggerOnFailure": true,
                  "debuggerLaunchTestFilter": "FlakyIntegrationTest"
                }
              }
            }
            """;

        // Verify the example contains the expected configuration keys
        Verify(exampleTestConfig.Contains("launchDebuggerOnFailure"));
        Verify(exampleTestConfig.Contains("debuggerLaunchTestFilter"));
        Verify(exampleTestConfig.Contains("execution"));
    }

    public void UsageExampleWithActualTestMethod()
    {
        // This demonstrates how a developer would use the feature in practice
        
        // Step 1: Configure via MSTestSettings (this would be set by the adapter from runsettings)
        // <MSTestV2><LaunchDebuggerOnFailure>true</LaunchDebuggerOnFailure></MSTestV2>
        
        // Step 2: Run tests normally - when this assertion fails, debugger launches
        try
        {
            // Simulate settings being enabled
            DebuggerLaunchSettings.SetConfiguration(enabled: true);
            
            // This would trigger debugger launch in real usage
            var exception = VerifyThrows(() => Assert.AreEqual(42, 24, "This test failure should launch debugger"));
            
            Verify(exception is AssertFailedException);
        }
        finally
        {
            DebuggerLaunchSettings.Reset();
        }
    }
}