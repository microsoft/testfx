// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using GitHubActionsTerminalKind = ghactions::Microsoft.Testing.Extensions.TerminalKind;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsSummaryReporterTests
{
    [TestMethod]
    public void BuildMarkdown_AllPassing_UsesSuccessIconAndTotals()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Add", "CalculatorTests.Add", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Sub", "CalculatorTests.Sub", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(20)),
            new("Skip", "CalculatorTests.Skip", GitHubActionsTerminalKind.Skipped, TimeSpan.Zero),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "CalculatorTests", "net9.0");

        Assert.Contains("## ✅ Test Run Summary — CalculatorTests (net9.0)", markdown);
        Assert.Contains("| 3 | 2 | 0 | 1 | 30ms |", markdown);
        Assert.DoesNotContain("### ❌ Failures", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_WithFailures_UsesFailureIconAndListsFailures()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Pass", "StringUtilsTests.Pass", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(5)),
            new("Boom", "StringUtilsTests.Boom", GitHubActionsTerminalKind.Failed, TimeSpan.FromMilliseconds(7)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "StringUtilsTests", "net9.0");

        Assert.Contains("## ❌ Test Run Summary — StringUtilsTests (net9.0)", markdown);
        Assert.Contains("### ❌ Failures (1)", markdown);
        Assert.Contains("- `StringUtilsTests.Boom`", markdown);
    }

    [TestMethod]
    public void BuildMarkdown_EmitsSlowestTestsSortedByDuration()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Fast", "T.Fast", GitHubActionsTerminalKind.Passed, TimeSpan.FromMilliseconds(10)),
            new("Slow", "T.Slow", GitHubActionsTerminalKind.Passed, TimeSpan.FromSeconds(65)),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "T", "net9.0");

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
        string markdown = GitHubActionsSummaryReporter.BuildMarkdown([], "Empty", "net9.0");

        Assert.Contains("## ✅ Test Run Summary — Empty (net9.0)", markdown);
        Assert.Contains("| 0 | 0 | 0 | 0 | 0ms |", markdown);
    }
}
