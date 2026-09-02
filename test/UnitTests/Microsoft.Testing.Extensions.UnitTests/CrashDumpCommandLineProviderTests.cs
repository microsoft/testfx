// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpCommandLineProviderTests
{
    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenDumpTypeIsUnknownAsync()
    {
        CrashDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["Unknown"]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenDumpTypeIsKnownAsync(string value)
    {
        CrashDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("")]
    public async Task ValidateOptionArgumentsAsync_ReturnsInvalid_WhenCrashSequenceIsNotBooleanAsync(string value)
    {
        CrashDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == CrashDumpCommandLineOptions.CrashSequenceOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(CrashDumpResources.CrashSequenceOptionInvalidArgument, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("false")]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_WhenCrashSequenceIsBooleanAsync(string value)
    {
        CrashDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == CrashDumpCommandLineOptions.CrashSequenceOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ReturnsValid_ForFileNameWithoutDmpExtensionAsync()
    {
        // The provider intentionally does not enforce a '.dmp' extension, so arbitrary names
        // (e.g. with a custom suffix appended by an outer wrapper script) must be accepted.
        CrashDumpCommandLineProvider provider = new();
        CommandLineOption option = provider.GetCommandLineOptions().Single(o => o.Name == CrashDumpCommandLineOptions.CrashDumpFileNameOptionName);
        ValidationResult validationResult = await provider.ValidateOptionArgumentsAsync(option, ["dump-20240101.custom"]).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpTypeOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashSequenceOptionName)]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenSubOptionIsUsedWithoutMainOptionAsync(string subOption)
    {
        CrashDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [subOption] = ["true"],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(CrashDumpResources.MissingCrashDumpMainOption, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenSubOptionIsUsedWithCrashDumpAsync()
    {
        // --crashreport's validity also depends on the current OS platform (it is unsupported on
        // Windows), so this uses --crashdump as the main option to keep the test OS-independent.
        CrashDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
            [CrashDumpCommandLineOptions.CrashDumpFileNameOptionName] = ["dump.dmp"],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsInvalid_WhenCrashReportAndIfSupportedAreBothUsedAsync()
    {
        CrashDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [CrashDumpCommandLineOptions.CrashReportOptionName] = [],
            [CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName] = [],
        })).ConfigureAwait(false);

        Assert.IsFalse(validationResult.IsValid);
        Assert.AreEqual(CrashDumpResources.CrashReportAndIfSupportedAreMutuallyExclusiveErrorMessage, validationResult.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsValid_WhenOnlyCrashDumpIsUsedAsync()
    {
        CrashDumpCommandLineProvider provider = new();
        ValidationResult validationResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
        })).ConfigureAwait(false);

        Assert.IsTrue(validationResult.IsValid, validationResult.ErrorMessage);
    }
}
