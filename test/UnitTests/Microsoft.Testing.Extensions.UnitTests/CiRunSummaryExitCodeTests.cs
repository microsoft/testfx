// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Globalization;
using System.Resources;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

using GitHubActionsResources = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.Resources.GitHubActionsResources;
using GitHubCiRunSummaryExitCode = ghactions::Microsoft.Testing.Extensions.CiRunSummaryExitCode;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiRunSummaryExitCodeTests
{
    [TestMethod]
    public void IsTestResultOutcome_ForSuccessAndTestFailureCodes_ReturnsTrueInBothReporters()
    {
        Assert.IsTrue(CiRunSummaryExitCode.IsTestResultOutcome(0));
        Assert.IsTrue(CiRunSummaryExitCode.IsTestResultOutcome(2));
        Assert.IsTrue(GitHubCiRunSummaryExitCode.IsTestResultOutcome(0));
        Assert.IsTrue(GitHubCiRunSummaryExitCode.IsTestResultOutcome(2));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(8)]
    [DataRow(255)]
    public void IsTestResultOutcome_ForOtherCodes_ReturnsFalseInBothReporters(int exitCode)
    {
        Assert.IsFalse(CiRunSummaryExitCode.IsTestResultOutcome(exitCode));
        Assert.IsFalse(GitHubCiRunSummaryExitCode.IsTestResultOutcome(exitCode));
    }

    [TestMethod]
    public void IndicatesFailure_OnlySuccessIsNotFailureInBothReporters()
    {
        Assert.IsFalse(CiRunSummaryExitCode.IndicatesFailure(0));
        Assert.IsTrue(CiRunSummaryExitCode.IndicatesFailure(2));
        Assert.IsTrue(CiRunSummaryExitCode.IndicatesFailure(9));
        Assert.IsFalse(GitHubCiRunSummaryExitCode.IndicatesFailure(0));
        Assert.IsTrue(GitHubCiRunSummaryExitCode.IndicatesFailure(2));
        Assert.IsTrue(GitHubCiRunSummaryExitCode.IndicatesFailure(9));
    }

    [TestMethod]
    public void GetName_ReturnsEnumNameForKnownCodesAndUnknownOtherwiseInBothReporters()
    {
        Assert.AreEqual("Success", CiRunSummaryExitCode.GetName(0));
        Assert.AreEqual("AtLeastOneTestFailed", CiRunSummaryExitCode.GetName(2));
        Assert.AreEqual("Unknown", CiRunSummaryExitCode.GetName(-1));
        Assert.AreEqual("Unknown", CiRunSummaryExitCode.GetName(255));
        Assert.AreEqual("Success", GitHubCiRunSummaryExitCode.GetName(0));
        Assert.AreEqual("AtLeastOneTestFailed", GitHubCiRunSummaryExitCode.GetName(2));
        Assert.AreEqual("Unknown", GitHubCiRunSummaryExitCode.GetName(-1));
        Assert.AreEqual("Unknown", GitHubCiRunSummaryExitCode.GetName(255));
    }

    [TestMethod]
    public void GetReason_ForKnownCode_UsesMatchingResourceInBothReporters()
    {
        string expected = AzureDevOpsResources.ExitCodeReasonSuccess;

        Assert.AreEqual(expected, GitHubActionsResources.ExitCodeReasonSuccess);
        Assert.AreEqual(expected, CiRunSummaryExitCode.GetReason(0, AzureDevOpsResources.ResourceManager));
        Assert.AreEqual(expected, GitHubCiRunSummaryExitCode.GetReason(0, GitHubActionsResources.ResourceManager));
    }

    [TestMethod]
    public void GetReason_ForOutOfRangeCode_FallsBackToTheUnknownResourceStringInBothReporters()
    {
        string expected = AzureDevOpsResources.ExitCodeReasonUnknown;

        Assert.AreEqual(expected, GitHubActionsResources.ExitCodeReasonUnknown);
        Assert.AreEqual(expected, CiRunSummaryExitCode.GetReason(255, AzureDevOpsResources.ResourceManager));
        Assert.AreEqual(expected, GitHubCiRunSummaryExitCode.GetReason(255, GitHubActionsResources.ResourceManager));
    }

    [TestMethod]
    public void GetReason_WhenResourceManagerHasNoMatchingKeys_ReturnsTheHardcodedFallbackLiteralInBothReporters()
    {
        // Keep this independent expected value aligned with the final fallback in CiRunSummaryExitCode.
        const string expected = "The test run reported a non-success exit code.";
        ResourceManager resources = new MissingResourceManager();

        Assert.AreEqual(expected, CiRunSummaryExitCode.GetReason(8, resources));
        Assert.AreEqual(expected, GitHubCiRunSummaryExitCode.GetReason(8, resources));
    }

    [TestMethod]
    public void GetReason_WhenSpecificResourceIsMissing_UsesUnknownResourceInBothReporters()
    {
        const string expected = "Localized unknown reason";
        ResourceManager resources = new UnknownOnlyResourceManager();

        Assert.AreEqual(expected, CiRunSummaryExitCode.GetReason(8, resources));
        Assert.AreEqual(expected, GitHubCiRunSummaryExitCode.GetReason(8, resources));
    }

    private sealed class UnknownOnlyResourceManager : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
            => name is "ExitCodeReasonUnknown" ? "Localized unknown reason" : null;
    }

    private sealed class MissingResourceManager : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
            => null;
    }
}
