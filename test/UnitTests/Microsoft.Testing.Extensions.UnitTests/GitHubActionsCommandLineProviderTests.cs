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
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections)]
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
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenStepSummarySectionsIsUsedWithReportGhAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
            [GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections] = ["test-results"],
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
    [DataRow("test-results")]
    [DataRow("SLOW-TESTS")]
    [DataRow("all")]
    [DataRow("test-results|slow-tests")]
    [DataRow("test-results,slow-tests")]
    [DataRow("test-results|TEST-RESULTS")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForStepSummarySectionsAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, value.Split('|')).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("test-results,")]
    public async Task ValidateOptionArgumentsAsync_ReturnsLocalizedError_WhenStepSummarySectionsIsEmptyAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(GitHubActionsResources.EmptyStepSummarySections, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("unknown", "unknown")]
    [DataRow("all|unknown", "unknown")]
    [DataRow("test-results,bogus", "bogus")]
    public async Task ValidateOptionArgumentsAsync_ReturnsLocalizedError_WhenStepSummarySectionIsUnknownAsync(string value, string invalidValue)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummarySections);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, value.Split('|')).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidStepSummarySection, invalidValue),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    public void TryParseStepSummarySections_IsCaseInsensitiveAndDuplicateSafe()
    {
        bool parsed = GitHubActionsStepSummarySectionsParser.TryParse(
            [" TEST-RESULTS ", "slow-tests,SLOW-TESTS", "test-results"],
            out GitHubActionsStepSummarySections sections,
            out string? invalidValue,
            out bool hasEmptyValue);

        Assert.IsTrue(parsed);
        Assert.AreEqual(GitHubActionsStepSummarySections.All, sections);
        Assert.IsNull(invalidValue);
        Assert.IsFalse(hasEmptyValue);
    }

    [TestMethod]
    public void TryParseStepSummarySections_RejectsEmptySet()
    {
        bool parsed = GitHubActionsStepSummarySectionsParser.TryParse(
            [],
            out GitHubActionsStepSummarySections sections,
            out string? invalidValue,
            out bool hasEmptyValue);

        Assert.IsFalse(parsed);
        Assert.AreEqual(GitHubActionsStepSummarySections.None, sections);
        Assert.IsNull(invalidValue);
        Assert.IsTrue(hasEmptyValue);
    }

    [TestMethod]
    public void GetSections_DefaultsToAll_WhenOptionIsAbsent()
    {
        GitHubActionsStepSummarySections sections = GitHubActionsStepSummarySectionsParser.GetSections(
            new TestCommandLineOptions([]));

        Assert.AreEqual(GitHubActionsStepSummarySections.All, sections);
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
