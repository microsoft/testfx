// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsExitCodeTests
{
    [TestMethod]
    [DataRow(0)] // Success
    [DataRow(2)] // AtLeastOneTestFailed
    public void IsTestResultOutcome_ForTestResultCodes_ReturnsTrue(int exitCode)
        => Assert.IsTrue(GitHubActionsExitCode.IsTestResultOutcome(exitCode));

    [TestMethod]
    [DataRow(1)] // GenericFailure
    [DataRow(3)] // TestSessionAborted
    [DataRow(8)] // ZeroTests
    [DataRow(9)] // MinimumExpectedTestsPolicyViolation
    [DataRow(13)] // TestExecutionStoppedForMaxFailedTests
    [DataRow(255)] // Unknown
    public void IsTestResultOutcome_ForNonTestResultCodes_ReturnsFalse(int exitCode)
        => Assert.IsFalse(GitHubActionsExitCode.IsTestResultOutcome(exitCode));

    [TestMethod]
    public void IndicatesFailure_OnlySuccessIsNotFailure()
    {
        Assert.IsFalse(GitHubActionsExitCode.IndicatesFailure(0));
        Assert.IsTrue(GitHubActionsExitCode.IndicatesFailure(2));
        Assert.IsTrue(GitHubActionsExitCode.IndicatesFailure(9));
    }

    [TestMethod]
    public void GetName_ReturnsEnumNameForKnownCodeAndUnknownOtherwise()
    {
        Assert.AreEqual("MinimumExpectedTestsPolicyViolation", GitHubActionsExitCode.GetName(9));
        Assert.AreEqual("ZeroTests", GitHubActionsExitCode.GetName(8));
        Assert.AreEqual("Unknown", GitHubActionsExitCode.GetName(255));
    }

    [TestMethod]
    public void GetReason_ForUnknownCode_ReturnsGenericNonSuccessReason()
    {
        // 255 is outside the documented ExitCode set: it must not throw and must fall back to the generic reason.
        string reason = GitHubActionsExitCode.GetReason(255);
        Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
    }

    [TestMethod]
    public void GetReason_ForKnownCode_MentionsRelevantOption()
    {
        Assert.Contains("--minimum-expected-tests", GitHubActionsExitCode.GetReason(9));
        Assert.Contains("--maximum-failed-tests", GitHubActionsExitCode.GetReason(13));
        Assert.Contains("coverage threshold", GitHubActionsExitCode.GetReason(14));
    }

    [TestMethod]
    public void GetExitCodeAnnotation_ForZeroTests_EmitsErrorWorkflowCommand()
    {
        string annotation = GitHubActionsAnnotationReporter.GetExitCodeAnnotation(8);

        Assert.IsTrue(annotation.StartsWith("::error title=", StringComparison.Ordinal), annotation);
        Assert.Contains("exit code 8", annotation);
        Assert.Contains("ZeroTests", annotation);
    }
}
