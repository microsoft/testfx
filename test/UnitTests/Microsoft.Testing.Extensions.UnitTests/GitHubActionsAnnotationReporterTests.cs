// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsAnnotationReporterTests
{
    [TestMethod]
    public void GetErrorAnnotation_ReportsResolvedFileWithLineColTitleAndEscaping()
    {
        Exception error = CaptureException("this is an error\nwith\rnewline", out int throwLine);

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "MyNamespace.MyTest", explanation: null, error, GitHubActionsRepositoryRoot.FindGitRoot(), CreateFileSystemWhereEveryFileExists(), new NoopLogger());

        // The line is computed dynamically (from where the throw actually executes) rather than hard-coded, and the
        // file existence is mocked, so the assertion does not depend on this file's exact layout or the physical
        // repo checkout.
        Assert.IsTrue(text.StartsWith("::error file=", StringComparison.Ordinal), text);
        Assert.Contains($"GitHubActionsAnnotationReporterTests.cs,line={throwLine},col=1,title=Test failed%3A MyNamespace.MyTest::", text);
        Assert.IsTrue(text.EndsWith("this is an error%0Awith%0Dnewline", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void GetErrorAnnotation_PrefersExplanationOverExceptionMessage()
    {
        Exception error = CaptureException("exception message", out int throwLine);

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "MyNamespace.MyTest", "Some custom reason\nwith\rnewline", error, GitHubActionsRepositoryRoot.FindGitRoot(), CreateFileSystemWhereEveryFileExists(), new NoopLogger());

        Assert.IsTrue(text.StartsWith("::error file=", StringComparison.Ordinal), text);
        Assert.Contains($"GitHubActionsAnnotationReporterTests.cs,line={throwLine},col=1,title=Test failed%3A MyNamespace.MyTest::", text);
        Assert.IsTrue(text.EndsWith("Some custom reason%0Awith%0Dnewline", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void GetErrorAnnotation_FallsBackToTitleOnly_WhenNoSourceLocation()
    {
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", "boom", exception: null, repoRoot: null, CreateFileSystemWhereEveryFileExists(), new NoopLogger());

        Assert.AreEqual("::error title=Test failed%3A MyNamespace.MyTest::boom", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_UsesFallbackMessage_WhenNoExplanationOrException()
    {
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", explanation: null, exception: null, repoRoot: null, CreateFileSystemWhereEveryFileExists(), new NoopLogger());

        Assert.IsTrue(text.StartsWith("::error title=Test failed%3A MyNamespace.MyTest::", StringComparison.Ordinal), text);
    }

    // Throws (and catches) an exception, reporting the exact line of the throw statement so tests can assert the
    // resolved line without hard-coding a physical number that shifts whenever code above changes.
    private static Exception CaptureException(string message, out int throwLine)
    {
        throwLine = 0;
        try
        {
            throwLine = CurrentLine() + 1;
            throw new Exception(message);
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static int CurrentLine([CallerLineNumber] int line = 0) => line;

    private static IFileSystem CreateFileSystemWhereEveryFileExists()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns(true);
        return fileSystem.Object;
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
