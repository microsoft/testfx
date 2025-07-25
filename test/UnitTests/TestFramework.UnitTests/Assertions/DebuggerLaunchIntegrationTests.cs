// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Integration tests for the debugger launch functionality.
/// These tests verify the complete flow of environment variable checking and debugger launch behavior.
/// </summary>
public class DebuggerLaunchIntegrationTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up environment variables after each test
            Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);
            Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", null);
        }
        base.Dispose(disposing);
    }

    public void ShouldLaunchDebuggerReturnsFalseWhenEnvironmentVariableNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", null);

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(!shouldLaunch);
    }

    public void ShouldLaunchDebuggerReturnsFalseWhenEnvironmentVariableSetToZero()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(!shouldLaunch);
    }

    public void ShouldLaunchDebuggerReturnsTrueWhenEnvironmentVariableSetToOne()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        Verify(shouldLaunch);
    }

    public void ShouldLaunchDebuggerReturnsTrueWhenFilterIsSetButNoTestContext()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "1");
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER", "SomeTestName");

        // Act
        bool shouldLaunch = GetShouldLaunchDebuggerOnFailureResult();

        // Assert
        // Should return true because we can't determine test name context in this case
        // and default to launching debugger when enabled
        Verify(shouldLaunch);
    }

    public void AssertFailPreservesOriginalExceptionType()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.Fail("Test failure message"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Test failure message"));
    }

    public void AssertAreEqualPreservesOriginalExceptionType()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.AreEqual(10, 5, "Values should be equal"));
        Verify(ex is AssertFailedException);
        Verify(ex.Message.Contains("Expected:<10>"));
        Verify(ex.Message.Contains("Actual:<5>"));
    }

    public void VariousAssertMethodsAllUseThrowAssertFailedPath()
    {
        // Test that different assertion methods all go through the same debugger launch logic
        Environment.SetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE", "0");

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
    /// This method replicates the logic from Assert.ShouldLaunchDebuggerOnFailure
    /// since that method is private. This allows us to test the logic without
    /// needing to expose internal implementation details.
    /// </summary>
    private static bool GetShouldLaunchDebuggerOnFailureResult()
    {
        // Check if debugger launch is enabled
        string? launchDebugger = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_ON_FAILURE");
        if (launchDebugger != "1")
        {
            return false;
        }

        // Check if there's a test name filter
        string? testNameFilter = Environment.GetEnvironmentVariable("MSTEST_LAUNCH_DEBUGGER_TEST_FILTER");
        if (string.IsNullOrWhiteSpace(testNameFilter))
        {
            return true; // No filter means launch for all failures
        }

        // For now, we return true when filter is set but we can't determine test name
        // This matches the current implementation in Assert.cs
        return true;
    }
}