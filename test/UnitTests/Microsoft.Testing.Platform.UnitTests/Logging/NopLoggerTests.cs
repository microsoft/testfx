// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class NopLoggerTests
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

    [DynamicData(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers), DynamicDataSourceType.Method)]
    [TestMethod]
    public void NopLogger_CheckDisabled(LogLevel logLevel)
        => Assert.IsFalse(_nopLogger.IsEnabled(logLevel));

    [DynamicData(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers), DynamicDataSourceType.Method)]
    [TestMethod]
    public void NopLogger_Log_NoFormatterCalls(LogLevel logLevel)
    {
        _nopLogger.Log(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }

    [DynamicData(nameof(LogTestHelpers.GetLogLevels), typeof(LogTestHelpers), DynamicDataSourceType.Method)]
    [TestMethod]
    public async ValueTask NopLogger_LogAsync_NoFormatterCalls(LogLevel logLevel)
    {
        await _nopLogger.LogAsync(logLevel, Message, _exception, Formatter);
        Assert.AreEqual(0, s_formatterCalls);
    }
}
