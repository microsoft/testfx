// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";
    public const string TrxModeOptionName = "trx-mode";
    public const string InProcessMode = "in-process";
    public const string OutOfProcessMode = "out-of-process";

    private readonly bool _isTestHostControllerSupported;
    private readonly IEnvironment _environment;

    public TrxReportGeneratorCommandLine()
        : this(TrxModeHelpers.IsTestHostControllerSupported, new SystemEnvironment())
    {
    }

    internal TrxReportGeneratorCommandLine(bool isTestHostControllerSupported, IEnvironment environment)
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
        _isTestHostControllerSupported = isTestHostControllerSupported;
        _environment = environment;
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
            TrxModeOptionName => arguments is [string mode]
                && (InProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase) || OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase))
                    ? ValidationResult.ValidTask
                    : ValidationResult.InvalidTask(ExtensionResources.TrxModeInvalidValue),
            _ => ValidationResult.ValidTask,
        };

    public override async Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        ValidationResult result = await ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            TrxReportOptionName,
            TrxReportFileNameOptionName,
            ExtensionResources.TrxReportFileNameRequiresTrxReport,
            ExtensionResources.TrxReportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return result;
        }

        if (commandLineOptions.IsOptionSet(TrxReportOptionName))
        {
            string? mode = TrxModeHelpers.GetEffectiveMode(commandLineOptions, _environment);
            if (OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase)
                && !_isTestHostControllerSupported)
            {
                return ValidationResult.Invalid(ExtensionResources.TrxModeOutOfProcessNotSupported);
            }
        }
        else if (commandLineOptions.IsOptionSet(TrxModeOptionName))
        {
            return ValidationResult.Invalid(ExtensionResources.TrxModeRequiresTrxReport);
        }

        return ValidationResult.Valid();
    }
}
