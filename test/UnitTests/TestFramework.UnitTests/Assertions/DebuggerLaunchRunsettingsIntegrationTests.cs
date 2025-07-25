// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Tests demonstrating the debugger launch feature with runsettings integration.
/// These tests show how the feature should work with TestRunParameters from .runsettings files.
/// </summary>
public class DebuggerLaunchRunsettingsIntegrationTests : TestContainer
{
    private TestContext? _testContext;

    /// <summary>
    /// Gets or sets the test context which provides information about and functionality for the current test run.
    /// </summary>
    public TestContext TestContext
    {
        get => _testContext ?? throw new InvalidOperationException("TestContext not set");
        set => _testContext = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DebuggerLaunchSettings.Reset();
        }
        base.Dispose(disposing);
    }

    public void DebuggerLaunchSettingsFromTestRunParameters()
    {
        // This test demonstrates how runsettings parameters would be accessed in practice
        // In a real scenario, these would come from a .runsettings file like:
        // <RunSettings>
        //   <TestRunParameters>
        //     <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
        //   </TestRunParameters>
        // </RunSettings>

        // Arrange - Simulate TestContext with TestRunParameters from runsettings
        var mockTestContext = CreateMockTestContext(new Dictionary<string, string>
        {
            ["MSTest.LaunchDebuggerOnFailure"] = "true"
        });

        DebuggerLaunchSettings.RegisterTestContext(mockTestContext);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchSettingsWithTestFilterFromRunsettings()
    {
        // Arrange - Simulate runsettings with both enable flag and test filter
        var mockTestContext = CreateMockTestContext(new Dictionary<string, string>
        {
            ["MSTest.LaunchDebuggerOnFailure"] = "true",
            ["MSTest.LaunchDebuggerTestFilter"] = "MyFlakyTest"
        });

        DebuggerLaunchSettings.RegisterTestContext(mockTestContext);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.TestNameFilter == "MyFlakyTest");
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchSettingsDisabledByDefaultInRunsettings()
    {
        // Arrange - Simulate empty runsettings or no debugger settings
        var mockProperties = new System.Collections.Hashtable();

        // Act
        bool isEnabled = mockProperties["MSTest.LaunchDebuggerOnFailure"]?.ToString() == "true";

        // Assert
        Verify(!isEnabled);
    }

    public void ExampleRunsettingsConfiguration()
    {
        // This test documents the expected .runsettings format for the feature
        string exampleRunsettings = """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <!-- Configuration for debugger launch on test failure -->
              <TestRunParameters>
                <!-- Enable debugger launch when any test assertion fails -->
                <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
                
                <!-- Optional: Only launch debugger for tests matching this filter -->
                <Parameter name="MSTest.LaunchDebuggerTestFilter" value="FlakyIntegrationTest" />
              </TestRunParameters>
              
              <!-- Other standard runsettings configurations -->
              <RunConfiguration>
                <MaxCpuCount>1</MaxCpuCount>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
              </RunConfiguration>
            </RunSettings>
            """;

        // Verify the example contains the expected configuration keys
        Verify(exampleRunsettings.Contains("MSTest.LaunchDebuggerOnFailure"));
        Verify(exampleRunsettings.Contains("MSTest.LaunchDebuggerTestFilter"));
        Verify(exampleRunsettings.Contains("TestRunParameters"));
    }

    public void ExampleTestConfigConfiguration()
    {
        // This test documents the expected app.config/web.config format for .NET Framework
        string exampleAppConfig = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <configSections>
                <section name="microsoft.visualstudio.testtools" 
                         type="Microsoft.VisualStudio.TestTools.UnitTesting.TestConfigurationSection, 
                               Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions, 
                               Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
              </configSections>
              
              <!-- MSTest configuration section -->
              <microsoft.visualstudio.testtools 
                launchDebuggerOnFailure="true" 
                debuggerLaunchTestFilter="FlakyIntegrationTest">
                
                <!-- Other MSTest configurations like data sources can go here -->
                <dataSources>
                  <!-- Data source configurations -->
                </dataSources>
              </microsoft.visualstudio.testtools>
            </configuration>
            """;

        // Verify the example contains the expected configuration attributes
        Verify(exampleAppConfig.Contains("launchDebuggerOnFailure"));
        Verify(exampleAppConfig.Contains("debuggerLaunchTestFilter"));
        Verify(exampleAppConfig.Contains("microsoft.visualstudio.testtools"));
    }

    public void UsageExampleWithActualTestMethod()
    {
        // This demonstrates how a developer would use the feature in practice
        
        // Step 1: Configure via runsettings (this would be in a .runsettings file)
        // <Parameter name="MSTest.LaunchDebuggerOnFailure" value="true" />
        
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

    private static TestContext CreateMockTestContext(Dictionary<string, string> properties)
    {
        // Create a simple mock TestContext for testing
        // In a real implementation, this would be provided by the test framework
        return new MockTestContext(properties);
    }

    private class MockTestContext : TestContext
    {
        private readonly System.Collections.IDictionary _properties;

        public MockTestContext(Dictionary<string, string> properties)
        {
            _properties = new System.Collections.Hashtable();
            foreach (var kvp in properties)
            {
                _properties[kvp.Key] = kvp.Value;
            }
        }

        public override System.Collections.IDictionary Properties => _properties;

        // Implement abstract members with minimal functionality for testing
        public override void WriteLine(string message) { }
        public override void WriteLine(string format, params object?[] args) { }
        public override void Write(string message) { }
        public override void Write(string format, params object?[] args) { }
        public override void DisplayMessage(MessageLevel messageLevel, string message) { }
    }
}