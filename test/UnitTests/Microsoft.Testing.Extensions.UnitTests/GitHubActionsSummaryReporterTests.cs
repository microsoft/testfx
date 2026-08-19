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
    public void BuildMarkdown_TruncatesOversizedFailureDetails_AndSaysSo()
    {
        // Every failure carries a stack trace far larger than the supplied budget, so only the first few can be
        // expanded; the rest must degrade to compact lines and be reported as omitted. The budget is passed
        // explicitly rather than relying on the default, so the test states the ratio it depends on.
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
        Assert.Contains("job summary size limit was reached", markdown);
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
        // Measured on ubuntu-latest: a summary of exactly 1 MiB is accepted, and 1,148,551 bytes is rejected
        // outright with "$GITHUB_STEP_SUMMARY upload aborted" — GitHub discards the whole file rather than
        // truncating it. The small margin below the documented limit guards the near-boundary rejection
        // reported in actions/runner#4337, which did not reproduce but costs two bytes to defend against.
        Assert.AreEqual(1024 * 1024, GitHubActionsFailureDetails.GitHubStepSummaryLimit);
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, GitHubActionsFailureDetails.EffectiveStepSummaryLimit);

        // The margin must stay negligible: a large one would silently cost users summary content for no reason.
        Assert.IsLessThan(1024, GitHubActionsFailureDetails.GitHubStepSummaryLimit - GitHubActionsFailureDetails.EffectiveStepSummaryLimit);

        // The condense threshold must leave real headroom below the point of no return, since co-writer
        // output continues to accumulate after this reporter has degraded to one-liners.
        Assert.IsLessThan(GitHubActionsFailureDetails.EffectiveStepSummaryLimit, GitHubActionsFailureDetails.MaxSummaryLength);
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
        Assert.IsTrue(GitHubActionsSummaryReporter.IsSummaryNearLimit(fileSystem.Object, "summary.md", new Mock<ILogger>().Object));
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

        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);

        // The whole point of the shared budget: the file stays under GitHub's cap no matter the module count.
        Assert.IsLessThan(GitHubActionsFailureDetails.GitHubStepSummaryLimit, markdown.Length);
        Assert.Contains("test project(s) because the job summary size limit was reached", markdown);
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
    public void BuildTruncationNotice_SaysWhyTheSummaryStopsShort()
    {
        string notice = GitHubActionsSummaryReporter.BuildTruncationNotice();

        Assert.Contains(GitHubActionsSummaryReporter.TruncationNoticeMarker, notice);
        Assert.Contains("truncated", notice);
        // The reader needs the reason, not just the fact: an oversized summary is dropped outright by GitHub.
        Assert.Contains("dropping", notice);
        Assert.Contains(GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture), notice);
    }

    [TestMethod]
    public async Task AppendStepSummaryWithTrailingNoticeAsync_MovesNoticeAfterNewContent_InsteadOfRepeatingIt()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Empty);
            var fileSystem = new SystemFileSystem();
            string notice = GitHubActionsSummaryReporter.BuildTruncationNotice();

            await GitHubActionsSummaryReporter.AppendStepSummaryWithTrailingNoticeAsync(
                fileSystem, path, "first-project\n", notice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);
            await GitHubActionsSummaryReporter.AppendStepSummaryWithTrailingNoticeAsync(
                fileSystem, path, "second-project\n", notice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);

            // Both projects' verdicts survive, and the note closes the summary rather than being stranded between
            // them — a reader scrolling to the bottom is exactly who needs to know the report was cut short.
            Assert.Contains("first-project", summary);
            Assert.Contains("second-project", summary);
            Assert.EndsWith(notice, summary);
            Assert.IsLessThan(
                summary.IndexOf(GitHubActionsSummaryReporter.TruncationNoticeMarker, StringComparison.Ordinal),
                summary.IndexOf("second-project", StringComparison.Ordinal));
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AppendStepSummaryWithTrailingNoticeAsync_MovesNoticePastACoWritersOutput_SoItStaysLast()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Empty);
            var fileSystem = new SystemFileSystem();
            string notice = GitHubActionsSummaryReporter.BuildTruncationNotice();

            await GitHubActionsSummaryReporter.AppendStepSummaryWithTrailingNoticeAsync(
                fileSystem, path, "first-project\n", notice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            // This extension is not the only writer to GITHUB_STEP_SUMMARY: a test framework appends its own block
            // after the reporter runs, so the note is no longer the tail when the next project writes.
            File.AppendAllText(path, "### framework's own section\n");

            await GitHubActionsSummaryReporter.AppendStepSummaryWithTrailingNoticeAsync(
                fileSystem, path, "second-project\n", notice, maxAttempts: 1, retryDelay: TimeSpan.Zero, CancellationToken.None);

            string summary = File.ReadAllText(path);

            // The co-writer's block is preserved and stays in order, but the note is lifted past it so it still
            // closes the summary instead of being stranded where the budget first ran out.
            Assert.Contains("framework's own section", summary);
            Assert.EndsWith(notice, summary);
            Assert.IsLessThan(
                summary.IndexOf(GitHubActionsSummaryReporter.TruncationNoticeMarker, StringComparison.Ordinal),
                summary.IndexOf("framework's own section", StringComparison.Ordinal));
            Assert.IsLessThan(
                summary.IndexOf("second-project", StringComparison.Ordinal),
                summary.IndexOf("framework's own section", StringComparison.Ordinal));
            // The note is stated once. Repeating it per project would spend the little headroom that is left on
            // restating the same sentence.
            AssertSingleNotice(summary);
        }
        finally
        {
            File.Delete(path);
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
