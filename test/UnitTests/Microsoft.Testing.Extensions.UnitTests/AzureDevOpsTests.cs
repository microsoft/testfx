// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK || NET472_OR_GREATER
using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Platform.Helpers;
#endif
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsTests
{
    [TestMethod]
    public void ReportsTheFirstExistingFileInStackTraceWithTheRightLineNumberAndEscaping()
    {
#if NETFRAMEWORK && !NET472_OR_GREATER
        // We rely on code file paths that are present in pdb for this project. We use portable PDBs, which don't report the code location for
        // .NET Framework <4.7.2, so we won't get the path and the test will fail.
        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/symbols#support-for-portable-pdbs
        return;
#else
        Exception error;
        try
        {
            throw new Exception("this is an error\nwith\rnewline");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ## so when the test fails we don't report it to AzDO, the severity is invalid, and the result is confusing.
        var logger = new TextLogger();
        string? text = AzureDevOpsReporter.GetErrorText(null, error, "severity", new SystemFileSystem(), logger)?.Trim('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=27;columnnumber=1]this is an error%0Awith%0Dnewline", text, $"\nLogs:\n{string.Join("\n", logger.Logs)}");
#endif
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
