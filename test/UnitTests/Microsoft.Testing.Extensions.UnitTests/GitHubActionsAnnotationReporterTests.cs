// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsAnnotationReporterTests
{
    [TestMethod]
    public void AppendHistoryContext_AddsPriorFailureCounts()
    {
        GitHubActionsAnnotationReporter reporter = CreateReporter(
            new GitHubActionsHistoryStats(passCount: 7, failCount: 3));

        string? message = reporter.AppendHistoryContext(
            "flaky-id",
            "Tests.Flaky",
            "Flaky",
            "boom",
            exception: null);

        Assert.AreEqual("boom Historical context: failed 3 and flaked 0 of 10 prior runs within the 14-day history window.", message);
    }

    [TestMethod]
    public void AppendHistoryContext_AddsFlakyOnlyContext()
    {
        GitHubActionsAnnotationReporter reporter = CreateReporter(
            new GitHubActionsHistoryStats(passCount: 6, failCount: 0, flakyCount: 2));

        string? message = reporter.AppendHistoryContext(
            "flaky-id",
            "Tests.Flaky",
            "Flaky",
            "boom",
            exception: null);

        Assert.AreEqual("boom Historical context: flaked 2 of 6 prior runs within the 14-day history window.", message);
    }

    [TestMethod]
    [DataRow(4, "boom")]
    [DataRow(5, "boom Historical context: passed all 5 prior runs within the 14-day history window.")]
    public void AppendHistoryContext_AppliesRegressionSampleBoundary(int passCount, string expected)
    {
        GitHubActionsAnnotationReporter reporter = CreateReporter(
            new GitHubActionsHistoryStats(passCount, failCount: 0));

        string? message = reporter.AppendHistoryContext(
            "stable-id",
            "Tests.Stable",
            "Stable",
            "boom",
            exception: null);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    public void AppendHistoryContext_AddsHistoricalDuration()
    {
        GitHubActionsAnnotationReporter reporter = CreateReporter(
            new GitHubActionsHistoryStats(
                passCount: 5,
                failCount: 0,
                p95DurationTicks: TimeSpan.FromSeconds(2).Ticks,
                p99DurationTicks: TimeSpan.FromSeconds(3).Ticks,
                durationSampleCount: 20));

        string? message = reporter.AppendHistoryContext(
            "stable-id",
            "Tests.Stable",
            "Stable",
            "boom",
            exception: null);

        Assert.AreEqual(
            "boom Historical context: passed all 5 prior runs within the 14-day history window. Historical duration: p95 2.00s, p99 3.00s across 20 prior samples.",
            message);
    }

    [TestMethod]
    public void GetErrorAnnotation_ReportsResolvedFileWithLineColTitleAndEscaping()
    {
        Exception error = CaptureException("this is an error\nwith\rnewline", out int throwLine);

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "MyNamespace.MyTest", explanation: null, error, GitHubActionsRepositoryRoot.FindGitRoot(), CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

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
            "MyNamespace.MyTest", "Some custom reason\nwith\rnewline", error, GitHubActionsRepositoryRoot.FindGitRoot(), CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

        Assert.IsTrue(text.StartsWith("::error file=", StringComparison.Ordinal), text);
        Assert.Contains($"GitHubActionsAnnotationReporterTests.cs,line={throwLine},col=1,title=Test failed%3A MyNamespace.MyTest::", text);
        Assert.IsTrue(text.EndsWith("Some custom reason%0Awith%0Dnewline", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void GetErrorAnnotation_FallsBackToTitleOnly_WhenNoSourceLocation()
    {
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", "boom", exception: null, repoRoot: null, CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

        Assert.AreEqual("::error title=Test failed%3A MyNamespace.MyTest::boom", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_RemapsDeterministicBuildRootPathToWorkspaceRelative()
    {
        // A frame emitted from a deterministic (source-linked) build carries the '/_/' root marker; the reporter
        // must strip it and produce a forward-slash workspace-relative path regardless of the repo root value.
        var exception = new StackTraceException("   at Contoso.Calc.Add() in /_/src/Calc.cs:line 12");

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "Contoso.CalcTests.Add", "boom", exception, repoRoot: "/repo/", CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

        Assert.AreEqual("::error file=src/Calc.cs,line=12,col=1,title=Test failed%3A Contoso.CalcTests.Add::boom", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_SkipsAssertionFramesAndAnnotatesUserCallSite()
    {
        // The top frame is an MSTest assertion implementation and must be skipped in favour of the user's call site.
        var exception = new StackTraceException(
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail(string message) in /_/assert/Assert.cs:line 1\n"
            + "   at Contoso.MyTests.TheTest() in /_/src/MyTests.cs:line 7");

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "Contoso.MyTests.TheTest", "nope", exception, repoRoot: "/repo/", CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

        Assert.AreEqual("::error file=src/MyTests.cs,line=7,col=1,title=Test failed%3A Contoso.MyTests.TheTest::nope", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_UsesFallbackMessage_WhenNoExplanationOrException()
    {
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation("MyNamespace.MyTest", explanation: null, exception: null, repoRoot: null, CreateFileSystemWhereEveryFileExists(), new NoopLogger(), skipAssertionFrames: true);

        Assert.IsTrue(text.StartsWith("::error title=Test failed%3A MyNamespace.MyTest::", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void GetErrorAnnotation_FallsBackToDeclaredLocation_WhenStackTraceHasNoUsableFrame()
    {
        // No exception at all (a framework that reports a failure without one), but the test node carried a
        // declared location: the annotation must still be pinned to the test's declaration.
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "Contoso.MyTests.TheTest",
            "boom",
            exception: null,
            repoRoot: "/repo/",
            CreateFileSystemWhereEveryFileExists(),
            new NoopLogger(),
            skipAssertionFrames: true,
            new GitHubActionsSourceLocation("src/MyTests.cs", 42));

        Assert.AreEqual("::error file=src/MyTests.cs,line=42,col=1,title=Test failed%3A Contoso.MyTests.TheTest::boom", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_PrefersStackTraceOverDeclaredLocation()
    {
        // The stack frame points at the failing statement, which is more precise than the test declaration.
        var exception = new StackTraceException("   at Contoso.MyTests.TheTest() in /_/src/MyTests.cs:line 7");

        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "Contoso.MyTests.TheTest",
            "nope",
            exception,
            repoRoot: "/repo/",
            CreateFileSystemWhereEveryFileExists(),
            new NoopLogger(),
            skipAssertionFrames: true,
            new GitHubActionsSourceLocation("src/MyTests.cs", 1));

        Assert.AreEqual("::error file=src/MyTests.cs,line=7,col=1,title=Test failed%3A Contoso.MyTests.TheTest::nope", text);
    }

    [TestMethod]
    public void GetErrorAnnotation_OmitsLine_WhenDeclaredLocationHasNoLine()
    {
        string text = GitHubActionsAnnotationReporter.GetErrorAnnotation(
            "Contoso.MyTests.TheTest",
            "boom",
            exception: null,
            repoRoot: "/repo/",
            CreateFileSystemWhereEveryFileExists(),
            new NoopLogger(),
            skipAssertionFrames: true,
            new GitHubActionsSourceLocation("src/MyTests.cs", 0));

        Assert.AreEqual("::error file=src/MyTests.cs,title=Test failed%3A Contoso.MyTests.TheTest::boom", text);
    }

    [TestMethod]
    public void GetSkippedAnnotation_EmitsTitleOnlyWarningWithReasonAndEscaping()
    {
        string text = GitHubActionsAnnotationReporter.GetSkippedAnnotation("MyNamespace.MyTest", "not today\nmaybe\rlater");

        Assert.AreEqual("::warning title=Test skipped%3A MyNamespace.MyTest::not today%0Amaybe%0Dlater", text);
    }

    [TestMethod]
    public void GetSkippedAnnotation_PinsWarningToDeclaredLocation_WhenAvailable()
    {
        string text = GitHubActionsAnnotationReporter.GetSkippedAnnotation(
            "Contoso.MyTests.TheTest", "not today", new GitHubActionsSourceLocation("src/MyTests.cs", 12));

        Assert.AreEqual("::warning file=src/MyTests.cs,line=12,col=1,title=Test skipped%3A Contoso.MyTests.TheTest::not today", text);
    }

    [TestMethod]
    public void TryResolveDeclaredLocation_ReturnsNull_WhenTestNodeHasNoFileLocation()
    {
        var testNode = new TestNode { Uid = "uid", DisplayName = "TheTest" };

        Assert.IsNull(GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, "/repo/", CreateFileSystemWhereEveryFileExists()));
    }

    [TestMethod]
    public void TryResolveDeclaredLocation_ReturnsWorkspaceRelativePathAndLine()
    {
        var testNode = new TestNode { Uid = "uid", DisplayName = "TheTest" };
        var position = new LinePosition(21, -1);
        testNode.Properties.Add(new TestFileLocationProperty("/repo/src/MyTests.cs", new LinePositionSpan(position, position)));

        GitHubActionsSourceLocation? location = GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, "/repo/", CreateFileSystemWhereEveryFileExists());

        Assert.IsNotNull(location);
        GitHubActionsSourceLocation resolvedLocation = location!.Value;
        Assert.AreEqual("src/MyTests.cs", resolvedLocation.RelativeNormalizedPath);
        Assert.AreEqual(21, resolvedLocation.LineNumber);
    }

    [TestMethod]
    public void TryResolveDeclaredLocation_NormalizesUnknownLineToZero()
    {
        // Both the MSTest adapter and the VSTest bridge use -1 as the "line unknown" sentinel.
        var testNode = new TestNode { Uid = "uid", DisplayName = "TheTest" };
        var position = new LinePosition(-1, -1);
        testNode.Properties.Add(new TestFileLocationProperty("/repo/src/MyTests.cs", new LinePositionSpan(position, position)));

        GitHubActionsSourceLocation? location = GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, "/repo/", CreateFileSystemWhereEveryFileExists());

        Assert.IsNotNull(location);
        GitHubActionsSourceLocation resolvedLocation = location!.Value;
        Assert.AreEqual(0, resolvedLocation.LineNumber);
    }

    [TestMethod]
    public void TryResolveDeclaredLocation_ReturnsNull_WhenFileIsOutsideTheWorkspace()
    {
        var testNode = new TestNode { Uid = "uid", DisplayName = "TheTest" };
        var position = new LinePosition(21, -1);
        testNode.Properties.Add(new TestFileLocationProperty("/elsewhere/src/MyTests.cs", new LinePositionSpan(position, position)));

        Assert.IsNull(GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, "/repo/", CreateFileSystemWhereEveryFileExists()));
    }

    [TestMethod]
    public void TryResolveDeclaredLocation_ReturnsNull_WhenFileDoesNotExistOnDisk()
    {
        var testNode = new TestNode { Uid = "uid", DisplayName = "TheTest" };
        var position = new LinePosition(21, -1);
        testNode.Properties.Add(new TestFileLocationProperty("/repo/src/MyTests.cs", new LinePositionSpan(position, position)));

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns(false);

        Assert.IsNull(GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, "/repo/", fileSystem.Object));
    }

    [TestMethod]
    public void GetSkippedAnnotation_FallsBackToDefaultReason_WhenNoExplanation()
    {
        string text = GitHubActionsAnnotationReporter.GetSkippedAnnotation("MyNamespace.MyTest", explanation: null);

        Assert.AreEqual("::warning title=Test skipped%3A MyNamespace.MyTest::The test was skipped without providing a reason.", text);
    }

    // Produces an exception whose stack trace deterministically points at this test file at a fixed line, so
    // tests can assert the resolved file and line. A real 'throw' was previously used, but the runtime-reported
    // line of a thrown exception shifts under Release JIT optimization (observed differing between .NET
    // Framework net462 and net472), which made the exact-line assertion flaky in CI (see #9658). A synthetic
    // frame keeps both the resolved file and line stable while still exercising the resolver's real
    // repo-root/'/_/' path relativization: [CallerFilePath] yields this file's path (mapped to '/_/test/...'
    // in deterministic CI builds, or an absolute path locally), exactly as a genuine frame would.
    private static Exception CaptureException(string message, out int throwLine, [CallerFilePath] string filePath = "")
    {
        throwLine = 12345;
        return new StackTraceException(
            $"   at Microsoft.Testing.Extensions.UnitTests.GitHubActionsAnnotationReporterTests.CaptureException() in {filePath}:line {throwLine}",
            message);
    }

    private static IFileSystem CreateFileSystemWhereEveryFileExists()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns(true);
        return fileSystem.Object;
    }

    private static GitHubActionsAnnotationReporter CreateReporter(GitHubActionsHistoryStats stats)
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(item => item.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        return new GitHubActionsAnnotationReporter(
            new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
            }),
            environment.Object,
            Mock.Of<IFileSystem>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ITestApplicationProcessExitCode>(),
            loggerFactory.Object,
            new FakeHistoryService(stats, historyWindowInDays: 14));
    }

    // Exception whose StackTrace is a caller-supplied synthetic string, letting tests exercise the
    // frame-parsing/path-remapping branches deterministically without relying on real PDB-derived paths.
    private sealed class StackTraceException : Exception
    {
        private readonly string _stackTrace;

        public StackTraceException(string stackTrace, string? message = null)
            : base(message)
            => _stackTrace = stackTrace;

        public override string? StackTrace => _stackTrace;
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

    private sealed class FakeHistoryService(
        GitHubActionsHistoryStats stats,
        int historyWindowInDays) : IGitHubActionsHistoryService
    {
        public bool IsEnabled => true;

        public string? HistoryPath => null;

        public int HistoryWindowInDays { get; } = historyWindowInDays;

        public bool TryGetStats(
            string testId,
            string fullyQualifiedName,
            string displayName,
            out GitHubActionsHistoryStats result)
        {
            result = stats;
            return true;
        }

        public Task WriteAsync(
            IReadOnlyList<ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule> modules,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
