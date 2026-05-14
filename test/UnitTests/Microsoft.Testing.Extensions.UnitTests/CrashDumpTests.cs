// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpTests
{
    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    public async Task IsValid_If_CrashDumpType_Has_CorrectValue(string crashDumpType)
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [crashDumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvValid_If_CrashDumpType_Has_IncorrectValue()
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpTypeOptionInvalidType, "invalid"), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task CrashDump_CommandLineOptions_Are_Valid_ByDefault()
    {
        var provider = new CrashDumpCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task CrashReport_Without_CrashDump_Is_Invalid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(CrashDumpResources.CrashReportRequiresCrashDumpErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task CrashReport_Alongside_CrashDump_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow(CrashDumpCommandLineOptions.CrashDumpOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashReportOptionName)]
    [TestMethod]
    public async Task CrashReportOnly_Cannot_Be_Combined_With_Other_Crash_Options(string otherCrashOption)
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { otherCrashOption, [] },
            { CrashDumpCommandLineOptions.CrashReportOnlyOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(CrashDumpResources.CrashReportOnlyCannotBeCombinedErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task CrashReportOnly_Alone_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOnlyOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }
}
