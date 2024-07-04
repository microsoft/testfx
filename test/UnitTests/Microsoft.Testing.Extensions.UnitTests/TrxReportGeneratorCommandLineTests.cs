// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestGroup]
public class TrxReportGeneratorCommandLineTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task IsValid_If_TrxFile_And_Only_TargetFilename_Is_Provided()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);
        string filename = Path.GetRandomFileName() + ".trx";

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments(false, false)]
    [Arguments(true, true)]
    public async Task IsInvalid_If_TrxFile_And_Only_TargetFilename_Are_Not_Provided(bool isTrxFile, bool hasDirectory)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        string filename = Path.GetRandomFileName() + (isTrxFile ? ".trx" : string.Empty);
        if (hasDirectory)
        {
            filename = Path.Combine(Path.GetTempPath(), filename);
        }

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(isTrxFile ? TestReports.Resources.ExtensionResources.TrxReportFileNameShouldNotContainPath : TestReports.Resources.ExtensionResources.TrxReportFileNameExtensionIsNotTrx, validateOptionsResult.ErrorMessage);
    }

    [Arguments(false, false, true)]
    [Arguments(true, true, false)]
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

    [Arguments(true, false, false)]
    [Arguments(true, true, true)]
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
        Assert.AreEqual(isDiscoverTestsSet ? TestReports.Resources.ExtensionResources.TrxReportIsNotValidForDiscovery : TestReports.Resources.ExtensionResources.TrxReportFileNameRequiresTrxReport, validateOptionsResult.ErrorMessage);
    }
}
