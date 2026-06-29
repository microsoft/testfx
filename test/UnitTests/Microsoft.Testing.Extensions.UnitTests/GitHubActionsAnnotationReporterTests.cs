// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsAnnotationReporterTests
{
    [TestMethod]
    public void GetErrorAnnotation_ReportsFirstExistingFileWithLineColTitleAndEscaping()
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

        var logger = new NoopLogger();
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", null, error, GitHubActionsRepositoryRoot.FindGitRoot(), new SystemFileSystem(), logger);

        Assert.AreEqual(
            "::error file=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/GitHubActionsAnnotationReporterTests.cs,line=19,col=1,title=Test failed%3A MyNamespace.MyTest::this is an error%0Awith%0Dnewline",
            text);
    }

    [TestMethod]
    public void GetErrorAnnotation_PrefersExplanationOverExceptionMessage()
    {
        Exception error;
        try
        {
            throw new Exception("exception message");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var logger = new NoopLogger();
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", "Some custom reason\nwith\rnewline", error, GitHubActionsRepositoryRoot.FindGitRoot(), new SystemFileSystem(), logger);

        Assert.AreEqual(
            "::error file=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/GitHubActionsAnnotationReporterTests.cs,line=40,col=1,title=Test failed%3A MyNamespace.MyTest::Some custom reason%0Awith%0Dnewline",
            text);
    }

    [TestMethod]
    public void GetErrorAnnotation_FallsBackToTitleOnly_WhenNoSourceLocation()
    {
        var logger = new NoopLogger();
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", "boom", exception: null, repoRoot: null, new SystemFileSystem(), logger);

        Assert.AreEqual("::error title=Test failed%3A MyNamespace.MyTest::boom", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_UsesFallbackMessage_WhenNoExplanationOrException()
    {
        var logger = new NoopLogger();
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", explanation: null, exception: null, repoRoot: null, new SystemFileSystem(), logger);

        Assert.IsTrue(text.StartsWith("::error title=Test failed%3A MyNamespace.MyTest::", StringComparison.Ordinal), text);
    }

    private sealed class NoopLogger : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Task.CompletedTask;
    }
}
