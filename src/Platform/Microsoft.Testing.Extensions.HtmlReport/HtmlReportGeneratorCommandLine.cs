// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string HtmlReportOptionName = "report-html";
    public const string HtmlReportFileNameOptionName = "report-html-filename";

    public HtmlReportGeneratorCommandLine()
        : base(
            nameof(HtmlReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.HtmlReportGeneratorDisplayName,
            ExtensionResources.HtmlReportGeneratorDescription,
            [
                new(HtmlReportOptionName, ExtensionResources.HtmlReportOptionDescription, ArgumentArity.Zero, false),
                new(HtmlReportFileNameOptionName, ExtensionResources.HtmlReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == HtmlReportFileNameOptionName
            ? global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                ".html",
                ExtensionResources.HtmlReportFileNameMustNotBeEmpty,
                ExtensionResources.HtmlReportFileNameExtensionIsNotHtml,
                ExtensionResources.HtmlReportFileNameRelativePathMustStayUnderResultsDirectory)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            HtmlReportOptionName,
            HtmlReportFileNameOptionName,
            ExtensionResources.HtmlReportFileNameRequiresHtmlReport,
            ExtensionResources.HtmlReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
