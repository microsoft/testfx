// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestGroup]
public class TrxCompareToolCommandLineTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    [Arguments(TrxCompareToolCommandLine.BaselineTrxOptionName)]
    [Arguments(TrxCompareToolCommandLine.TrxToCompareOptionName)]
    public async Task IsValid_When_Correct_TrxFile_IsProvided_For_Options(string optionName)
    {
        var provider = new TrxCompareToolCommandLine(new TestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);
        string filename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");
        File.WriteAllText(filename, string.Empty);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
        File.Delete(filename);
    }

    [Arguments(TrxCompareToolCommandLine.BaselineTrxOptionName, false)]
    [Arguments(TrxCompareToolCommandLine.BaselineTrxOptionName, true)]
    [Arguments(TrxCompareToolCommandLine.TrxToCompareOptionName, false)]
    [Arguments(TrxCompareToolCommandLine.TrxToCompareOptionName, true)]
    public async Task IsInvalid_When_Incorrect_TrxFile_IsProvided_For_Options(string optionName, bool isTrxFile)
    {
        var provider = new TrxCompareToolCommandLine(new TestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);
        string filename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + (isTrxFile ? ".trx" : string.Empty));

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, TestReports.Resources.ExtensionResources.TrxComparerToolOptionExpectsSingleArgument, optionName), validateOptionsResult.ErrorMessage);
    }

    public async Task IsValid_If_Both_TrxOptions_Are_Provided()
    {
        var provider = new TrxCompareToolCommandLine(new TestExtension());
        var options = new Dictionary<string, string[]>
        {
            { TrxCompareToolCommandLine.BaselineTrxOptionName, [] },
            { TrxCompareToolCommandLine.TrxToCompareOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task IsInvalid_If_Any_TrxOptions_Is_Missing(bool isBaseLineSet, bool isToCompareSet)
    {
        var provider = new TrxCompareToolCommandLine(new TestExtension());
        var options = new Dictionary<string, string[]>();
        if (isBaseLineSet)
        {
            options.Add(TrxCompareToolCommandLine.BaselineTrxOptionName, []);
        }

        if (isToCompareSet)
        {
            options.Add(TrxCompareToolCommandLine.TrxToCompareOptionName, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, TestReports.Resources.ExtensionResources.TrxComparerToolBothFilesMustBeSpecified, TrxCompareToolCommandLine.BaselineTrxOptionName, TrxCompareToolCommandLine.TrxToCompareOptionName), validateOptionsResult.ErrorMessage);
    }
}
