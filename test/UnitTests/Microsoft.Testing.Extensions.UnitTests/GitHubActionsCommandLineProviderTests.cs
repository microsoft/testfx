// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsCommandLineProviderTests
{
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
}
