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
    private readonly NopLogger _nopLogger = new();

    private static int s_formatterCalls;

    [Arguments(LogLevel.Trace)]
    [Arguments(LogLevel.Debug)]
    [Arguments(LogLevel.Information)]
    [Arguments(LogLevel.Warning)]
    [Arguments(LogLevel.Error)]
    [Arguments(LogLevel.Critical)]
    public void NopLogger_CheckDisabled(LogLevel logLevel)
    {
        Assert.IsFalse(_nopLogger.IsEnabled(logLevel));
    }

    [Arguments(LogLevel.Trace)]
    [Arguments(LogLevel.Debug)]
    [Arguments(LogLevel.Information)]
    [Arguments(LogLevel.Warning)]
    [Arguments(LogLevel.Error)]
    [Arguments(LogLevel.Critical)]
    public void NopLogger_Log_NoFormatterCalls(LogLevel logLevel)
    {
        _nopLogger.Log(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }

    [Arguments(LogLevel.Trace)]
    [Arguments(LogLevel.Debug)]
    [Arguments(LogLevel.Information)]
    [Arguments(LogLevel.Warning)]
    [Arguments(LogLevel.Error)]
    [Arguments(LogLevel.Critical)]
    public async ValueTask NopLogger_LogAsync_NoFormatterCalls(LogLevel logLevel)
    {
        await _nopLogger.LogAsync(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }
}
