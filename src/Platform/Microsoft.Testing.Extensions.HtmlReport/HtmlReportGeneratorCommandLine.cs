// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform;
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
    {
        if (commandOption.Name == HtmlReportFileNameOptionName)
        {
            if (arguments.Length is 0)
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameMustNotBeEmpty);
            }

            string argument = arguments[0];

            string fileNamePart = Path.GetFileName(argument);
            if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameMustNotBeEmpty);
            }

            if (!fileNamePart.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameExtensionIsNotHtml);
            }

            if (ReportFileNameValidator.EscapesResultsDirectory(argument))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameRelativePathMustStayUnderResultsDirectory);
            }
        }

        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(HtmlReportFileNameOptionName) && !commandLineOptions.IsOptionSet(HtmlReportOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameRequiresHtmlReport)
            : commandLineOptions.IsOptionSet(HtmlReportOptionName) && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportIsNotValidForDiscovery)
                : ValidationResult.ValidTask;
}
