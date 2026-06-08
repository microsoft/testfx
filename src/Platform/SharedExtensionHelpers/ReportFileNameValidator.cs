// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions;

internal static class ReportFileNameValidator
{
    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static Task<ValidationResult> ValidateReportFileNameArgument(
        string[] arguments,
        string expectedExtension,
        string emptyErrorMessage,
        string badExtensionErrorMessage,
        string escapesDirectoryErrorMessage)
    {
        if (arguments.Length is 0)
        {
            return ValidationResult.InvalidTask(emptyErrorMessage);
        }

        string argument = arguments[0];

        // We accept relative or absolute paths, but the leaf must be a non-empty file name
        // that ends with the expected extension. Relative paths must stay under the test
        // results directory, while the directory portion of valid paths is treated as a
        // literal path and validated by the OS when we open the file.
        string fileNamePart = Path.GetFileName(argument);
        if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
        {
            return ValidationResult.InvalidTask(emptyErrorMessage);
        }

        if (!fileNamePart.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.InvalidTask(badExtensionErrorMessage);
        }

        return EscapesResultsDirectory(argument)
            ? ValidationResult.InvalidTask(escapesDirectoryErrorMessage)
            : ValidationResult.ValidTask;
    }

    public static Task<ValidationResult> ValidateReportCommandLineOptions(
        ICommandLineOptions commandLineOptions,
        string reportOptionName,
        string reportFileNameOptionName,
        string fileNameRequiresReportErrorMessage,
        string reportIsNotValidForDiscoveryErrorMessage,
        string discoverTestsOptionName)
        => commandLineOptions.IsOptionSet(reportFileNameOptionName)
            && !commandLineOptions.IsOptionSet(reportOptionName)
            ? ValidationResult.InvalidTask(fileNameRequiresReportErrorMessage)
            : commandLineOptions.IsOptionSet(reportOptionName)
                && commandLineOptions.IsOptionSet(discoverTestsOptionName)
                ? ValidationResult.InvalidTask(reportIsNotValidForDiscoveryErrorMessage)
                : ValidationResult.ValidTask;

    public static bool EscapesResultsDirectory(string path)
    {
        // Fully-qualified paths (e.g. "C:\foo.ext", "\\server\share\foo.ext" or "/foo.ext") are accepted
        // as-is and validated by the OS when we open the file - the user explicitly opted out of writing
        // under the test results directory.
        if (IsPathFullyQualified(path))
        {
            return false;
        }

        // Drive-relative paths on Windows such as "C:foo.ext" are "rooted" but not fully qualified -
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
}
