// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : ICommandLineOptionsProvider
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";

    /// <inheritdoc />
    public string Uid { get; } = nameof(TrxReportGeneratorCommandLine);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(TrxReportOptionName, ExtensionResources.TrxReportOptionDescription, ArgumentArity.Zero, false),
            new(TrxReportFileNameOptionName, ExtensionResources.TrxReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        };

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == TrxReportFileNameOptionName)
        {
            if (!arguments[0].EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameExtensionIsNotTrx);
            }

            if (!RoslynString.IsNullOrEmpty(Path.GetDirectoryName(arguments[0])))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameShouldNotContainPath);
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet(TrxReportFileNameOptionName)
            && !commandLineOptions.IsOptionSet(TrxReportOptionName))
        {
            return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameRequiresTrxReport);
        }

        if (commandLineOptions.IsOptionSet(TrxReportOptionName)
            && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return ValidationResult.InvalidTask(ExtensionResources.TrxReportIsNotValidForDiscovery);
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
