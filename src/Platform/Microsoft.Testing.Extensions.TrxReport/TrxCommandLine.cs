// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";

    public TrxReportGeneratorCommandLine()
        : base(
            nameof(TrxReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.TrxReportGeneratorDisplayName,
            ExtensionResources.TrxReportGeneratorDescription,
            [
                new(TrxReportOptionName, ExtensionResources.TrxReportOptionDescription, ArgumentArity.Zero, false),
                new(TrxReportFileNameOptionName, ExtensionResources.TrxReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false)
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == TrxReportFileNameOptionName
            ? global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                ".trx",
                ExtensionResources.TrxReportFileNameMustNotBeEmpty,
                ExtensionResources.TrxReportFileNameExtensionIsNotTrx,
                ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => global::Microsoft.Testing.Extensions.ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            TrxReportOptionName,
            TrxReportFileNameOptionName,
            ExtensionResources.TrxReportFileNameRequiresTrxReport,
            ExtensionResources.TrxReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
