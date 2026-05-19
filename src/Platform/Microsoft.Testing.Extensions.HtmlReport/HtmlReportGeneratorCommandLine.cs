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
        => commandLineOptions.IsOptionSet(HtmlReportFileNameOptionName) && !commandLineOptions.IsOptionSet(HtmlReportOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportFileNameRequiresHtmlReport)
            : commandLineOptions.IsOptionSet(HtmlReportOptionName) && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? ValidationResult.InvalidTask(ExtensionResources.HtmlReportIsNotValidForDiscovery)
                : ValidationResult.ValidTask;

    // We are intentionally strict here so that we cannot be tricked across platforms.
    // The argument must be a "pure" file name: no directory separator, no drive letter,
    // no parent directory traversal, no invalid file name character, no leading/trailing
    // whitespace, no Windows reserved device name. We use a hard-coded list of invalid
    // characters (a superset of Path.GetInvalidFileNameChars() on Linux + Windows) so
    // the same input is rejected regardless of the host OS.
    private static readonly char[] InvalidFileNameChars =
    [
        '\0', '/', '\\', ':', '*', '?', '"', '<', '>', '|',
        '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
        '\b', '\t', '\n', '\u000b', '\u000c', '\r',
        '\u000e', '\u000f', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014',
        '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001a', '\u001b',
        '\u001c', '\u001d', '\u001e', '\u001f',
    ];

    // Windows reserved device names. CreateFile on Windows will redirect a file
    // named e.g. CON.html to the actual device. Rejecting them up-front means the
    // option doesn't pass validation but then explode later in WriteAsync.
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

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
            if (Array.IndexOf(InvalidFileNameChars, c) >= 0)
            {
                return false;
            }
        }

        // Disallow Windows device names independent of host OS so the option is
        // consistently rejected. We compare against the bare name (without extension)
        // because e.g. "CON.html" maps to the CON device.
        string bareName = fileName;
        int dot = bareName.IndexOf('.');
        if (dot >= 0)
        {
            bareName = bareName.Substring(0, dot);
        }

        foreach (string reserved in WindowsReservedNames)
        {
            if (string.Equals(bareName, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
