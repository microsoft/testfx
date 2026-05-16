// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

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
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenAzureDevOpsDependentOptionsAreConfiguredCorrectlyAsync()
    {
        AzureDevOpsCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile] = ["quarantine.txt"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity] = ["warning"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid);
        Assert.IsNull(validationResult.ErrorMessage);
    }
}
