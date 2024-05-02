﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class NopLoggerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
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

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers))]
    public void NopLogger_CheckDisabled(LogLevel logLevel)
        => Assert.IsFalse(_nopLogger.IsEnabled(logLevel));

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers))]
    public void NopLogger_Log_NoFormatterCalls(LogLevel logLevel)
    {
        _nopLogger.Log(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers))]
    public async ValueTask NopLogger_LogAsync_NoFormatterCalls(LogLevel logLevel)
    {
        await _nopLogger.LogAsync(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }
}
