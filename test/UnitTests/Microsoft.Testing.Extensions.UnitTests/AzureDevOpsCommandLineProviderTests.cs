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
}
