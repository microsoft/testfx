// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string CtrfReportOptionName = "report-ctrf";
    public const string CtrfReportFileNameOptionName = "report-ctrf-filename";

    public CtrfReportGeneratorCommandLine()
        : base(
            nameof(CtrfReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.CtrfReportGeneratorDisplayName,
            ExtensionResources.CtrfReportGeneratorDescription,
            [
                new(CtrfReportOptionName, ExtensionResources.CtrfReportOptionDescription, ArgumentArity.Zero, false),
                new(CtrfReportFileNameOptionName, ExtensionResources.CtrfReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == CtrfReportFileNameOptionName
            ? ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                ".json",
                ExtensionResources.CtrfReportFileNameMustNotBeEmpty,
                ExtensionResources.CtrfReportFileNameExtensionIsNotJson,
                ExtensionResources.CtrfReportFileNameRelativePathMustStayUnderResultsDirectory)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            CtrfReportOptionName,
            CtrfReportFileNameOptionName,
            ExtensionResources.CtrfReportFileNameRequiresCtrfReport,
            ExtensionResources.CtrfReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
