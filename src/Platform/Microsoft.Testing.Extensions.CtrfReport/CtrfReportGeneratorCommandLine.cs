// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string CtrfReportOptionName = "report-ctrf";
    public const string CtrfReportFileNameOptionName = "report-ctrf-filename";

    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public CtrfReportGeneratorCommandLine()
        : base(
            nameof(CtrfReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.CtrfReportGeneratorDisplayName,
            ExtensionResources.CtrfReportGeneratorDescription,
            [
                new(CtrfReportOptionName, ExtensionResources.CtrfReportOptionDescription, ArgumentArity.Zero, false),
                new(CtrfReportFileNameOptionName, ExtensionResources.CtrfReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == CtrfReportFileNameOptionName)
        {
            if (arguments.Length is 0)
            {
                return ValidationResult.InvalidTask(ExtensionResources.CtrfReportFileNameMustNotBeEmpty);
            }

            string argument = arguments[0];

            string fileNamePart = Path.GetFileName(argument);
            if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
            {
                return ValidationResult.InvalidTask(ExtensionResources.CtrfReportFileNameMustNotBeEmpty);
            }

            if (!fileNamePart.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.CtrfReportFileNameExtensionIsNotJson);
            }

            if (EscapesResultsDirectory(argument))
            {
                return ValidationResult.InvalidTask(ExtensionResources.CtrfReportFileNameRelativePathMustStayUnderResultsDirectory);
            }
        }

        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(CtrfReportFileNameOptionName) && !commandLineOptions.IsOptionSet(CtrfReportOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.CtrfReportFileNameRequiresCtrfReport)
            : commandLineOptions.IsOptionSet(CtrfReportOptionName) && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? ValidationResult.InvalidTask(ExtensionResources.CtrfReportIsNotValidForDiscovery)
                : ValidationResult.ValidTask;

    private static bool EscapesResultsDirectory(string path)
    {
        // Fully-qualified paths (e.g. "C:\foo.json", "\\server\share\foo.json" or "/foo.json") are
        // accepted as-is and validated by the OS when we open the file - the user explicitly opted
        // out of writing under the test results directory.
        if (IsPathFullyQualified(path))
        {
            return false;
        }

        // Drive-relative paths on Windows such as "C:foo.json" are "rooted" but not fully qualified -
        // they resolve against the current directory of the drive, which is unpredictable and would
        // silently escape the test results directory. Reject them. On non-Windows OSes
        // Path.IsPathRooted only returns true for paths starting with "/", which are already handled
        // above, so this check is effectively Windows-only and matches the TRX option behavior.
        if (Path.IsPathRooted(path))
        {
            return true;
        }

        // Any remaining ".." segment in a relative path would escape the test results directory.
        return path.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..");
    }

    private static bool IsPathFullyQualified(string path)
    {
#if NETCOREAPP
        return Path.IsPathFullyQualified(path);
#else
        // Mirrors the runtime implementation that is missing on .NET Framework and netstandard2.0.
        if (path.Length < 2)
        {
            return false;
        }

        // UNC paths like "\\server\share" (or with forward slashes).
        if (IsDirectorySeparator(path[0]) && IsDirectorySeparator(path[1]))
        {
            return true;
        }

        // On Unix, only paths starting with "/" are fully qualified.
        if (Path.DirectorySeparatorChar == '/')
        {
            return path[0] == '/';
        }

        // On Windows, fully qualified drive paths must be "X:\" or "X:/".
        return path.Length >= 3
            && IsValidDriveLetter(path[0])
            && path[1] == ':'
            && IsDirectorySeparator(path[2]);

        static bool IsDirectorySeparator(char c)
            => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        static bool IsValidDriveLetter(char c)
            => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
#endif
    }
}
