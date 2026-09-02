// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsCommandLineProviderTests
{
    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenDemoteKnownFlakyIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky] = [],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsDemoteKnownFlakyRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenFlakyHistoryIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsFlakyHistoryRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenQuarantineFileIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile] = ["quarantine.txt"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsQuarantineFileRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSeverityIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity] = ["warning"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsReportSeverityRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenDemoteKnownFlakyIsUsedWithoutFlakyHistoryAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky] = [],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsDemoteKnownFlakyRequiresFlakyHistory, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSummaryIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsSummaryRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenStackFrameFilterIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter] = ["^MyCompany\\."],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsStackFrameFilterRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenAnnotationsIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations] = ["off"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsAnnotationsRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenGroupsIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsGroups] = ["off"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsGroupsRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenGroupsValueIsNotOnOrOffAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsGroups);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenGroupsValueIsOffAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsGroups);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["off"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenAnnotationsValueIsNotOnOrOffAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenAnnotationsValueIsOnAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["on"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenStackFrameFilterRegexIsInvalidAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["[unclosed"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.Contains("[unclosed", validationResult.ErrorMessage ?? string.Empty);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenStackFrameFilterHasTooManyPatternsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter);
        string[] patterns = Enumerable.Range(0, AzureDevOpsCommandLineProvider.MaxStackFrameFilterPatterns + 1)
            .Select(i => $"^Foo{i}\\.")
            .ToArray();

        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, patterns).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.Contains(AzureDevOpsCommandLineProvider.MaxStackFrameFilterPatterns.ToString(System.Globalization.CultureInfo.InvariantCulture), validationResult.ErrorMessage ?? string.Empty);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForValidStackFrameFilterRegexAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["^MyCompany\\.Testing\\."]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSlowTestHistoryIsUsedWithoutAzureDevOpsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory] = ["14"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsSlowTestHistoryRequiresAzureDevOps, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSlowTestHistoryMinSampleIsUsedWithoutSlowTestHistoryAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample] = ["5"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsSlowTestHistoryMinSampleRequiresSlowTestHistory, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSlowTestHistoryMultiplierIsUsedWithoutSlowTestHistoryAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier] = ["1.5"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsSlowTestHistoryMultiplierRequiresSlowTestHistory, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenSlowTestHistorySubOptionsAreUsedWithSlowTestHistoryAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory] = ["14"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample] = ["5"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier] = ["1.5"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenArtifactUploadOptionsAreUsedWithUploadEnabledAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude] = ["**/*.log"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["MyArtifact"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenPublishRunNameIsUsedWithoutPublishTestResultsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName] = ["MyRun"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(AzureDevOpsResources.PublishAzdoRunNameRequiresPublishAzdoTestResults, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenPublishRunNameIsUsedWithPublishTestResultsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName] = ["MyRun"],
            [AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName] = [],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenNoOptionsAreSetAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("91")]
    [DataRow("not-a-number")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForInvalidFlakyHistoryDaysAsync(string days)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [days]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidFlakyHistoryDays, days),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("30")]
    [DataRow("90")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForValidFlakyHistoryDaysAsync(string days)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [days]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("91")]
    [DataRow("not-a-number")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForInvalidSlowTestHistoryDaysAsync(string days)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [days]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSlowTestHistoryDays, days),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("30")]
    [DataRow("90")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForValidSlowTestHistoryDaysAsync(string days)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [days]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("0")]
    [DataRow("1.5")]
    [DataRow("not-a-number")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForInvalidSlowTestHistoryMinSampleAsync(string minSample)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [minSample]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSlowTestHistoryMinSample, minSample),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("10")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForValidSlowTestHistoryMinSampleAsync(string minSample)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [minSample]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("0")]
    [DataRow("10001")]
    [DataRow("NaN")]
    [DataRow("not-a-number")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForInvalidSlowTestHistoryMultiplierAsync(string multiplier)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [multiplier]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSlowTestHistoryMultiplier, multiplier),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0.1")]
    [DataRow("2.5")]
    [DataRow("10000")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForValidSlowTestHistoryMultiplierAsync(string multiplier)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [multiplier]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeOff)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll)]
    [DataRow("ALL")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForKnownArtifactUploadModeAsync(string mode)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [mode]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForUnknownSeverityAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["critical"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSeverity, "critical"),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("error")]
    [DataRow("warning")]
    [DataRow("WARNING")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForKnownSeverityAsync(string severity)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [severity]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForRelativeGlobPatternAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["**/*.log"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_ForEmptyOrWhitespaceGlobPatternAsync(string pattern)
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [pattern]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidArtifactUploadGlob, pattern),
            validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForMultipleGlobPatternsAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["**/*.log", "artifacts/**"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }
}
