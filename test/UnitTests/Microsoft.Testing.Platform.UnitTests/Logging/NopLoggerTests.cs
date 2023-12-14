// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class NopLoggerTests : TestBase
{
    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
        {
            ++s_formatterCalls;
            return string.Empty;
        };

    private const string Message = "DummyMessage";
    private readonly Exception _exception = new("TestException");
    private readonly NopLogger _nopLogger;

    private static int s_formatterCalls;

    public NopLoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _nopLogger = new NopLogger();
    }

    public void NopLogger_CheckDisabled()
    {
        Assert.IsFalse(_nopLogger.IsEnabled(LogLevel.Information));
        Assert.IsFalse(_nopLogger.IsEnabled(LogLevel.Error));
        Assert.IsFalse(_nopLogger.IsEnabled(LogLevel.Trace));
    }

    public void NopLogger_Log_NoFormatterCalls()
    {
        _nopLogger.Log(LogLevel.Error, Message, _exception, Formatter);
        _nopLogger.Log(LogLevel.Information, Message, null, Formatter);

        Assert.AreEqual(0, s_formatterCalls);
    }

    public async ValueTask NopLogger_LogAsync_NoFormatterCalls()
    {
        await _nopLogger.LogAsync(LogLevel.Error, Message, _exception, Formatter);
        await _nopLogger.LogAsync(LogLevel.Information, Message, null, Formatter);

        Assert.AreEqual(0, s_formatterCalls);
    }
}
