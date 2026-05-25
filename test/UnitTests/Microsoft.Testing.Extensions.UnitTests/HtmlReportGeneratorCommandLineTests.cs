// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HtmlReportGeneratorCommandLineTests
{
    [TestMethod]
    [DataRow("report.html")]
    [DataRow("sub/report.html")]
    public async Task IsValid_If_HtmlFileNameOrNestedRelativePath_Is_Provided(string fileName)
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsValid_If_HtmlFileNameUsesBackslashSeparator_OnWindows()
    {
        // The '\' character is only a directory separator on Windows; on Unix it would be treated
        // as part of the leaf file name (and later sanitized at write time).
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["sub\\report.html"]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    public async Task IsValid_If_HtmlFile_Has_Absolute_Path()
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);
        string fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".html");

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [DataRow("report.txt")] // wrong extension
    [DataRow("report")] // no extension
    [DataRow("REPORT.HTM")] // wrong extension (htm vs html)
    public async Task IsInvalid_If_FileName_Does_Not_End_With_Html(string fileName)
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(HtmlReport.Resources.ExtensionResources.HtmlReportFileNameExtensionIsNotHtml, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("../report.html")]
    [DataRow("nested/../report.html")]
    public async Task IsInvalid_If_RelativePath_Contains_ParentDirectorySegment(string fileName)
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(HtmlReport.Resources.ExtensionResources.HtmlReportFileNameShouldNotContainPath, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsInvalid_If_HtmlFile_Uses_DriveRelativePath_OnWindows()
    {
        // Drive-relative paths such as "C:report.html" are "rooted" but not fully qualified, so they
        // would silently escape the test results directory. Validate that they are rejected on Windows.
        // On non-Windows OSes ':' is a valid file-name character, so this check is Windows-only and
        // matches the TRX option behavior.
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["C:report.html"]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(HtmlReport.Resources.ExtensionResources.HtmlReportFileNameShouldNotContainPath, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_FileName_Provided_Without_HtmlReport_Flag()
    {
        var provider = new HtmlReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName] = ["report.html"],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(HtmlReport.Resources.ExtensionResources.HtmlReportFileNameRequiresHtmlReport, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_If_HtmlReport_Used_With_DiscoverTests()
    {
        var provider = new HtmlReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [HtmlReportGeneratorCommandLine.HtmlReportOptionName] = [],
            [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(HtmlReport.Resources.ExtensionResources.HtmlReportIsNotValidForDiscovery, result.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_HtmlReport_Used_Alone()
    {
        var provider = new HtmlReportGeneratorCommandLine();
        var options = new Dictionary<string, string[]>
        {
            [HtmlReportGeneratorCommandLine.HtmlReportOptionName] = [],
        };

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("report*.html")]
    [DataRow("CON.html")]
    public async Task IsValid_When_FileName_WillBeSanitized_AtWriteTime(string fileName)
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
    }
}
