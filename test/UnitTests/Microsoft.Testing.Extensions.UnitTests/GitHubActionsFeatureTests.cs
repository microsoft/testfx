// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsFeatureTests
{
    [TestMethod]
    [DataRow("true")]
    [DataRow("TRUE")]
    [DataRow("TrUe")]
    public void IsRunningOnGitHubActions_ReturnsTrue_WhenEnvironmentValueIsTrueRegardlessOfCasing(string value)
    {
        Mock<IEnvironment> environment = CreateEnvironment(value);

        Assert.IsTrue(GitHubActionsFeature.IsRunningOnGitHubActions(environment.Object));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("false")]
    [DataRow("1")]
    [DataRow(" true")]
    public void IsRunningOnGitHubActions_ReturnsFalse_WhenEnvironmentValueIsUnsetOrNotTrue(string? value)
    {
        Mock<IEnvironment> environment = CreateEnvironment(value);

        Assert.IsFalse(GitHubActionsFeature.IsRunningOnGitHubActions(environment.Object));
    }

    [TestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void IsMasterEnabled_ReturnsExpectedValue_BasedOnEnvironmentAndMasterOption(
        bool isGitHubActions,
        bool isMasterOptionSet,
        bool expected)
    {
        Mock<IEnvironment> environment = CreateEnvironment(isGitHubActions ? "true" : null);
        TestCommandLineOptions options = CreateOptions(isMasterOptionSet);

        Assert.AreEqual(expected, GitHubActionsFeature.IsMasterEnabled(options, environment.Object));
    }

    [TestMethod]
    public void IsKnobEnabled_ReturnsTrue_WhenKnobIsUnset()
        => Assert.IsTrue(GitHubActionsFeature.IsKnobEnabled(CreateOptions(), GitHubActionsCommandLineOptions.GitHubActionsGroups));

    [TestMethod]
    [DataRow("on")]
    [DataRow("anything")]
    public void IsKnobEnabled_ReturnsTrue_WhenKnobIsNotOff(string value)
    {
        TestCommandLineOptions options = CreateOptions(
            knobOptionName: GitHubActionsCommandLineOptions.GitHubActionsGroups,
            knobValue: value);

        Assert.IsTrue(GitHubActionsFeature.IsKnobEnabled(options, GitHubActionsCommandLineOptions.GitHubActionsGroups));
    }

    [TestMethod]
    [DataRow("off")]
    [DataRow("OFF")]
    [DataRow("OfF")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    public void IsKnobEnabled_ReturnsFalse_WhenKnobValueDisablesFeature(string value)
    {
        TestCommandLineOptions options = CreateOptions(
            knobOptionName: GitHubActionsCommandLineOptions.GitHubActionsGroups,
            knobValue: value);

        Assert.IsFalse(GitHubActionsFeature.IsKnobEnabled(options, GitHubActionsCommandLineOptions.GitHubActionsGroups));
    }

    [TestMethod]
    [DataRow("on-failure")]
    [DataRow("ON-FAILURE")]
    [DataRow("On-Failure")]
    public void IsStepSummaryOnFailureOnly_ReturnsTrue_WhenValueIsOnFailureRegardlessOfCasing(string value)
    {
        TestCommandLineOptions options = CreateOptions(
            knobOptionName: GitHubActionsCommandLineOptions.GitHubActionsStepSummary,
            knobValue: value);

        Assert.IsTrue(GitHubActionsFeature.IsStepSummaryOnFailureOnly(options));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("on")]
    [DataRow("off")]
    [DataRow("other")]
    public void IsStepSummaryOnFailureOnly_ReturnsFalse_WhenValueIsUnsetOrNotOnFailure(string? value)
    {
        TestCommandLineOptions options = value is null
            ? CreateOptions()
            : CreateOptions(
                knobOptionName: GitHubActionsCommandLineOptions.GitHubActionsStepSummary,
                knobValue: value);

        Assert.IsFalse(GitHubActionsFeature.IsStepSummaryOnFailureOnly(options));
    }

    [TestMethod]
    [DataRow(true, true, null, true)]
    [DataRow(true, true, "on", true)]
    [DataRow(true, true, "off", false)]
    [DataRow(true, false, null, false)]
    [DataRow(false, true, null, false)]
    public void IsEnabled_ReturnsExpectedValue_BasedOnMasterAndKnobOptions(
        bool isGitHubActions,
        bool isMasterOptionSet,
        string? knobValue,
        bool expected)
    {
        Mock<IEnvironment> environment = CreateEnvironment(isGitHubActions ? "true" : null);
        TestCommandLineOptions options = CreateOptions(
            isMasterOptionSet,
            GitHubActionsCommandLineOptions.GitHubActionsGroups,
            knobValue);

        Assert.AreEqual(
            expected,
            GitHubActionsFeature.IsEnabled(options, environment.Object, GitHubActionsCommandLineOptions.GitHubActionsGroups));
    }

    private static Mock<IEnvironment> CreateEnvironment(string? githubActionsValue)
    {
        Mock<IEnvironment> environment = new();
        _ = environment
            .Setup(e => e.GetEnvironmentVariable("GITHUB_ACTIONS"))
            .Returns(githubActionsValue);
        return environment;
    }

    private static TestCommandLineOptions CreateOptions(
        bool isMasterOptionSet = false,
        string? knobOptionName = null,
        string? knobValue = null)
    {
        Dictionary<string, string[]> options = [];
        if (isMasterOptionSet)
        {
            options[GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];
        }

        if (knobOptionName is not null && knobValue is not null)
        {
            options[knobOptionName] = [knobValue];
        }

        return new TestCommandLineOptions(options);
    }
}
