// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpCommandLineProviderTests
{
    [TestMethod]
    [DataRow("abc")]
    [DataRow("")]
    [DataRow("-1")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenTimeoutIsNotParseableAsync(string value)
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTimeoutOptionInvalidArgument, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("30s")]
    [DataRow("2m")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenTimeoutIsParseableAsync(string value)
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenTypeIsUnknownAsync()
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["Unknown"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("None")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenTypeIsKnownAsync(string value)
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenTypeIfSupportedIsUnknownAsync()
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["Unknown"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenTypeIfSupportedIsTriageAsync()
    {
        // 'Triage' is always accepted by --hangdump-type-if-supported regardless of the current
        // TFM: on runtimes where it is unsupported the lifetime handler maps it to a fallback.
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["Triage"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForOptionsWithoutSpecificValidationAsync()
    {
        HangDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == HangDumpCommandLineProvider.HangDumpFileNameOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["dump.dmp"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSubOptionIsUsedWithoutHangDumpAsync(string subOption)
    {
        HangDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [subOption] = ["30s"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(ExtensionResources.MissingHangDumpMainOption, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenSubOptionIsUsedWithHangDumpAsync()
    {
        HangDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
            [HangDumpCommandLineProvider.HangDumpTimeoutOptionName] = ["30s"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenTypeAndTypeIfSupportedAreBothUsedAsync()
    {
        HangDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
            [HangDumpCommandLineProvider.HangDumpTypeOptionName] = ["Mini"],
            [HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName] = ["Mini"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTypeAndIfSupportedAreMutuallyExclusiveErrorMessage, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenOnlyTypeIsUsedAsync()
    {
        HangDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
            [HangDumpCommandLineProvider.HangDumpTypeOptionName] = ["Mini"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public void IsHangDumpTypeSupportedOnCurrentRuntime_ReturnsTrue_ForAlwaysSupportedType()
        => Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Mini"));

    [TestMethod]
    public void MapToSupportedDumpType_ReturnsSameValue_WhenSupportedOnCurrentRuntime()
        => Assert.AreEqual("Mini", HangDumpCommandLineProvider.MapToSupportedDumpType("Mini"));

    // "Unknown" is neither a supported nor a specially-mapped value, so the fallback ("Full")
    // is used. This guards the historical default behavior described in the source comments.
    [TestMethod]
    public void MapToSupportedDumpType_ReturnsFull_ForUnrecognizedValue()
        => Assert.AreEqual("Full", HangDumpCommandLineProvider.MapToSupportedDumpType("Unknown"));
}
