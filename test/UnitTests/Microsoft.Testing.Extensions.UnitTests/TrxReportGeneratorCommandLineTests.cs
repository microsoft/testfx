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
    [DataRow("in-process")]
    [DataRow("out-of-process")]
    [DataRow("IN-PROCESS")]
    [DataRow("OUT-OF-PROCESS")]
    public async Task IsValid_When_Mode_Is_Supported(string mode)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().Single(x => x.Name == TrxReportGeneratorCommandLine.TrxModeOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [mode]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(new string[] { })]
    [DataRow(new[] { "" })]
    [DataRow(new[] { "invalid" })]
    [DataRow(new[] { "in-process", "out-of-process" })]
    public async Task IsInvalid_When_Mode_Arguments_Are_Invalid(string[] arguments)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().Single(x => x.Name == TrxReportGeneratorCommandLine.TrxModeOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, arguments).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxModeInvalidValue, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("in-process", false)]
    [DataRow("out-of-process", false)]
    [DataRow("in-process", true)]
    [DataRow("out-of-process", true)]
    public async Task Mode_Requires_ReportTrx(string mode, bool reportEnabled)
    {
        var provider = new TrxReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [TrxReportGeneratorCommandLine.TrxModeOptionName] = [mode],
        };
        if (reportEnabled)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxReportOptionName, []);
        }

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.AreEqual(reportEnabled, result.IsValid, result.ErrorMessage);
        if (!reportEnabled)
        {
            Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxModeRequiresTrxReport, result.ErrorMessage);
        }
    }

    [TestMethod]
    [DataRow("foo.trx")]
    [DataRow("sub/foo.trx")]
    [DataRow("sub\\foo.trx")]
    public async Task IsValid_If_TrxFile_And_FileNameOrNestedPath_Is_Provided(string filename)
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        validateOptionsResult.AssertIsValid();
    }

    [TestMethod]
    public async Task IsValid_If_TrxFile_Has_Absolute_Path()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);
        string filename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
        validateOptionsResult.AssertIsValid();
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
    public async Task IsInvalid_If_TrxFile_RelativePath_Escapes_TestResultsDirectory()
    {
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        foreach (string filename in new[] { "../foo.trx", Path.Combine("nested", "..", "foo.trx") })
        {
            ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
            Assert.IsFalse(validateOptionsResult.IsValid, filename);
            Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory, validateOptionsResult.ErrorMessage, filename);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsInvalid_If_TrxFile_Uses_DriveRelativePath_OnWindows()
    {
        // Drive-relative paths such as "C:foo.trx" are "rooted" but not fully qualified, so they would
        // silently escape the test results directory. Validate that they are rejected on Windows.
        var provider = new TrxReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TrxReportGeneratorCommandLine.TrxReportFileNameOptionName);

        foreach (string filename in new[] { "C:foo.trx", "C:..\\foo.trx" })
        {
            ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [filename]).ConfigureAwait(false);
            Assert.IsFalse(validateOptionsResult.IsValid, filename);
            Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory, validateOptionsResult.ErrorMessage, filename);
        }
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
        validateOptionsResult.AssertIsValid();
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
