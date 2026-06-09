// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportGeneratorCommandLineTests
{
    [TestMethod]
    [DataRow("report.xml")]
    [DataRow("sub/report.xml")]
    public async Task IsValid_If_JUnitFileNameOrNestedRelativePath_Is_Provided(string fileName)
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsValid_If_JUnitFileNameUsesBackslashSeparator_OnWindows()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["sub\\report.xml"]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    public async Task IsValid_If_JUnitFile_Has_Absolute_Path()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);
        string fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [DataRow("report.txt")]
    [DataRow("report")]
    public async Task IsInvalid_If_FileName_Does_Not_End_With_Xml(string fileName)
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameExtensionIsNotXml, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("../report.xml")]
    [DataRow("nested/../report.xml")]
    public async Task IsInvalid_If_RelativePath_Contains_ParentDirectorySegment(string fileName)
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameRelativePathMustStayUnderResultsDirectory, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsInvalid_If_JUnitFile_Uses_DriveRelativePath_OnWindows()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["C:report.xml"]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameRelativePathMustStayUnderResultsDirectory, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("sub/")]
    [DataRow("sub/ ")]
    public async Task IsInvalid_If_FileNamePart_Is_Empty_Or_Whitespace(string fileName)
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameMustNotBeEmpty, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_No_Argument_Provided()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameMustNotBeEmpty, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_FileName_Provided_Without_JUnitReport_Flag()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName] = ["report.xml"],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportFileNameRequiresJUnitReport, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_JUnitReport_Used_With_DiscoverTests()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [JUnitReportGeneratorCommandLine.JUnitReportOptionName] = [],
            [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(JUnitReport.Resources.ExtensionResources.JUnitReportIsNotValidForDiscovery, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_JUnitReport_Used_Alone()
    {
        var provider = new JUnitReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [JUnitReportGeneratorCommandLine.JUnitReportOptionName] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
    }
}
