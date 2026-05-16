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
            string fileName = arguments[0];

            // Validate "pure file name" first. We don't want any path component, drive letter,
            // parent directory traversal, leading/trailing whitespace or invalid file name char.
            if (!IsValidPureFileName(fileName))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameShouldNotContainPath);
            }

            if (!fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameExtensionIsNotHtml);
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet(HtmlReportFileNameOptionName)
            && !commandLineOptions.IsOptionSet(HtmlReportOptionName))
        {
            return ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameRequiresHtmlReport);
        }

        return commandLineOptions.IsOptionSet(HtmlReportOptionName)
            && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportIsNotValidForDiscovery)
            : ValidationResult.ValidTask;
    }

    // We are intentionally strict here so that we cannot be tricked across platforms.
    // The argument must be a "pure" file name: no directory separator, no drive letter,
    // no parent directory traversal, no invalid file name character, no leading/trailing
    // whitespace.
    private static bool IsValidPureFileName(string fileName)
    {
        if (RoslynString.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName != fileName.Trim())
        {
            return false;
        }

        if (fileName == "." || fileName == ".." || fileName.Contains(".."))
        {
            return false;
        }

        foreach (char c in fileName)
        {
            if (c is '/' or '\\' or ':')
            {
                return false;
            }
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            if (fileName.IndexOf(invalid) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
