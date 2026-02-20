// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsTests
{
    [TestMethod]
    public void ReportsTheFirstExistingFileInStackTraceWithTheRightLineNumberAndEscaping()
    {
        Exception error;
        try
        {
            throw new Exception("this is an error\nwith\rnewline");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ##. If we keep it, then when the test fails, the assertion failure will get printed to screen and picked up incorrectly by AzDO, because it scans all output for the ##vso... pattern
        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText("MyTestDisplayName", null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=19;columnnumber=1][MyTestDisplayName] [net9.0] this is an error%0Awith%0Dnewline", text, $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    [TestMethod]
    public void ReportsWithoutDisplayNameWhenNull()
    {
        Exception error;
        try
        {
            throw new Exception("this is an error");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ##. If we keep it, then when the test fails, the assertion failure will get printed to screen and picked up incorrectly by AzDO, because it scans all output for the ##vso... pattern
        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText(null, null, error, "severity", new SystemFileSystem(), logger, "net9.0")?.TrimStart('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=38;columnnumber=1][net9.0] this is an error", text, $"\nLogs:\n{string.Join("\n", logger.Logs)}");
    }

    private class TextLogger : ILogger
    {
        public List<string> Logs { get; } = [];

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");
            return Task.CompletedTask;
        }
    }
}
