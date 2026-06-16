// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.Reporting;

internal sealed class AzureDevOpsCommandLineProvider : CommandLineOptionsProviderBase
{
    private static readonly string[] ArtifactUploadModes =
    [
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeOff,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll,
    ];

    private static readonly string[] SeverityOptions = ["error", "warning"];

    internal const int MaxStackFrameFilterPatterns = 16;
    internal const int StackFrameFilterMatchTimeoutMs = 500;

    private static readonly string DemoteKnownFlakyOptionDescriptionFormatted = string.Format(
        CultureInfo.InvariantCulture,
        AzureDevOpsResources.DemoteKnownFlakyOptionDescription,
        AzureDevOpsReporter.KnownFlakyFailureRateThreshold * 100);

    private static readonly string StackFrameFilterOptionDescriptionFormatted = string.Format(
        CultureInfo.InvariantCulture,
        AzureDevOpsResources.StackFrameFilterOptionDescription,
        MaxStackFrameFilterPatterns,
        StackFrameFilterMatchTimeoutMs);

    public AzureDevOpsCommandLineProvider()
        : base(
            nameof(AzureDevOpsCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            AzureDevOpsResources.DisplayName,
            AzureDevOpsResources.Description,
            [
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName, AzureDevOpsResources.OptionDescription, ArgumentArity.Zero, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky, DemoteKnownFlakyOptionDescriptionFormatted, ArgumentArity.Zero, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory, AzureDevOpsResources.FlakyHistoryOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, AzureDevOpsResources.QuarantineFileOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, AzureDevOpsResources.SeverityOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter, StackFrameFilterOptionDescriptionFormatted, ArgumentArity.OneOrMore, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsSummary, AzureDevOpsResources.SummaryOptionDescription, ArgumentArity.ZeroOrOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, AzureDevOpsResources.UploadArtifactExcludeOptionDescription, ArgumentArity.ZeroOrMore, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, AzureDevOpsResources.UploadArtifactIncludeOptionDescription, ArgumentArity.ZeroOrMore, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName, AzureDevOpsResources.UploadArtifactNameOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts, AzureDevOpsResources.UploadArtifactsOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, AzureDevOpsResources.PublishAzdoRunNameOptionDescription, ArgumentArity.ExactlyOne, false),
                new CommandLineOption(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName, AzureDevOpsResources.PublishAzdoTestResultsOptionDescription, ArgumentArity.Zero, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory => ValidateFlakyHistoryArgumentsAsync(arguments),
            AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity when !SeverityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSeverity, arguments[0])),
            AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter => ValidateStackFrameFilterArgumentsAsync(arguments),
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts when !ArtifactUploadModes.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidArtifactUploadMode, arguments[0])),
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude or AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude
                => ValidateGlobPatternsAsync(arguments),
            _ => ValidationResult.ValidTask,
        };

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
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
            else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsStackFrameFilterRequiresAzureDevOps;
            }
            else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsSummary))
            {
                errorMessage = AzureDevOpsResources.AzureDevOpsSummaryRequiresAzureDevOps;
            }
        }
        else if (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky)
            && !commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory))
        {
            errorMessage = AzureDevOpsResources.AzureDevOpsDemoteKnownFlakyRequiresFlakyHistory;
        }

        if (errorMessage is null && HasArtifactUploadConfiguration(commandLineOptions) && IsArtifactUploadDisabled(commandLineOptions))
        {
            errorMessage = AzureDevOpsResources.ArtifactUploadOptionsRequireUploadArtifacts;
        }

        if (errorMessage is null
            && commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName)
            && !commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName))
        {
            errorMessage = AzureDevOpsResources.PublishAzdoRunNameRequiresPublishAzdoTestResults;
        }

        return errorMessage is null
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(errorMessage);
    }

    private static bool HasArtifactUploadConfiguration(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude)
            || commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude)
            || commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName);

    private static bool IsAbsolutePattern(string pattern)
        => pattern.Length > 0
            && (pattern[0] is '/' or '\\'
                || (pattern.Length >= 2 && pattern[1] == ':'));

    private static bool IsArtifactUploadDisabled(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts, out string[]? arguments)
            || (arguments is [string argument]
                && AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeOff.Equals(argument, StringComparison.OrdinalIgnoreCase));

    private static Task<ValidationResult> ValidateGlobPatternsAsync(string[] arguments)
    {
        foreach (string argument in arguments)
        {
            if (RoslynString.IsNullOrWhiteSpace(argument) || IsAbsolutePattern(argument))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidArtifactUploadGlob, argument));
            }

            try
            {
                var matcher = new Matcher(PathComparison.Comparison);
                matcher.AddInclude(NormalizePattern(argument));
            }
            catch (ArgumentException)
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidArtifactUploadGlob, argument));
            }
        }

        return ValidationResult.ValidTask;
    }

    private static string NormalizePattern(string pattern)
        => pattern.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static Task<ValidationResult> ValidateFlakyHistoryArgumentsAsync(string[] arguments)
        => int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int days)
            && days is >= 1 and <= 90
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidFlakyHistoryDays, arguments[0]));

    private static Task<ValidationResult> ValidateStackFrameFilterArgumentsAsync(string[] arguments)
    {
        if (arguments.Length > MaxStackFrameFilterPatterns)
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.StackFrameFilterTooManyRegexes, MaxStackFrameFilterPatterns));
        }

        foreach (string pattern in arguments)
        {
            if (RoslynString.IsNullOrEmpty(pattern))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidStackFrameFilterRegex, pattern, "pattern is empty"));
            }

            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(StackFrameFilterMatchTimeoutMs));
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidStackFrameFilterRegex, pattern, ex.Message));
            }
        }

        return ValidationResult.ValidTask;
    }
}
