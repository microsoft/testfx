// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportGeneratorCommandLine : ICommandLineOptionsProvider
{
    public const string HtmlReportOptionName = "report-html";
    public const string HtmlReportFileNameOptionName = "report-html-filename";

    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <inheritdoc />
    public string Uid => nameof(HtmlReportGeneratorCommandLine);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.HtmlReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.HtmlReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(HtmlReportOptionName, ExtensionResources.HtmlReportOptionDescription, ArgumentArity.Zero, false),
            new(HtmlReportFileNameOptionName, ExtensionResources.HtmlReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == HtmlReportFileNameOptionName)
        {
            if (arguments.Length is 0)
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameShouldNotContainPath);
            }

            string argument = arguments[0];

            string fileNamePart = Path.GetFileName(argument);
            if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameShouldNotContainPath);
            }

            if (!fileNamePart.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameExtensionIsNotHtml);
            }

            if (EscapesResultsDirectory(argument))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameShouldNotContainPath);
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(HtmlReportFileNameOptionName) && !commandLineOptions.IsOptionSet(HtmlReportOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameRequiresHtmlReport)
            : commandLineOptions.IsOptionSet(HtmlReportOptionName) && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportIsNotValidForDiscovery)
                : ValidationResult.ValidTask;

    private static bool EscapesResultsDirectory(string path)
        => !IsPathFullyQualified(path)
            && (Path.IsPathRooted(path)
                || path.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."));

    private static bool IsPathFullyQualified(string path)
    {
#if NETCOREAPP
        return Path.IsPathFullyQualified(path);
#else
        return path.Length >= 2
            && ((IsDirectorySeparator(path[0]) && IsDirectorySeparator(path[1]))
            || (Path.DirectorySeparatorChar == '/'
                ? path[0] == '/'
                : path.Length >= 3
                    && IsValidDriveLetter(path[0])
                    && path[1] == ':'
                    && IsDirectorySeparator(path[2])));

        static bool IsDirectorySeparator(char c)
            => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        static bool IsValidDriveLetter(char c)
            => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
#endif
    }
}
