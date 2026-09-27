// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class NopLoggerTests
{
    private const string Message = "DummyMessage";
    private readonly Exception _exception = new("TestException");
    private readonly NopLogger _nopLogger = new();

    [DynamicData(nameof(LogTestHelpers.GetLogLevelsForDynamicData), typeof(LogTestHelpers))]
    [TestMethod]
    public void NopLogger_CheckDisabled(LogLevel logLevel)
        => Assert.IsFalse(_nopLogger.IsEnabled(logLevel));

    [DynamicData(nameof(LogTestHelpers.GetLogLevelsForDynamicData), typeof(LogTestHelpers))]
    [TestMethod]
    public void NopLogger_Log_NoFormatterCalls(LogLevel logLevel)
    {
        // A per-test local counter (instead of the previous shared static field) removes any latent race
        // between the class's concurrently-runnable [TestMethod]s under this assembly's method-level
        // parallelization: each test now owns its own formatter-call count.
        int formatterCalls = 0;
        string Formatter(string state, Exception? exception)
        {
            formatterCalls++;
            return string.Empty;
        }

        _nopLogger.Log(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, formatterCalls);
    }

    [DynamicData(nameof(LogTestHelpers.GetLogLevelsForDynamicData), typeof(LogTestHelpers))]
    [TestMethod]
    public async ValueTask NopLogger_LogAsync_NoFormatterCalls(LogLevel logLevel)
    {
        int formatterCalls = 0;
        string Formatter(string state, Exception? exception)
        {
            formatterCalls++;
            return string.Empty;
        }

        await _nopLogger.LogAsync(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, formatterCalls);
    }
}
