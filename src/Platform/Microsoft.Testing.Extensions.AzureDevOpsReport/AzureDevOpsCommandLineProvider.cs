// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Reporting;

internal sealed class AzureDevOpsCommandLineProvider : ICommandLineOptionsProvider
{
    private static readonly string[] SeverityOptions = ["error", "warning"];

    public string Uid => nameof(AzureDevOpsCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName, AzureDevOpsResources.OptionDescription, ArgumentArity.Zero, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, AzureDevOpsResources.SeverityOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity)
        {
            if (!SeverityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSeverity, arguments[0]));
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (!commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName) &&
            commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity))
        {
            // If report-azdo is not set, but report-azdo-severity is set, it's invalid.
            return ValidationResult.InvalidTask(AzureDevOpsResources.AzureDevOpsReportSeverityRequiresAzureDevOps);
        }

        return ValidationResult.ValidTask;
    }
}
