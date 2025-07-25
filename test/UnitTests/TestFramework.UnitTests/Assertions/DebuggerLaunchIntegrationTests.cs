// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Integration tests for the debugger launch functionality.
/// These tests verify the complete flow of configuration reading and debugger launch behavior.
/// </summary>
public class DebuggerLaunchIntegrationTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up environment variables and settings after each test
            DebuggerLaunchSettings.Reset();
            Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
            Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", null);
        }
        base.Dispose(disposing);
    }

    public void DebuggerLaunchViaConfigurationSettings()
    {
        // Arrange
        DebuggerLaunchSettings.SetConfiguration(enabled: true);

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(shouldLaunch);
    }

    public void DebuggerLaunchViaEnvironmentVariableFallback()
    {
        // Arrange
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(shouldLaunch);
    }

    public void DebuggerLaunchDisabledByDefault()
    {
        // Arrange
        DebuggerLaunchSettings.Reset();
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(!shouldLaunch);
    }

    public void ConfigurationTakesPrecedenceOverEnvironmentVariable()
    {
        // Arrange - set env var to disabled but config to enabled
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");
        DebuggerLaunchSettings.SetConfiguration(enabled: true);

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(shouldLaunch); // Configuration should take precedence
    }

    public void AssertFailPreservesOriginalExceptionType()
    {
        // Arrange
        DebuggerLaunchSettings.SetConfiguration(enabled: false);

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.Fail("Test failure message"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Test failure message"));
    }

    public void AssertAreEqualPreservesOriginalExceptionType()
    {
        // Arrange
        DebuggerLaunchSettings.SetConfiguration(enabled: false);

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.AreEqual(10, 5, "Values should be equal"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Expected:<10>"));
        Verify(ex.Message.Contains("Actual:<5>"));
    }

    public void VariousAssertMethodsAllUseThrowAssertFailedPath()
    {
        // Test that different assertion methods all go through the same debugger launch logic
        DebuggerLaunchSettings.SetConfiguration(enabled: false);

        // All these should throw AssertFailedException without launching debugger
        Exception ex1 = VerifyThrows(() => Assert.IsTrue(false));
        Exception ex2 = VerifyThrows(() => Assert.IsNull("not null"));
        Exception ex3 = VerifyThrows(() => Assert.AreNotEqual(5, 5));
        Exception ex4 = VerifyThrows(() => Assert.IsInstanceOfType("string", typeof(int)));

        Verify(ex1 is AssertFailedException);
        Verify(ex2 is AssertFailedException);
        Verify(ex3 is AssertFailedException);
        Verify(ex4 is AssertFailedException);
    }

    /// <summary>
    /// This method uses reflection to access the private ShouldLaunchDebuggerOnFailure method
    /// in the Assert class to test the complete integration.
    /// </summary>
    private static bool GetShouldLaunchDebuggerOnFailureResult()
    {
        var method = typeof(Assert).GetMethod("ShouldLaunchDebuggerOnFailure", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException("Could not find ShouldLaunchDebuggerOnFailure method");
        }

        var result = method.Invoke(null, null);
        return result is bool && (bool)result;
    }
}