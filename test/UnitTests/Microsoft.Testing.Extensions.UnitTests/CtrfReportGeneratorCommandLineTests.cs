// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfReportGeneratorCommandLineTests
{
    [TestMethod]
    [DataRow("report.json")]
    [DataRow("report.ctrf.json")]
    [DataRow("sub/report.json")]
    public async Task IsValid_If_JsonFileNameOrNestedRelativePath_Is_Provided(string fileName)
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsValid_If_JsonFileNameUsesBackslashSeparator_OnWindows()
    {
        // The '\' character is only a directory separator on Windows; on Unix it would be treated
        // as part of the leaf file name (and later sanitized at write time).
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["sub\\report.json"]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    public async Task IsValid_If_JsonFile_Has_Absolute_Path()
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);
        string fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [DataRow("report.txt")] // wrong extension
    [DataRow("report")] // no extension
    [DataRow("REPORT.JSO")] // wrong extension (truncated)
    [DataRow("report.html")] // html is not json
    public async Task IsInvalid_If_FileName_Does_Not_End_With_Json(string fileName)
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameExtensionIsNotJson, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("../report.json")]
    [DataRow("nested/../report.json")]
    public async Task IsInvalid_If_RelativePath_Contains_ParentDirectorySegment(string fileName)
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameRelativePathMustStayUnderResultsDirectory, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsInvalid_If_JsonFile_Uses_DriveRelativePath_OnWindows()
    {
        // Drive-relative paths such as "C:report.json" are "rooted" but not fully qualified, so they
        // would silently escape the test results directory. Validate that they are rejected on Windows.
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["C:report.json"]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameRelativePathMustStayUnderResultsDirectory, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("sub/")]
    [DataRow("sub/ ")]
    public async Task IsInvalid_If_FileNamePart_Is_Empty_Or_Whitespace(string fileName)
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameMustNotBeEmpty, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_No_Argument_Provided()
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameMustNotBeEmpty, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_FileName_Provided_Without_CtrfReport_Flag()
    {
        var provider = new CtrfReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName] = ["report.json"],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportFileNameRequiresCtrfReport, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_CtrfReport_Used_With_DiscoverTests()
    {
        var provider = new CtrfReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [CtrfReportGeneratorCommandLine.CtrfReportOptionName] = [],
            [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(CtrfReport.Resources.ExtensionResources.CtrfReportIsNotValidForDiscovery, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_CtrfReport_Used_Alone()
    {
        var provider = new CtrfReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [CtrfReportGeneratorCommandLine.CtrfReportOptionName] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("report*.json")]
    [DataRow("CON.json")]
    public async Task IsValid_When_FileName_WillBeSanitized_AtWriteTime(string fileName)
    {
        var provider = new CtrfReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
    }
}
