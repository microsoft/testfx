// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;
using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsCommandLineProviderTests
{
    [TestMethod]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsGroups)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsAnnotations)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsStepSummary)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsFailureDetails)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSubOptionIsUsedWithoutReportGhAsync(string subOption)
    {
        GitHubActionsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [subOption] = ["off"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, GitHubActionsResources.SubOptionsRequireReportGh, GitHubActionsCommandLineOptions.GitHubActionsOptionName),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsGroups)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsAnnotations)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsStepSummary)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsFailureDetails)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenSubOptionIsUsedWithReportGhAsync(string subOption)
    {
        GitHubActionsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
            [subOption] = ["off"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenGroupsValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsGroups);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenGroupsValueIsOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsGroups);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["off"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenAnnotationsValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsAnnotations);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenAnnotationsValueIsOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsAnnotations);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["off"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenStepSummaryValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenFailureDetailsValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenFailureDetailsValueIsOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["off"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenSlowTestNoticesValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenSlowTestThresholdIsNotPositiveIntegerAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["0"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenSlowTestThresholdIsPositiveIntegerAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["30"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("enable")]
    [DataRow("1")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForBooleanAliasesAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsGroups);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, value);
    }

    [TestMethod]
    [DataRow("90s")]
    [DataRow("2m")]
    [DataRow("1.5h")]
    [DataRow("60")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForSlowTestThresholdDurationsAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, value);
    }

    [TestMethod]
    [DataRow("0s")]
    [DataRow("abc")]
    [DataRow("-5s")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForNonPositiveOrUnparseableThresholdAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid, value);
    }
}
