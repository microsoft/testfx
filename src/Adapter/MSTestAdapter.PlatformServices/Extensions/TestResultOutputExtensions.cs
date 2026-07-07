// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Extension methods that capture the standard output, error, debug trace, and diagnostic messages
/// from a test context onto a <see cref="TestResult"/>.
/// </summary>
internal static class TestResultOutputExtensions
{
    /// <summary>
    /// Assigns the captured output, error, debug trace, and diagnostic messages from the given
    /// test context to the result.
    /// </summary>
    /// <param name="result">The result to populate.</param>
    /// <param name="testContextImpl">The test context implementation providing output/error/trace. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context providing diagnostic messages.</param>
    public static void SetOutputAndTraces(
        this TestResult result,
        TestContextImplementation? testContextImpl,
        ITestContext testContext)
    {
        result.LogOutput = testContextImpl?.GetAndClearOutput();
        result.LogError = testContextImpl?.GetAndClearError();
        result.DebugTrace = testContextImpl?.GetAndClearTrace();
        result.TestContextMessages = testContext.GetAndClearDiagnosticMessages();
    }

    /// <summary>
    /// Assigns the captured output, error, debug trace, and diagnostic messages from the given
    /// test context to the result, prefixing each stream with the provided initialization logs.
    /// </summary>
    /// <param name="result">The result to populate.</param>
    /// <param name="testContextImpl">The test context implementation providing output/error/trace. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context providing diagnostic messages.</param>
    /// <param name="initializationLogs">Logs to prepend to <see cref="TestResult.LogOutput"/>.</param>
    /// <param name="initializationErrorLogs">Logs to prepend to <see cref="TestResult.LogError"/>.</param>
    /// <param name="initializationTrace">Trace to prepend to <see cref="TestResult.DebugTrace"/>.</param>
    /// <param name="initializationTestContextMessages">Messages to prepend to <see cref="TestResult.TestContextMessages"/>.</param>
    public static void SetOutputAndTraces(
        this TestResult result,
        TestContextImplementation? testContextImpl,
        ITestContext testContext,
        string? initializationLogs,
        string? initializationErrorLogs,
        string? initializationTrace,
        string? initializationTestContextMessages)
    {
        result.LogOutput = initializationLogs + testContextImpl?.GetAndClearOutput();
        result.LogError = initializationErrorLogs + testContextImpl?.GetAndClearError();
        result.DebugTrace = initializationTrace + testContextImpl?.GetAndClearTrace();
        result.TestContextMessages = initializationTestContextMessages + testContext.GetAndClearDiagnosticMessages();
    }

    /// <summary>
    /// Appends the captured output, error, debug trace, and diagnostic messages from the given
    /// test context to the result.
    /// </summary>
    /// <param name="result">The result to append to.</param>
    /// <param name="testContextImpl">The test context implementation providing output/error/trace. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context providing diagnostic messages.</param>
    public static void AppendOutputAndTraces(
        this TestResult result,
        TestContextImplementation? testContextImpl,
        ITestContext testContext)
    {
        result.LogOutput += testContextImpl?.GetAndClearOutput();
        result.LogError += testContextImpl?.GetAndClearError();
        result.DebugTrace += testContextImpl?.GetAndClearTrace();
        result.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
    }
}
