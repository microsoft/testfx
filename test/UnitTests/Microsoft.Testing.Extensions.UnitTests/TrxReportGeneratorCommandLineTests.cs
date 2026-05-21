// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxReportGeneratorCommandLineTests
{
    [TestMethod]
    [DataRow("foo.trx")]
    [DataRow("sub/foo.trx")]
    [DataRow("sub\\foo.trx")]
    [DataRow("../foo.trx")]
    public async Task IsValid_If_TrxFile_And_FileNameOrRelativePath_Is_Provided(string filename)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsValid_If_TrxFile_Has_Absolute_Path()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);
        string filename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_TrxFile_Is_Not_Trx()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        string filename = Path.GetRandomFileName();

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportFileNameExtensionIsNotTrx, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("sub/")]
    [DataRow("/")]
    public async Task IsInvalid_If_TrxFile_Has_Empty_File_Name(string filename)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportFileNameMustNotBeEmpty, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_TrxFile_Name_Is_Missing()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportFileNameMustNotBeEmpty, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false, false, true)]
    [DataRow(true, true, false)]
    public async Task IsValid_When_TrxReport_TrxReportFile_Is_Provided_And_DiscoverTests_Not_Provided(bool isFileNameSet, bool isTrxSet, bool isDiscoverTestsSet)
    {
        var provider = new TrxReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>();
        if (isFileNameSet)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, []);
        }

        if (isTrxSet)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxReportOptionName, []);
        }

        if (isDiscoverTestsSet)
        {
            options.Add(PlatformCommandLineProvider.DiscoverTestsOptionKey, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(true, true, true)]
    public async Task IsInvalid_When_TrxReport_TrxReportFile_Is_Provided_And_DiscoverTests_Provided(bool isFileNameSet, bool isTrxSet, bool isDiscoverTestsSet)
    {
        var provider = new TrxReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>();

        if (isFileNameSet)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, []);
        }

        if (isTrxSet)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxReportOptionName, []);
        }

        if (isDiscoverTestsSet)
        {
            options.Add(PlatformCommandLineProvider.DiscoverTestsOptionKey, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(isDiscoverTestsSet ? TrxReport.Resources.ExtensionResources.TrxReportIsNotValidForDiscovery : TrxReport.Resources.ExtensionResources.TrxReportFileNameRequiresTrxReport, validateOptionsResult.ErrorMessage);
    }
}
