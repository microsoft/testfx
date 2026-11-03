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
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsFailureDetails)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsHistory)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsAttachments, DisplayName = "Attachments require report-gh")]
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
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsHistory)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold)]
    [DataRow(GitHubActionsCommandLineOptions.GitHubActionsAttachments, DisplayName = "Attachments with report-gh")]
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
    public void GetCommandLineOptions_AttachmentsDirectory_IsVisibleAndRequiresOneArgument()
    {
        GitHubActionsCommandLineProvider provider = new();

        CommandLineOption option = Assert.ContainsSingle(provider.GetCommandLineOptions().Where(
            option => option.Name == GitHubActionsCommandLineOptions.GitHubActionsAttachments));

        Assert.AreEqual("report-gh-attachments", option.Name);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.AreEqual(GitHubActionsResources.AttachmentsOptionDescription, option.Description);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty attachment directory")]
    [DataRow(" ", DisplayName = "Whitespace attachment directory")]
    [DataRow("\0", DisplayName = "NUL attachment directory")]
    [DataRow("attachments", DisplayName = "Relative attachment directory")]
    [DataRow("../attachments", DisplayName = "Parent-relative attachment directory")]
    [DataRow("C:attachments", DisplayName = "Drive-relative attachment directory")]
    [DataRow("\\attachments", DisplayName = "Current-drive-rooted attachment directory")]
    public async Task ValidateOptionArgumentsAsync_AttachmentsRejectInvalidPaths(string path)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(
            option => option.Name == GitHubActionsCommandLineOptions.GitHubActionsAttachments);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [path]);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(GitHubActionsResources.InvalidAttachmentsPath, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Missing attachment path")]
    [DataRow(true, DisplayName = "Multiple attachment paths")]
    public async Task ValidateOptionArgumentsAsync_AttachmentsRejectMissingOrMultiplePaths(bool multiple)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(
            option => option.Name == GitHubActionsCommandLineOptions.GitHubActionsAttachments);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, multiple ? ["one", "two"] : []);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(GitHubActionsResources.InvalidAttachmentsPath, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "New attachment directory")]
    [DataRow(true, DisplayName = "Existing attachment directory")]
    public async Task ValidateOptionArgumentsAsync_AttachmentsAcceptDirectoryWithoutCreatingOutput(bool exists)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-attachment-validation-{Guid.NewGuid():N}");
        if (exists)
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            GitHubActionsCommandLineProvider provider = new();
            CommandLineOption option = provider.GetCommandLineOptions().Single(
                option => option.Name == GitHubActionsCommandLineOptions.GitHubActionsAttachments);

            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [directory]);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreEqual(exists, Directory.Exists(directory));
            if (exists)
            {
                Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_AttachmentsRejectExistingFileAndPreserveBytes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"github-attachment-validation-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, "existing file must remain intact");
        try
        {
            GitHubActionsCommandLineProvider provider = new();
            CommandLineOption option = provider.GetCommandLineOptions().Single(
                option => option.Name == GitHubActionsCommandLineOptions.GitHubActionsAttachments);

            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [path]);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(GitHubActionsResources.InvalidAttachmentsPath, result.ErrorMessage);
            Assert.AreEqual("existing file must remain intact", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
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
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenHistoryWindowHasNoHistoryPathAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
            [GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow] = ["7"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(GitHubActionsResources.HistoryWindowRequiresHistory, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("91")]
    [DataRow("1.5")]
    [DataRow("-1")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenHistoryWindowIsOutOfRangeAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidHistoryWindow, value),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty history path")]
    [DataRow(" ", DisplayName = "Whitespace history path")]
    [DataRow("\0", DisplayName = "NUL history path")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenHistoryPathIsEmptyAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsHistory);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(GitHubActionsResources.InvalidHistoryPath, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenHistoryPathIsExistingDirectoryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            GitHubActionsCommandLineProvider provider = new();
            CommandLineOption option = provider.GetCommandLineOptions().Single(
                o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsHistory);

            ValidationResult validationResult =
                await provider.ValidateOptionArgumentsAsync(option, [directory]).ConfigureAwait(false);

            Assert.IsFalse(validationResult.IsValid);
            Assert.AreEqual(GitHubActionsResources.InvalidHistoryPath, validationResult.ErrorMessage);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("30")]
    [DataRow("90")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenHistoryWindowIsInRangeAsync(string value)
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

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
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenStepSummaryValueIsNotSupportedAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);

        // Pin the branch, not just the verdict: an unsupported value has to name itself in the message, or the
        // user is told only that something was wrong.
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidStepSummaryValue, "maybe"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenFailureDetailsValueIsNotOnOrOffAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);

        // Pin the branch, not just the verdict: this option shares the on/off validator with several others, so
        // the message has to be the on/off one and has to name the value the user actually passed.
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.InvalidOnOffValue, "maybe"),
            validationResult.ErrorMessage);
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
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenStepSummaryValueIsOnFailureAsync()
    {
        GitHubActionsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [GitHubActionsCommandLineOptions.StepSummaryOnFailureValue]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("test-results")]
    [DataRow("SLOW-TESTS")]
    [DataRow("coverage")]
    [DataRow("all")]
    [DataRow("test-results|slow-tests")]
    [DataRow("test-results,slow-tests,coverage")]
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
            [" TEST-RESULTS ", "slow-tests,SLOW-TESTS", "coverage,COVERAGE", "test-results"],
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
    public void ToPersistedValues_AllIncludesCoverage()
        => Assert.AreSequenceEqual(
            ["test-results", "slow-tests", "coverage"],
            GitHubActionsStepSummarySectionsParser.ToPersistedValues(GitHubActionsStepSummarySections.All));

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
