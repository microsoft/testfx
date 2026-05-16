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

internal sealed class AzureDevOpsCommandLineProvider : ICommandLineOptionsProvider
{
    private static readonly string[] ArtifactUploadModes =
    [
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeOff,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles,
        AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll,
    ];

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
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, AzureDevOpsResources.SeverityOptionDescription, ArgumentArity.ExactlyOne, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, AzureDevOpsResources.UploadArtifactExcludeOptionDescription, ArgumentArity.ZeroOrMore, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, AzureDevOpsResources.UploadArtifactIncludeOptionDescription, ArgumentArity.ZeroOrMore, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName, AzureDevOpsResources.UploadArtifactNameOptionDescription, ArgumentArity.ExactlyOne, false),
            new CommandLineOption(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts, AzureDevOpsResources.UploadArtifactsOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity when !SeverityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidSeverity, arguments[0])),
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts when !ArtifactUploadModes.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                => ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.InvalidArtifactUploadMode, arguments[0])),
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude or AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude
                => ValidateGlobPatternsAsync(arguments),
            _ => ValidationResult.ValidTask,
        };

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            && commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity)
                ? ValidationResult.InvalidTask(AzureDevOpsResources.AzureDevOpsReportSeverityRequiresAzureDevOps)
                : HasArtifactUploadConfiguration(commandLineOptions) && IsArtifactUploadDisabled(commandLineOptions)
                    ? ValidationResult.InvalidTask(AzureDevOpsResources.ArtifactUploadOptionsRequireUploadArtifacts)
                    : ValidationResult.ValidTask;

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
}
