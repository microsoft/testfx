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
    public const string TrxModeOptionName = "trx-mode";
    public const string InProcessMode = "in-process";
    public const string OutOfProcessMode = "out-of-process";

    public TrxReportGeneratorCommandLine()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "TrxReportGeneratorCommandLine",
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.TrxReportGeneratorDisplayName,
            ExtensionResources.TrxReportGeneratorDescription,
            [
                new(TrxReportOptionName, ExtensionResources.TrxReportOptionDescription, ArgumentArity.Zero, false),
                new(TrxReportFileNameOptionName, ExtensionResources.TrxReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
                new(TrxModeOptionName, ExtensionResources.TrxModeOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            TrxReportFileNameOptionName => ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                ".trx",
                ExtensionResources.TrxReportFileNameMustNotBeEmpty,
                ExtensionResources.TrxReportFileNameExtensionIsNotTrx,
                ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory),
            TrxModeOptionName => ValidateTrxModeAsync(arguments),
            _ => ValidationResult.ValidTask,
        };

    private static Task<ValidationResult> ValidateTrxModeAsync(string[] arguments)
        => arguments is not [string mode]
            || (!InProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase)
                && !OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase))
                ? ValidationResult.InvalidTask(ExtensionResources.TrxModeInvalidArgument)
                : OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase)
                    && !TrxModeHelpers.IsTestHostControllerSupported
                        ? ValidationResult.InvalidTask(ExtensionResources.TrxModeOutOfProcessNotSupported)
                        : ValidationResult.ValidTask;

    public override async Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        ValidationResult reportValidation = await ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            TrxReportOptionName,
            TrxReportFileNameOptionName,
            ExtensionResources.TrxReportFileNameRequiresTrxReport,
            ExtensionResources.TrxReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey).ConfigureAwait(false);
        return !reportValidation.IsValid
            ? reportValidation
            : commandLineOptions.IsOptionSet(TrxModeOptionName)
                && !commandLineOptions.IsOptionSet(TrxReportOptionName)
                    ? ValidationResult.Invalid(ExtensionResources.TrxModeRequiresTrxReport)
                    : ValidationResult.Valid();
    }
}
