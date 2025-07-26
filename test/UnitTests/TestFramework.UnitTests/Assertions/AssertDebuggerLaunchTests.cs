// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertDebuggerLaunchTests : TestContainer
{
    public void Cleanup()
    {
        // Reset settings after each test
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", null);
    }

    public void DebuggerLaunchIsNotEnabledByDefault()
    {
        // Arrange - ensure clean state
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        
        // Act & Assert
        Verify(!DebuggerLaunchSettings.IsEnabled);
    }

    public void DebuggerLaunchViaTestRunParameters()
    {
        // Arrange - Simulate TestContext with TestRunParameters
        var mockTestContext = CreateMockTestContext(new Dictionary<string, string>
        {
            ["MSTest.LaunchDebuggerOnFailure"] = "true"
        });

        DebuggerLaunchSettings.RegisterTestContext(mockTestContext);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchViaTestRunParametersWithFilter()
    {
        // Arrange - Simulate TestContext with TestRunParameters including filter
        var mockTestContext = CreateMockTestContext(new Dictionary<string, string>
        {
            ["MSTest.LaunchDebuggerOnFailure"] = "true",
            ["MSTest.LaunchDebuggerTestFilter"] = "FlakyTest"
        });

        DebuggerLaunchSettings.RegisterTestContext(mockTestContext);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.TestNameFilter == "FlakyTest");
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void TestRunParametersTakePrecedenceOverEnvironmentVariables()
    {
        // Arrange - Set environment variable to disabled but TestRunParameters to enabled
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");
        
        var mockTestContext = CreateMockTestContext(new Dictionary<string, string>
        {
            ["MSTest.LaunchDebuggerOnFailure"] = "true"
        });

        DebuggerLaunchSettings.RegisterTestContext(mockTestContext);

        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled); // TestRunParameters should take precedence
    }

    public void DebuggerLaunchCanBeEnabledProgrammatically()
    {
        // Arrange
        DebuggerLaunchSettings.SetConfiguration(enabled: true);
        
        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
    }

    public void DebuggerLaunchCanBeEnabledWithTestFilter()
    {
        // Arrange
        const string testFilter = "MyTestMethod";
        DebuggerLaunchSettings.SetConfiguration(enabled: true, testNameFilter: testFilter);
        
        // Act & Assert
        Verify(DebuggerLaunchSettings.IsEnabled);
        Verify(DebuggerLaunchSettings.TestNameFilter == testFilter);
        Verify(DebuggerLaunchSettings.ShouldLaunchForCurrentTest());
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

    public void DebuggerLaunchFallsBackToEnvironmentVariable()
    {
        // Arrange
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");
        
        try
        {
            // Act
            var shouldLaunch = Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Instance.GetType()
                .GetMethod("ShouldLaunchDebuggerOnFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, null);
            
            // Assert
            Verify(shouldLaunch is bool && (bool)shouldLaunch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        }
    }

    public void DebuggerLaunchIsDisabledWhenEnvironmentVariableIsNotSet()
    {
        // Arrange
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        
        // Act
        var shouldLaunch = Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Instance.GetType()
            .GetMethod("ShouldLaunchDebuggerOnFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, null);
        
        // Assert
        Verify(shouldLaunch is bool && !(bool)shouldLaunch);
    }
}
        
        // Act
        string? actualFilter = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER");
        
        // Assert
        Verify(actualFilter == testFilter);
        
        // Cleanup
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", null);
    }

    public void AssertFailWithDebuggerLaunchDisabledShouldThrowAssertFailedException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        
        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.Fail("Test message"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Test message"));
    }

    public void AssertFailWithDebuggerLaunchEnabledShouldStillThrowAssertFailedException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");
        
        // Act & Assert - Note: Debugger.Launch() will be called but we can't easily test that
        // The important thing is that the exception is still thrown
        Exception ex = VerifyThrows(() => Assert.Fail("Test message"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Test message"));
        
        // Cleanup
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
    }
}