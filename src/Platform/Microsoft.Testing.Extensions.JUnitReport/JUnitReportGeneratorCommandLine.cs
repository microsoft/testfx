// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string JUnitReportOptionName = "report-junit";
    public const string JUnitReportFileNameOptionName = "report-junit-filename";

    public JUnitReportGeneratorCommandLine()
        : base(
            nameof(JUnitReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.JUnitReportGeneratorDisplayName,
            ExtensionResources.JUnitReportGeneratorDescription,
            [
                new(JUnitReportOptionName, ExtensionResources.JUnitReportOptionDescription, ArgumentArity.Zero, false),
                new(JUnitReportFileNameOptionName, ExtensionResources.JUnitReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == JUnitReportFileNameOptionName
            ? global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                ".xml",
                ExtensionResources.JUnitReportFileNameMustNotBeEmpty,
                ExtensionResources.JUnitReportFileNameExtensionIsNotXml,
                ExtensionResources.JUnitReportFileNameRelativePathMustStayUnderResultsDirectory)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            JUnitReportOptionName,
            JUnitReportFileNameOptionName,
            ExtensionResources.JUnitReportFileNameRequiresJUnitReport,
            ExtensionResources.JUnitReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
