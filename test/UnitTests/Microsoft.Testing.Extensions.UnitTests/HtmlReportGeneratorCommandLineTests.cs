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
    public async Task IsValid_If_PureHtmlFileName_Is_Provided()
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["report.html"]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    [DataRow("report.txt")]              // wrong extension
    [DataRow("report")]                   // no extension
    [DataRow("REPORT.HTM")]               // wrong extension (htm vs html)
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
    [DataRow("sub/report.html")]
    [DataRow("sub\\report.html")]
    [DataRow("..\\report.html")]
    [DataRow("../report.html")]
    [DataRow("..report.html")]            // contains ".."
    [DataRow("C:report.html")]            // drive letter
    [DataRow(" report.html")]             // leading whitespace
    [DataRow("report.html ")]             // trailing whitespace
    public async Task IsInvalid_If_FileName_Contains_Path_Or_Invalid_Chars(string fileName)
    {
        var provider = new HtmlReportGeneratorCommandLine();
        Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions()
            .First(x => x.Name == HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [fileName]).ConfigureAwait(false);

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
}
