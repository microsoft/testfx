// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Platform.Helpers;

using Moq;

using GitHubActionsTerminalKind = ghactions::Microsoft.Testing.Extensions.TerminalKind;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsSummaryReporterTests
{
    // ExitCode.Success (0): a normal passing run — no exit-code callout expected.
    private const int SuccessExitCode = 0;

    // ExitCode.AtLeastOneTestFailed (2): failures are conveyed by the table/list, not a callout.
    private const int AtLeastOneTestFailedExitCode = 2;

    // ExitCode.ZeroTests (8) and MinimumExpectedTestsPolicyViolation (9): non-test-result failures.
    private const int ZeroTestsExitCode = 8;
    private const int MinimumExpectedTestsExitCode = 9;

    [TestMethod]
    public void BuildMarkdown_AllPassing_UsesSuccessIconAndTotals()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Add", "CalculatorTests.Add", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Sub", "CalculatorTests.Sub", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(20)),
            new("Skip", "CalculatorTests.Skip", GitHubActionsTerminalKind.Skipped, TimeSpan.Zero),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", SuccessExitCode);

        Assert.Contains("## ✅ Test Run Summary — CalculatorTests (net9.0)", markdown);
        Assert.Contains("| 3 | 2 | 0 | 1 | 30ms |", markdown);
        Assert.DoesNotContain("### ❌ Failures", markdown);
        Assert.DoesNotContain("[!WARNING]", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailures_UsesFailureIconAndListsFailures()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Pass", "StringUtilsTests.Pass", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(5)),
            new("Boom", "StringUtilsTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(7)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "StringUtilsTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("## ❌ Test Run Summary — StringUtilsTests (net9.0)", markdown);
        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("- `StringUtilsTests.Boom`", markdown);

        // A plain "at least one test failed" outcome is conveyed by the failures list, not an exit-code callout.
        Assert.DoesNotContain("[!WARNING]", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_EmitsSlowestTestsSortedByDuration()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Fast", "T.Fast", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Slow", "T.Slow", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(65)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", SuccessExitCode);

        Assert.Contains("### ⏱ Slowest tests", markdown);
        int slowIndex = markdown.IndexOf("- `T.Slow` — 1m 05s", StringComparison.Ordinal);
        int fastIndex = markdown.IndexOf("- `T.Fast` — 10ms", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, slowIndex, markdown);
        Assert.IsGreaterThanOrEqualTo(0, fastIndex, markdown);

        // Slowest-first ordering: the slow test must be listed before the fast one, i.e. at a smaller index.
        // IsLessThan(upperBound, value) asserts value < upperBound, so this asserts slowIndex < fastIndex.
        Assert.IsLessThan(fastIndex, slowIndex, markdown);
    }

    [TestMethod]
    public void BuildMarkdown_NoTests_StillEmitsHeaderAndZeroTotals()
    {
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown([], "Empty", "net9.0", SuccessExitCode);

        Assert.Contains("## ✅ Test Run Summary — Empty (net9.0)", markdown);
        Assert.Contains("| 0 | 0 | 0 | 0 | 0ms |", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_ZeroTestsExitCode_UsesFailureIconAndEmitsCallout()
    {
        // No failing tests, but the process exit code says the run failed because nothing ran.
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown([], "Empty", "net9.0", ZeroTestsExitCode);

        Assert.Contains("## ❌ Test Run Summary — Empty (net9.0)", markdown);
        Assert.Contains("> [!WARNING]", markdown);
        Assert.Contains("Exit code 8 — ZeroTests:", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_MinimumExpectedTestsExitCode_EmitsCalloutEvenWhenTestsPassed()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Add", "CalculatorTests.Add", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0", MinimumExpectedTestsExitCode);

        // The single test passed, yet the run failed the minimum-expected-tests policy: icon and callout reflect it.
        Assert.Contains("## ❌ Test Run Summary — CalculatorTests (net9.0)", markdown);
        Assert.Contains("Exit code 9 — MinimumExpectedTestsPolicyViolation:", markdown);
        Assert.Contains("--minimum-expected-tests", markdown);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_WritesContent_OnFirstAttempt()
    {
        var buffer = new MemoryStream();
        Mock<IFileSystem> fileSystem = CreateFileSystemWritingTo(buffer);

        await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "hello world", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None);

        // UTF8Encoding(false) is used by the reporter, so there is no BOM to strip.
        Assert.AreEqual("hello world", Encoding.UTF8.GetString(buffer.ToArray()));
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Once);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_RetriesOnSharingViolation_ThenSucceeds()
    {
        var buffer = new MemoryStream();
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(buffer);

        var fileSystem = new Mock<IFileSystem>();
        // First open loses the race against another process (sharing violation), the second one wins.
        fileSystem.SetupSequence(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("The process cannot access the file because it is being used by another process."))
            .Returns(fileStream.Object);

        await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "second-wins", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None);

        Assert.AreEqual("second-wins", Encoding.UTF8.GetString(buffer.ToArray()));
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Exactly(2));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_Rethrows_WhenAllAttemptsFail()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("locked"));

        // After exhausting the bounded attempts the final IOException propagates so the caller can surface its
        // best-effort warning rather than looping forever.
        await Assert.ThrowsExactlyAsync<IOException>(() => GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "never-written", maxAttempts: 3, retryDelay: TimeSpan.Zero, CancellationToken.None));

        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Exactly(3));
    }

    private static Mock<IFileSystem> CreateFileSystemWritingTo(Stream target)
    {
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(target);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object);
        return fileSystem;
    }
}
