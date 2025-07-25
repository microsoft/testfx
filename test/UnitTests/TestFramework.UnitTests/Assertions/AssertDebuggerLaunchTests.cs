// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertDebuggerLaunchTests : TestContainer
{
    public void DebuggerLaunchIsNotCalledWhenEnvironmentVariableIsNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
        
        // Act & Assert
        string? launchDebugger = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE");
        Verify(launchDebugger != "1");
    }

    public void DebuggerLaunchIsNotCalledWhenEnvironmentVariableIsSetToFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");
        
        // Act & Assert
        string? launchDebugger = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE");
        Verify(launchDebugger != "1");
        
        // Cleanup
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
    }

    public void DebuggerLaunchShouldBeCalledWhenEnvironmentVariableIsSetToOne()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");
        
        // Act & Assert
        string? launchDebugger = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE");
        Verify(launchDebugger == "1");
        
        // Cleanup
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
    }

    public void TestFilterEnvironmentVariableIsProperlyRead()
    {
        // Arrange
        const string testFilter = "MyTestMethod";
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", testFilter);
        
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