// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

// Mirrors the extension-side Microsoft.Testing.Extensions.ReportFileNameSanitizer (linked source in
// src/Platform/SharedExtensionHelpers/ReportFileNameSanitizer.cs). The extension copy has SHIPPED
// InternalAPI entries in five extensions, so it cannot be moved without churn/risk. This core-internal
// copy exists pending future consolidation of the two.
internal static partial class ArtifactFileNameSanitizer
{
    private const char ReplacementChar = '_';
    private static readonly Regex ReservedFileNamesRegex = BuildReservedFileNameRegex();

    internal static string ReplaceInvalidFileNameChars(string fileName)
    {
        char[] result = new char[fileName.Length];

        for (int i = 0; i < fileName.Length; ++i)
        {
            result[i] = IsInvalidFileNameChar(fileName[i]) ? ReplacementChar : fileName[i];
        }

        string replaced = new string(result).TrimEnd();
        ArgumentGuard.Ensure(replaced.Length > 0, nameof(fileName), $"File name {fileName} is empty after sanitizing invalid characters.");

        if (IsReservedFileName(replaced))
        {
            replaced = ReplacementChar + replaced; // Cannot add to the end because it can have extensions.
        }

        return replaced;
    }

    private static bool IsInvalidFileNameChar(char c)
        => c is < ' ' or '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' or '@' or '(' or ')' or '^' or ' ';

    private static bool IsReservedFileName(string fileName) =>
        // CreateFile:
        // The following reserved device names cannot be used as the name of a file:
        // CON, PRN, AUX, NUL, COM1, COM2, COM3, COM4, COM5, COM6, COM7, COM8, COM9,
        // LPT1, LPT2, LPT3, LPT4, LPT5, LPT6, LPT7, LPT8, and LPT9.
        // Also avoid these names followed by an extension, for example, NUL.tx7.
        // Windows NT: CLOCK$ is also a reserved device name.
        ReservedFileNamesRegex.IsMatch(fileName);

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$", RegexOptions.None, "en-150")]
    private static partial Regex BuildReservedFileNameRegex();
#else
    private static Regex BuildReservedFileNameRegex() => new(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$");
#endif
}
