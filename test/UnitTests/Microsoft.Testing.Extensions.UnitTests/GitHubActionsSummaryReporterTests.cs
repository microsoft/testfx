// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

using GitHubActionsTerminalKind = ghactions::Microsoft.Testing.Extensions.TerminalKind;
using GitHubActionsTestFailureDetails = ghactions::Microsoft.Testing.Extensions.TestFailureDetails;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;
using GitHubCiRunSummaryAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubCiRunSummaryAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubCiRunSummaryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;

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
    public void BuildAggregateMarkdown_HtmlEncodesModuleSummaryLabel()
    {
        var module = new GitHubCiRunSummaryModule
        {
            AssemblyName = "<h1>A&B</h1>",
            ModulePath = "A.dll",
            TargetFramework = "net9.0<&>",
            Architecture = "x64&arm64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
            [module],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 0,
            passedTests: 0,
            failedTests: 0,
            skippedTests: 0,
            duration: null,
            exitCode: null,
            hasAuthoritativeRunSummary: false,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("<summary>&lt;h1&gt;A&amp;B&lt;/h1&gt; (net9.0&lt;&amp;&gt;, x64&amp;arm64)</summary>", markdown);
        Assert.DoesNotContain("<summary><h1>", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_PartialFailureUsesFailureIconAndDisambiguatesAttempts()
    {
        var first = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-1",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            FailedTests = 1,
            TotalTests = 1,
        };
        var second = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "other/Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session-2",
            AttemptNumber = 2,
            ExitCode = SuccessExitCode,
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
            [first, second],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.MaximumFailedTests),
            totalTests: 1,
            passedTests: 0,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: true);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.StartsWith("## ❌ Overall Test Run Summary", markdown);
        Assert.Contains("attempt 1, session session-1", markdown);
        Assert.Contains("attempt 2, session session-2", markdown);
        Assert.Contains("This summary is partial because the test run was truncated.", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailureDetails_RendersCollapsibleSection()
    {
        var failure = new GitHubActionsTestFailureDetails(
            "Expected: 42\nActual:   41",
            "System.Exception",
            "   at Calc.Add() in Calc.cs:line 42",
            "src/Calc.cs",
            42);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(2400), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("<details>\n<summary><code>CalcTests.Boom</code> — 2.40s</summary>", markdown);
        Assert.Contains("**Exception:** `System.Exception`", markdown);
        Assert.Contains("**Location:** `src/Calc.cs:42`", markdown);
        Assert.Contains("Expected: 42", markdown);
        Assert.Contains("at Calc.Add() in Calc.cs:line 42", markdown);
        Assert.Contains("</details>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailureDetailsDisabled_KeepsCompactFailureList()
    {
        var failure = new GitHubActionsTestFailureDetails("boom", "System.Exception", "at X()", "src/X.cs", 3);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(120), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: false);

        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("- `CalcTests.Boom` — 120ms", markdown);
        Assert.DoesNotContain("<details>", markdown);
        Assert.DoesNotContain("**Exception:**", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_FailureWithoutDetails_FallsBackToCompactLine()
    {
        // A framework that reports a failure without an explanation, exception or location has nothing to expand.
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "CalcTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(5)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalcTests", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("- `CalcTests.Boom` — 5ms", markdown);
        Assert.DoesNotContain("<details>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_HtmlEncodesFailureNameInSummaryElement()
    {
        // A generic test name would otherwise be parsed as an HTML tag and swallow the rest of the <summary> line.
        var failure = new GitHubActionsTestFailureDetails("boom", null, null, null, 0);
        GitHubActionsTestRecord[] records =
        [
            new("Map", "T.Map<string,int>", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("<code>T.Map&lt;string,int&gt;</code>", markdown);
        Assert.DoesNotContain("<code>T.Map<string,int></code>", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_FailureMessageContainingCodeFence_DoesNotBreakOutOfTheBlock()
    {
        // A message that itself contains a ``` fence must not terminate the block we open around it.
        var failure = new GitHubActionsTestFailureDetails("before\n```\ninjected\n```\nafter", null, null, null, 0);
        GitHubActionsTestRecord[] records =
        [
            new("Boom", "T.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1), failure),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("````text", markdown);
        Assert.Contains("injected", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_TruncatesFailureList_AndSaysSo()
    {
        // 25 failures exceed the 20-failure cap, so the summary must state that the list was truncated.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 25).Select(i =>
                new GitHubActionsTestRecord($"T{i}", $"T.Test{i}", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(1))),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode);

        Assert.Contains("### ❌ Failures (25)", markdown);
        Assert.Contains("Showing the first 20 of 25 failed tests", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_DegradesOversizedFailuresToNamedLines_KeepingEveryFailureListed()
    {
        // Every failure carries a stack trace far larger than the supplied budget, so only the first few can be
        // expanded. The rest must degrade to a named line rather than disappearing: which tests failed is the
        // part a reader cannot reconstruct, and it costs a line where the diagnostics cost kilobytes.
        string hugeStackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 100), GitHubActionsFailureDetails.MaxStackTraceRows));
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 20).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", hugeStackTrace, null, 0))),
        ];

        // Room for roughly two of the ~3.2 KB blocks, so the remaining failures must degrade.
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, detailsBudget: 7000);

        Assert.Contains("<details>", markdown);
        Assert.IsLessThan(records.Length, CountOccurrences(markdown, "<details>"));

        // Every failure is still named, expanded or not.
        foreach (GitHubActionsTestRecord record in records)
        {
            Assert.Contains(record.FullyQualifiedName, markdown);
        }
    }

    [TestMethod]
    public void BuildMarkdown_ExhaustedBudget_ListsFailuresAndSaysTheirDetailsWereOmitted()
    {
        // With no budget at all the section is a plain list. It must still say why the diagnostics are missing,
        // so a bare list is not mistaken for failures that had nothing more to show.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 3).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", "at T.Test()", null, 0))),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, detailsBudget: 0);

        Assert.DoesNotContain("<details>", markdown);
        Assert.Contains("Failure details for 3 listed test(s) were omitted", markdown);
        Assert.Contains("T.Test0", markdown);
        Assert.Contains("T.Test2", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_SharesTheBudgetAcrossFailures_SoASmallerBudgetExpandsFewer()
    {
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 100), 20));
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 10).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"T.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails("boom", "System.Exception", stackTrace, null, 0))),
        ];

        int generous = CountOccurrences(
            GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, detailsBudget: 100_000),
            "<details>");
        int tight = CountOccurrences(
            GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0", AtLeastOneTestFailedExitCode, includeFailureDetails: true, detailsBudget: 5_000),
            "<details>");

        Assert.AreEqual(10, generous);
        Assert.IsLessThan(generous, tight);
    }

    [TestMethod]
    public void Clip_ManyShortRows_IsTruncatedByRowCountEvenWhenUnderTheCharacterLimit()
    {
        // 200 one-word frames are only ~2,600 characters — under the character cap — yet far too long to read.
        // The row limit is what bounds this shape.
        string manyRows = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"at Frame{i}()"));
        Assert.IsLessThan(GitHubActionsFailureDetails.MaxStackTraceLength, manyRows.Length, "The input must be under the character cap for this test to be meaningful.");

        string clipped = GitHubActionsFailureDetails.Clip(manyRows, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows)!;

        Assert.Contains("[... truncated]", clipped);

        // The kept rows plus the truncation marker.
        Assert.HasCount(GitHubActionsFailureDetails.MaxStackTraceRows + 1, clipped.Split('\n'));
        Assert.Contains("at Frame0()", clipped);
        Assert.DoesNotContain("at Frame199()", clipped);
    }

    [TestMethod]
    public void Clip_RowCountUnderLimit_IsNotMarkedTruncated()
    {
        string fewRows = "line 1\nline 2\nline 3";

        string clipped = GitHubActionsFailureDetails.Clip(fewRows, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows)!;

        Assert.AreEqual(fewRows, clipped);
        Assert.DoesNotContain("[... truncated]", clipped);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_MissingFile_ReturnsFullBudget()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(false);

        int budget = GitHubActionsSummaryReporter.GetRemainingDetailsBudget(fileSystem.Object, "summary.md", new Mock<ILogger>().Object);

        Assert.AreEqual(GitHubActionsFailureDetails.MaxTotalDetailsLength, budget);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_ExistingContent_IsSubtracted()
    {
        // A sibling test project already wrote to the shared summary file; this project may only claim the rest.
        const int AlreadyWritten = 100_000;
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(new MemoryStream(new byte[AlreadyWritten]));

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(true);
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Returns(fileStream.Object);

        int budget = GitHubActionsSummaryReporter.GetRemainingDetailsBudget(fileSystem.Object, "summary.md", new Mock<ILogger>().Object);

        Assert.AreEqual(GitHubActionsFailureDetails.MaxTotalDetailsLength - AlreadyWritten, budget);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_FileAlreadyOverBudget_ReturnsZero()
    {
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(new MemoryStream(new byte[GitHubActionsFailureDetails.MaxTotalDetailsLength + 1]));

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(true);
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Returns(fileStream.Object);

        int budget = GitHubActionsSummaryReporter.GetRemainingDetailsBudget(fileSystem.Object, "summary.md", new Mock<ILogger>().Object);

        // Never negative: the caller uses this as a length, and a later project simply renders compact lines.
        Assert.AreEqual(0, budget);
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_UnreadableFile_FallsBackToFullBudget()
    {
        // Measuring the file is an optimization; failing to read it must not suppress diagnostics.
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(true);
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Throws(new IOException("locked"));

        int budget = GitHubActionsSummaryReporter.GetRemainingDetailsBudget(fileSystem.Object, "summary.md", new Mock<ILogger>().Object);

        Assert.AreEqual(GitHubActionsFailureDetails.MaxTotalDetailsLength, budget);
    }

    [TestMethod]
    public void EffectiveStepSummaryLimit_IsSlightlyBelowTheDocumentedLimit()
    {
        // GitHubStepSummaryLimit is defined as 1024 * 1024, GitHub's documented cap. Measured on ubuntu-latest: a
        // summary of exactly 1 MiB is accepted, and 1,148,551 bytes is rejected outright with
        // "$GITHUB_STEP_SUMMARY upload aborted" — GitHub discards the whole file rather than truncating it. The
        // small margin below the documented limit guards the near-boundary rejection reported in
        // actions/runner#4337, which did not reproduce but costs two bytes to defend against.
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, GitHubActionsFailureDetails.EffectiveStepSummaryLimit);

        // The margin must stay negligible: a large one would silently cost users summary content for no reason.
        Assert.IsLessThan(1024, GitHubActionsFailureDetails.GitHubStepSummaryLimit - GitHubActionsFailureDetails.EffectiveStepSummaryLimit);

        // The two degradation thresholds must be ordered and must both leave headroom below the point of no
        // return, since co-writer output keeps accumulating after this reporter has degraded.
        Assert.IsLessThan(GitHubActionsFailureDetails.CondenseSummaryLength, GitHubActionsFailureDetails.MaxSummaryLength);
        Assert.IsLessThan(GitHubActionsFailureDetails.EffectiveStepSummaryLimit, GitHubActionsFailureDetails.CondenseSummaryLength);
    }

    [TestMethod]
    public void DegradationThresholds_ShedDiagnosticsBeforeWholeSections()
        // The order of the two thresholds is the whole design: diagnostics are kilobytes per failure and go
        // first, while the list of which tests failed is a line each and survives until whole sections have to
        // go. Reversing them would drop the names of failing tests while still expanding stack traces.
        => Assert.IsLessThan(
            GitHubActionsFailureDetails.CondenseSummaryLength,
            GitHubActionsFailureDetails.MaxTotalDetailsLength);

    [TestMethod]
    public void ShouldCondenseProjectSection_OnlyOncePastTheCondenseThreshold()
    {
        // Between the two thresholds a project still gets a full section, so a file just under the condense
        // threshold must not be condensed — that is the band where failures are listed without diagnostics.
        Assert.IsFalse(CondenseAtLength(GitHubActionsFailureDetails.CondenseSummaryLength - 1));
        Assert.IsTrue(CondenseAtLength(GitHubActionsFailureDetails.CondenseSummaryLength));

        static bool CondenseAtLength(int length)
        {
            var fileStream = new Mock<IFileStream>();
            fileStream.Setup(s => s.Stream).Returns(new MemoryStream(new byte[length]));

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(true);
            fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(fileStream.Object);

            return GitHubActionsSummaryReporter.ShouldCondenseProjectSection(fileSystem.Object, "summary.md", new Mock<ILogger>().Object);
        }
    }

    [TestMethod]
    public void GetRemainingDetailsBudget_FileOverGitHubLimit_ReturnsZeroAndReportsNearLimit()
    {
        // A file this large is already beyond saving: GitHub will discard it. The budget must bottom out at
        // zero rather than going negative, and the near-limit check must agree, because together they are what
        // stop this project appending to a file that will be thrown away.
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(new MemoryStream(new byte[GitHubActionsFailureDetails.EffectiveStepSummaryLimit + 1024]));

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.ExistFile("summary.md")).Returns(true);
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Returns(fileStream.Object);

        Assert.AreEqual(0, GitHubActionsSummaryReporter.GetRemainingDetailsBudget(fileSystem.Object, "summary.md", new Mock<ILogger>().Object));
        Assert.IsTrue(GitHubActionsSummaryReporter.ShouldCondenseProjectSection(fileSystem.Object, "summary.md", new Mock<ILogger>().Object));
    }

    [TestMethod]
    public void BuildMinimalMarkdown_IsSmallEnoughThatTheProjectedSizeGateIsMeaningful()
    {
        // The overflow gate compares current file length + the rendered markdown against the limit. That is only
        // a useful last line of defence if the condensed form is genuinely small: a multi-kilobyte "minimal"
        // section would be refused so often that projects near the limit would report nothing at all.
        GitHubActionsTestRecord[] records =
        [
            .. Enumerable.Range(0, 500).Select(i => new GitHubActionsTestRecord(
                $"T{i}",
                $"Some.Very.Long.Namespace.And.Class.Name.Test{i}",
                GitHubActionsTerminalKind.Failed,
                TimeSpan.FromMilliseconds(1),
                new GitHubActionsTestFailureDetails(new string('x', 5000), "System.Exception", new string('y', 5000), "src/F.cs", 1))),
        ];

        string minimal = GitHubActionsSummaryReporter.BuildMinimalMarkdown(records, "Asm", "net9.0", AtLeastOneTestFailedExitCode);

        // Independent of test count and of how large the individual failures are.
        Assert.IsLessThan(1024, minimal.Length, minimal);
        Assert.Contains("500 total", minimal);
        Assert.Contains("condensed to one line", minimal);
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(200)]
    [DataRow(600)]
    [DataRow(2000)]
    [DataRow(5000)]
    public void BuildAggregateMarkdown_ScalesWithModuleCount_WithoutExceedingTheCap(int moduleCount)
    {
        // The aggregated path divides its detail budget by module count, so expanded diagnostics cannot grow
        // without bound. Each module's heading, totals table and failure list are written regardless, though, and
        // that per-module overhead is what a large run accumulates — the reason to check the rendered size at
        // module counts a big repository would actually reach, not just at the handful a unit test is tempted to
        // use. Exceeding GitHub's cap costs the entire summary, not its tail.
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 120), 25));
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, moduleCount).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 20,
            FailedTests = 20,
            Failures =
            [
                .. Enumerable.Range(0, 20).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"Boom{j}",
                    FullyQualifiedName = $"Contoso.Some.Reasonably.Long.Test.Assembly.Name{i}.SomeFixtureName.Boom{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = stackTrace,
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: moduleCount * 20,
            passedTests: 0,
            failedTests: moduleCount * 20,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, includeFailureDetails: true, out int omitted);
        string notice = omitted > 0 ? GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(omitted, moduleCount) : string.Empty;

        // Measured in bytes: the cap GitHub enforces is on the file, and a char count understates any summary
        // carrying non-ASCII text — which this one does, if only through its per-module status emoji.
        int byteCount = Encoding.UTF8.GetByteCount(markdown) + Encoding.UTF8.GetByteCount(notice);
        Assert.IsLessThan(
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit,
            byteCount,
            $"A {moduleCount}-module run renders {byteCount} bytes, which GitHub discards in full.");
    }

    [TestMethod]
    public void BuildAggregateMarkdown_ManyModules_StaysUnderTheLimitAndReportsOmittedModules()
    {
        // 40 modules, each with failures large enough that the shared budget cannot expand them all. The
        // file-level note must say so, because a per-module note is invisible inside a collapsed section.
        string stackTrace = string.Join("\n", Enumerable.Repeat(new string('x', 120), 25));
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 40).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"Tests{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 20,
            FailedTests = 20,
            Failures =
            [
                .. Enumerable.Range(0, 20).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"Boom{j}",
                    FullyQualifiedName = $"Tests{i}.Boom{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = stackTrace,
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 800,
            passedTests: 0,
            failedTests: 800,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, includeFailureDetails: true, out int modulesWithOmittedDetails);

        // The whole point of the shared budget: the file stays under GitHub's cap no matter the module count.
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, markdown.Length);

        // The shortfall is reported to the caller rather than buried at the end of the block, so it can be stated
        // at the top of the file where the reader will actually see it. The per-module notes inside each module's
        // section stay where they are.
        Assert.IsGreaterThan(0, modulesWithOmittedDetails);
        Assert.DoesNotContain("test project(s) because", markdown);

        string notice = GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(modulesWithOmittedDetails, modules.Length);
        Assert.Contains("take up too much space", notice);
        Assert.DoesNotContain("job summary size limit was reached", notice);
    }

    [TestMethod]
    public void TruncationNotices_ShareOneMarker_SoASummaryCannotCarryTwoWarnings()
    {
        // The per-project and aggregated writing modes describe different losses, and only one of them runs in a
        // given test process — but a workflow can mix them across steps. They share a marker so whichever warning
        // is written first is the only one, rather than the reader meeting two contradictory warnings.
        Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, GitHubActionsSummaryReporter.BuildTruncationNotice(3));
        Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [TestMethod]
    public void Clip_LongValue_TruncatesAndMarksIt()
    {
        string clipped = GitHubActionsFailureDetails.Clip(new string('a', 100), maxLength: 10)!;

        Assert.StartsWith("aaaaaaaaaa", clipped);
        Assert.Contains("[... truncated]", clipped);
    }

    [TestMethod]
    public void Clip_ShortOrEmptyValue_IsReturnedAsIsOrNull()
    {
        Assert.AreEqual("short", GitHubActionsFailureDetails.Clip("short", maxLength: 10));
        Assert.IsNull(GitHubActionsFailureDetails.Clip("   ", maxLength: 10));
        Assert.IsNull(GitHubActionsFailureDetails.Clip(null, maxLength: 10));
    }

    [TestMethod]
    public void BuildAggregateMarkdown_RendersFailureDetailsFromFragment()
    {
        var module = new GitHubCiRunSummaryModule
        {
            AssemblyName = "Tests",
            ModulePath = "Tests.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            ExecutionId = "execution",
            SessionUid = "session",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 1,
            FailedTests = 1,
            Failures =
            [
                new GitHubCiRunSummaryTest
                {
                    DisplayName = "Boom",
                    FullyQualifiedName = "T.Boom",
                    DurationTicks = TimeSpan.FromSeconds(2).Ticks,
                    ErrorMessage = "assertion failed",
                    ErrorType = "System.Exception",
                    StackTrace = "at T.Boom()",
                    FilePath = "src/T.cs",
                    LineNumber = 7,
                },
            ],
        };
        var aggregate = new GitHubCiRunSummaryAggregate(
            [module],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 1,
            passedTests: 0,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(2),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        Assert.Contains("<summary><code>T.Boom</code> — 2.00s</summary>", markdown);
        Assert.Contains("**Exception:** `System.Exception`", markdown);
        Assert.Contains("**Location:** `src/T.cs:7`", markdown);
        Assert.Contains("assertion failed", markdown);
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

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_DoesNotRetry_WhenWriteFailsAfterHandleAcquired()
    {
        // The handle is acquired successfully but the write/flush fails (e.g. disk full). Retrying would re-append
        // the full section on top of a partial one, so the failure must propagate after a single attempt.
        var fileStream = new Mock<IFileStream>();
        fileStream.Setup(s => s.Stream).Returns(new ThrowOnWriteStream());

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object);

        await Assert.ThrowsExactlyAsync<IOException>(() => GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "partial", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None));

        // Exactly one acquisition: a post-acquire write failure is not contention and must not be retried.
        fileSystem.Verify(f => f.NewFileStream("summary.md", FileMode.Append, FileAccess.Write, FileShare.Read), Times.Once);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithRetryAsync_RefusesTheWrite_WhenItWouldCrossTheCap()
    {
        // The gate lives here rather than in the caller: two sibling projects that each measured the file before
        // acquiring the lock would both see the same length, both conclude they fit, and both append — landing
        // over GitHub's cap, which costs the whole summary rather than one section.
        var buffer = new MemoryStream();
        buffer.SetLength(90);
        buffer.Seek(0, SeekOrigin.End);
        Mock<IFileSystem> fileSystem = CreateFileSystemWritingTo(buffer);

        bool written = await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "0123456789", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None, maxTotalBytes: 99);

        Assert.IsFalse(written);
        Assert.HasCount(90, buffer.ToArray(), "Nothing may be appended once the write is refused.");

        // One byte of headroom is enough: the gate refuses only what would actually cross the limit.
        Assert.IsTrue(await GitHubActionsSummaryReporter.AppendStepSummaryWithRetryAsync(
            fileSystem.Object, "summary.md", "0123456789", maxAttempts: 5, retryDelay: TimeSpan.Zero, CancellationToken.None, maxTotalBytes: 100));
        Assert.HasCount(100, buffer.ToArray());
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_LeavesTheFileUntouched_WhenTheResultWouldCrossTheCap()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new SystemFileSystem();

            bool written = await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "a section that does not fit", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None, leadingNotice: null, maxTotalBytes: 16);

            Assert.IsFalse(written);

            // Replacing the file with one GitHub would discard in full is worse than writing nothing: everything
            // other steps already wrote survives only if we leave it alone.
            Assert.AreEqual("existing\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void BuildAggregateMarkdown_CondenseAllModules_RendersOnlyVerdictLines()
    {
        // The fallback the post-processor takes when the full rendering is refused: the smallest report that still
        // names every test project.
        var aggregate = new GitHubCiRunSummaryAggregate(
            [
                new GitHubCiRunSummaryModule
                {
                    AssemblyName = "Tests",
                    ModulePath = "Tests.dll",
                    TargetFramework = "net9.0",
                    Architecture = "x64",
                    SessionUid = "session",
                    AttemptNumber = 1,
                    ExitCode = AtLeastOneTestFailedExitCode,
                    TotalTests = 2,
                    FailedTests = 1,
                    PassedTests = 1,
                    Failures =
                    [
                        new GitHubCiRunSummaryTest
                        {
                            DisplayName = "Boom",
                            FullyQualifiedName = "Tests.Boom",
                            DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                            ErrorMessage = "assertion failed",
                            ErrorType = "System.Exception",
                        },
                    ],
                },
            ],
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 2,
            passedTests: 1,
            failedTests: 1,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(1),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(
            aggregate, includeFailureDetails: true, out _, out int condensedModules, out _, condenseAllModules: true);

        Assert.AreEqual(1, condensedModules);
        Assert.Contains("❌ `Tests` (net9.0): 2 total", markdown);
        Assert.DoesNotContain("assertion failed", markdown);
        Assert.DoesNotContain("Tests.Boom", markdown);
    }

    [TestMethod]
    public void BuildAggregateMarkdown_StaysUnderTheCap_WhenTheContentIsNonAscii()
    {
        // The cap GitHub enforces is on bytes, and this content is the non-ASCII-heavy kind: a UTF-16 char count
        // understates a summary of Japanese test names by threefold, which is enough to render a file GitHub
        // discards while every char-based check reports it as comfortably within budget. The module count is high
        // enough to exhaust the listing bound too, so the tail of the run is reported as a count rather than as
        // one line per project — the only rendering whose size does not grow with the run.
        GitHubCiRunSummaryModule[] modules = Enumerable.Range(0, 3000).Select(i => new GitHubCiRunSummaryModule
        {
            AssemblyName = $"テストアセンブリの名前{i}",
            ModulePath = $"Tests{i}.dll",
            TargetFramework = "net9.0",
            Architecture = "x64",
            SessionUid = $"session-{i}",
            AttemptNumber = 1,
            ExitCode = AtLeastOneTestFailedExitCode,
            TotalTests = 5,
            FailedTests = 5,
            Failures =
            [
                .. Enumerable.Range(0, 5).Select(j => new GitHubCiRunSummaryTest
                {
                    DisplayName = $"失敗したテスト{j}",
                    FullyQualifiedName = $"テストアセンブリの名前{i}.とても長い名前空間.失敗したテスト{j}",
                    DurationTicks = TimeSpan.FromMilliseconds(1).Ticks,
                    ErrorMessage = "アサーションが失敗しました。期待値と実際の値が一致しません。",
                    ErrorType = "System.Exception",
                }),
            ],
        }).ToArray();

        var aggregate = new GitHubCiRunSummaryAggregate(
            modules,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            totalTests: 15000,
            passedTests: 0,
            failedTests: 15000,
            skippedTests: 0,
            duration: TimeSpan.FromSeconds(10),
            exitCode: AtLeastOneTestFailedExitCode,
            hasAuthoritativeRunSummary: true,
            isPartial: false);

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, includeFailureDetails: true, out _, out int condensedModules, out int unlistedModules);

        int byteCount = Encoding.UTF8.GetByteCount(markdown);
        Assert.IsGreaterThan(0, condensedModules, "This many modules must exhaust the budget, or the test is not exercising the bound.");
        Assert.IsGreaterThan(0, unlistedModules, "This many modules must exhaust the listing bound, or the test is not exercising it.");
        Assert.IsLessThan(
            GitHubActionsFailureDetails.GitHubStepSummaryLimit,
            byteCount,
            $"A 3000-module run renders {byteCount} bytes, which GitHub discards in full.");

        // Silently stopping the listing would leave a reader believing the run had only the projects shown.
        Assert.Contains("further test project(s) are not listed", markdown);
    }

    [TestMethod]
    public async Task CreateModule_ThenFragmentRoundTrip_KeepsFailureDiagnostics_AndOmitsThemFromSlowestTests()
    {
        // The other aggregate tests hand-build CiRunSummaryTest instances, so none of them exercises the
        // TestRecord -> CiRunSummaryTest conversion or the JSON fragment the deferred path actually writes.
        // Without this test, dropping the diagnostics from either would leave every test passing while real
        // multi-project runs silently lost the failure details this PR exists to render.
        string resultsDirectory = Path.Combine(Path.GetTempPath(), "mtp-fragment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDirectory);
        try
        {
            GitHubActionsTestRecord[] records =
            [
                new GitHubActionsTestRecord(
                    "Boom",
                    "T.Boom",
                    GitHubActionsTerminalKind.Failed,
                    TimeSpan.FromSeconds(9),
                    new GitHubActionsTestFailureDetails("assertion failed", "System.InvalidOperationException", "   at T.Boom()", "src/T.cs", 42)),
            ];

            GitHubCiRunSummaryModule module = GitHubCiRunSummaryAggregation.CreateModule(
                records,
                "Tests",
                Path.Combine(resultsDirectory, "Tests.dll"),
                "net9.0",
                "x64",
                executionId: "execution",
                sessionUid: "session",
                attemptNumber: 1,
                exitCode: AtLeastOneTestFailedExitCode);

            string fragmentPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(resultsDirectory, "github-actions", "github-actions", module);

            GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
                [new InputArtifact(fragmentPath, "microsoft.testing.github-actions-summary-fragment", Path.Combine(resultsDirectory, "Tests.dll"), "net9.0", "x64", "execution")],
                "github-actions",
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            GitHubCiRunSummaryTest failure = aggregate.Modules.Single().Failures.Single();
            Assert.AreEqual("assertion failed", failure.ErrorMessage);
            Assert.AreEqual("System.InvalidOperationException", failure.ErrorType);
            Assert.AreEqual("   at T.Boom()", failure.StackTrace);
            Assert.AreEqual("src/T.cs", failure.FilePath);
            Assert.AreEqual(42, failure.LineNumber);

            // The same test also qualifies as a slowest test. Carrying its stack trace there too would duplicate
            // potentially large diagnostics in every fragment for no rendering benefit.
            GitHubCiRunSummaryTest slowest = aggregate.Modules.Single().SlowestTests.Single();
            Assert.AreEqual("T.Boom", slowest.FullyQualifiedName);
            Assert.IsNull(slowest.StackTrace);
            Assert.IsNull(slowest.ErrorMessage);

            // Rendering it end to end is what proves the diagnostics survived in a usable shape.
            string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
            Assert.Contains("**Exception:** `System.InvalidOperationException`", markdown);
            Assert.Contains("**Location:** `src/T.cs:42`", markdown);
            Assert.Contains("assertion failed", markdown);
        }
        finally
        {
            Directory.Delete(resultsDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void HasLeadingTruncationNotice_IgnoresTheMarker_WhenItAppearsInsideFailureDiagnostics()
    {
        // Failure messages are copied verbatim into the summary, so a test whose output contains the marker text
        // must not be mistaken for the notice — that would suppress the real warning and leave a shortened
        // summary that never says it was shortened.
        Assert.IsTrue(GitHubActionsSummaryReporter.HasLeadingTruncationNotice(GitHubActionsSummaryReporter.BuildTruncationNotice(3)));
        Assert.IsFalse(GitHubActionsSummaryReporter.HasLeadingTruncationNotice(
            $"## Tests\n\n```text\nExpected the summary to contain {GitHubActionsSummaryReporter.TruncationNoticeMarker}\n```\n"));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_StillHoistsTheNotice_WhenAFailureMessageContainsTheMarker()
    {
        string path = Path.GetTempFileName();
        try
        {
            // A previously written section whose rendered failure message happens to carry the marker text.
            File.WriteAllText(path, $"## Tests\n\n```text\nexpected {GitHubActionsSummaryReporter.TruncationNoticeMarker}\n```\n");
            var fileSystem = new SystemFileSystem();

            await GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                fileSystem, path, "## More\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_ReplacesMatchingSection()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing\n");
            var fileSystem = new SystemFileSystem();

            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "first", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);
            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "second", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);
            Assert.Contains("existing", summary);
            Assert.Contains("second", summary);
            Assert.DoesNotContain("first", summary);
            const string Marker = "microsoft-testing-platform:github-actions:run-1:start";
            int firstMarker = summary.IndexOf(Marker, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, firstMarker);
            Assert.AreEqual(-1, summary.IndexOf(Marker, firstMarker + Marker.Length, StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void BuildTruncationNotice_SaysHowManyProjectsGotTheirResultsIn()
    {
        string notice = GitHubActionsSummaryReporter.BuildTruncationNotice(7);

        Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeMarker, notice);
        Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeEndMarker, notice);
        Assert.Contains("shortened", notice);
        // The count is the point: it tells the reader how much of the report they can trust as complete.
        Assert.Contains("7", notice);
        // The limit is quoted in round units, not to the byte — the exact figure is noise to the reader.
        Assert.Contains("1 MB", notice);
        Assert.DoesNotContain(GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture), notice);
    }

    [TestMethod]
    public void CountProjectSections_CountsOnlyFullSections()
    {
        string full = GitHubActionsSummaryReporter.BuildMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);
        string condensed = GitHubActionsSummaryReporter.BuildMinimalMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);

        Assert.AreEqual(0, GitHubActionsSummaryReporter.CountProjectSections(condensed));
        Assert.AreEqual(2, GitHubActionsSummaryReporter.CountProjectSections(full + condensed + full));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_HoistsNoticeToTheTop_AndAppendsContentAfterIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "earlier-project\n");
            var fileSystem = new SystemFileSystem();

            await GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                fileSystem, path, "first-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);
            await GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                fileSystem, path, "second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);

            // The warning leads the report, so a reader meets it before the sections it is warning about, and the
            // content that was already there keeps its order behind it.
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.IsLessThan(
                summary.IndexOf("first-project", StringComparison.Ordinal),
                summary.IndexOf("earlier-project", StringComparison.Ordinal));
            Assert.IsLessThan(
                summary.IndexOf("second-project", StringComparison.Ordinal),
                summary.IndexOf("first-project", StringComparison.Ordinal));
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_KeepsNoticeFirst_WhenACoWriterAppendsAfterIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Empty);
            var fileSystem = new SystemFileSystem();

            await GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                fileSystem, path, "first-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            // This extension is not the only writer to GITHUB_STEP_SUMMARY: a test framework appends its own block
            // after the reporter runs. Appending cannot dislodge a note that sits at the top, which is the reason
            // it goes there rather than at the end.
            File.AppendAllText(path, "### framework's own section\n");

            await GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                fileSystem, path, "second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);

            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.Contains("framework's own section", summary);
            Assert.Contains("second-project", summary);
            // The note is stated once. Repeating it per project would spend the little headroom that is left on
            // restating the same sentence.
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task UpsertStepSummaryWithRetryAsync_PutsTheNoticeFirst_AndNeverAddsASecondOne()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "earlier content\n");
            var fileSystem = new SystemFileSystem();
            string notice = GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(2, 5);

            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "aggregate block", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None, notice);

            string summary = File.ReadAllText(path);
            Assert.StartsWith(GitHubActionsSummaryReporter.TruncationNoticeMarker, summary);
            Assert.Contains("earlier content", summary);
            Assert.Contains("aggregate block", summary);
            AssertSingleNotice(summary);

            // Re-running the same aggregation replaces its block; the warning must not be duplicated with it.
            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem, path, "run-1", "aggregate block v2", maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None, notice);

            summary = File.ReadAllText(path);
            Assert.Contains("aggregate block v2", summary);
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CountProjectSections_IgnoresTheMarkerInsideTestOutput()
    {
        // A failing test's name and message are rendered into the summary verbatim, so a test that mentions the
        // marker would otherwise be counted as a project section and inflate the number the warning quotes.
        string full = GitHubActionsSummaryReporter.BuildMarkdown(
            [new GitHubActionsTestRecord("T", "T.Test", GitHubActionsTerminalKind.Passed, TimeSpan.Zero)],
            "T",
            "net9.0",
            exitCode: 0);

        string withMarkerInProse = full + $"- `SomeTest_Mentioning_{GitHubActionsSummaryReporter.ProjectSectionMarker}_InItsName` — 1ms\n";

        Assert.AreEqual(1, GitHubActionsSummaryReporter.CountProjectSections(withMarkerInProse));
    }

    [TestMethod]
    public async Task AppendStepSummaryWithLeadingNoticeAsync_LeavesTheSummaryIntact_WhenTheRewriteFailsPartway()
    {
        // Hoisting the notice replaces the whole file. If that were done by truncating in place, a failure partway
        // through — a full disk on a runner, or cancellation during session teardown — would leave the summary
        // empty, losing every section earlier projects wrote. Failing must cost this project's section, never the
        // file, so the write is staged elsewhere and only swapped in once complete.
        string dir = Path.Combine(Path.GetTempPath(), "mtp-summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "summary.md");
        const string Original = "earlier-project-section\n";
        try
        {
            File.WriteAllText(path, Original);
            var real = new SystemFileSystem();

            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.ExistFile(It.IsAny<string>())).Returns<string>(File.Exists);
            fileSystem.Setup(f => f.ReplaceFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>(real.ReplaceFile);
            fileSystem.Setup(f => f.DeleteFile(It.IsAny<string>())).Callback<string>(real.DeleteFile);
            fileSystem.Setup(f => f.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
                .Returns<string, FileMode, FileAccess, FileShare>((p, mode, access, share) =>
                {
                    // Everything behaves normally except the staged copy, which fails mid-write.
                    if (p.EndsWith(".tmp", StringComparison.Ordinal))
                    {
                        var throwing = new Mock<IFileStream>();
                        throwing.Setup(s => s.Stream).Returns(new ThrowOnWriteStream());
                        return throwing.Object;
                    }

                    return real.NewFileStream(p, mode, access, share);
                });

            await Assert.ThrowsExactlyAsync<IOException>(() =>
                GitHubActionsSummaryReporter.AppendStepSummaryWithLeadingNoticeAsync(
                    fileSystem.Object, path, "second-project\n", GitHubActionsSummaryReporter.BuildTruncationNotice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None));

            // The pre-existing content is still there: the failed write never touched the summary itself.
            Assert.AreEqual(Original, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void AssertSingleNotice(string summary)
    {
        string marker = GitHubActionsSummaryReporter.TruncationNoticeMarker;
        int first = summary.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, first);
        Assert.AreEqual(-1, summary.IndexOf(marker, first + marker.Length, StringComparison.Ordinal));
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

    // A writable stream that fails on any attempt to write or flush, simulating a mid-write I/O error (e.g. disk full)
    // after the exclusive append handle has already been acquired.
    private sealed class ThrowOnWriteStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;

            // Position is not settable on this write-only, non-seekable test stream. The discard makes the
            // otherwise-ignored assigned value explicit so static analysis doesn't flag it.
            set => _ = value;
        }

        public override void Flush() => throw new IOException("There is not enough space on the disk.");

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("There is not enough space on the disk.");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
