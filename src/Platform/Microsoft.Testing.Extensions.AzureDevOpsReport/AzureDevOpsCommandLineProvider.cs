// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.Reporting;

internal sealed class AzureDevOpsCommandLineProvider : ICommandLineOptionsProvider
{
    private static readonly string[] SeverityOptions = ["error", "warning"];

    public string Uid => nameof(AzureDevOpsCommandLineProvider);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName, AzureDevOpsResources.OptionDescription, ArgumentArity.Zero, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky, AzureDevOpsResources.DemoteKnownFlakyOptionDescription, ArgumentArity.Zero, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory, AzureDevOpsResources.FlakyHistoryOptionDescription, ArgumentArity.ExactlyOne, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, AzureDevOpsResources.QuarantineFileOptionDescription, ArgumentArity.ExactlyOne, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, AzureDevOpsResources.SeverityOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory => ValidateFlakyHistoryArgumentsAsync(arguments),
            AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity => ValidateSeverityArgumentsAsync(arguments),
            _ => ValidationResult.ValidTask,
        };

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        string? errorMessage = null;
        if (!commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName))
        {
            if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsDemoteKnownFlakyRequiresAzureDevOps;
            }
            else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsFlakyHistoryRequiresAzureDevOps;
            }
            else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsQuarantineFileRequiresAzureDevOps;
            }
            else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsReportSeverityRequiresAzureDevOps;
            }
        }
        else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky)
            && !commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory))
        {
            errorMessage = AzureDevOpsResources.AzureDevOpsDemoteKnownFlakyRequiresFlakyHistory;
        }

        return errorMessage is null
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(errorMessage);
    }

    private static Task<ValidationResult> ValidateFlakyHistoryArgumentsAsync(string[] arguments)
        => int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int days)
            && days is >= 1 and <= 90
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidFlakyHistoryDays, arguments[0]));

    private static Task<ValidationResult> ValidateSeverityArgumentsAsync(string[] arguments)
        => SeverityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSeverity, arguments[0]));
}
