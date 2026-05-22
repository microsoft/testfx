// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : ICommandLineOptionsProvider
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";

    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <inheritdoc />
    public string Uid => nameof(TrxReportGeneratorCommandLine);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(TrxReportOptionName, ExtensionResources.TrxReportOptionDescription, ArgumentArity.Zero, false),
            new(TrxReportFileNameOptionName, ExtensionResources.TrxReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false)
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == TrxReportFileNameOptionName)
        {
            if (arguments.Length is 0)
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameMustNotBeEmpty);
            }

            string argument = arguments[0];

            // We accept relative or absolute paths, but the leaf must be a non-empty file name
            // that ends with ".trx". Relative paths must stay under the test results directory,
            // while the directory portion of valid paths is treated as a literal path and validated
            // by the OS when we open the file.
            string fileNamePart = Path.GetFileName(argument);
            if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameMustNotBeEmpty);
            }

            if (!fileNamePart.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameExtensionIsNotTrx);
            }

            if (EscapesResultsDirectory(argument))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory);
            }
        }

        return ValidationResult.ValidTask;
    }

    private static bool EscapesResultsDirectory(string path)
    {
        // Fully-qualified paths (e.g. "C:\foo.trx", "\\server\share\foo.trx" or "/foo.trx") are accepted
        // as-is and validated by the OS when we open the file - the user explicitly opted out of writing
        // under the test results directory.
        if (IsPathFullyQualified(path))
        {
            return false;
        }

        // Drive-relative paths on Windows such as "C:foo.trx" are "rooted" but not fully qualified -
        // they resolve against the current directory of the drive, which is unpredictable and would
        // silently escape the test results directory. Reject them.
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
