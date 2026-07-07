// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

internal static class TestResultOutputExtensions
{
    /// <summary>
    /// Captures the output, error, trace and diagnostic messages from the test context and
    /// assigns them to the corresponding <see cref="TestResult"/> properties.
    /// </summary>
    /// <param name="result">The test result to populate.</param>
    /// <param name="testContextImpl">The test context implementation to read (and clear) output, error and trace from. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context to read (and clear) diagnostic messages from.</param>
    public static void SetOutputAndTraces(this TestResult result, TestContextImplementation? testContextImpl, ITestContext testContext)
    {
        result.LogOutput = testContextImpl?.GetAndClearOutput();
        result.LogError = testContextImpl?.GetAndClearError();
        result.DebugTrace = testContextImpl?.GetAndClearTrace();
        result.TestContextMessages = testContext.GetAndClearDiagnosticMessages();
    }

    /// <summary>
    /// Captures the output, error, trace and diagnostic messages from the test context and
    /// assigns them to the corresponding <see cref="TestResult"/> properties, prepending the
    /// provided prefixes (typically assembly/class initialize logs).
    /// </summary>
    /// <param name="result">The test result to populate.</param>
    /// <param name="testContextImpl">The test context implementation to read (and clear) output, error and trace from. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context to read (and clear) diagnostic messages from.</param>
    /// <param name="outputPrefix">The value prepended to the captured output.</param>
    /// <param name="errorPrefix">The value prepended to the captured error.</param>
    /// <param name="tracePrefix">The value prepended to the captured trace.</param>
    /// <param name="messagesPrefix">The value prepended to the captured diagnostic messages.</param>
    public static void SetOutputAndTraces(this TestResult result, TestContextImplementation? testContextImpl, ITestContext testContext, string? outputPrefix, string? errorPrefix, string? tracePrefix, string? messagesPrefix)
    {
        result.LogOutput = outputPrefix + testContextImpl?.GetAndClearOutput();
        result.LogError = errorPrefix + testContextImpl?.GetAndClearError();
        result.DebugTrace = tracePrefix + testContextImpl?.GetAndClearTrace();
        result.TestContextMessages = messagesPrefix + testContext.GetAndClearDiagnosticMessages();
    }

    /// <summary>
    /// Captures the output, error, trace and diagnostic messages from the test context and
    /// appends them to the corresponding <see cref="TestResult"/> properties.
    /// </summary>
    /// <param name="result">The test result to append to.</param>
    /// <param name="testContextImpl">The test context implementation to read (and clear) output, error and trace from. May be <see langword="null"/>.</param>
    /// <param name="testContext">The test context to read (and clear) diagnostic messages from.</param>
    public static void AppendOutputAndTraces(this TestResult result, TestContextImplementation? testContextImpl, ITestContext testContext)
    {
        result.LogOutput += testContextImpl?.GetAndClearOutput();
        result.LogError += testContextImpl?.GetAndClearError();
        result.DebugTrace += testContextImpl?.GetAndClearTrace();
        result.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
    }
}
