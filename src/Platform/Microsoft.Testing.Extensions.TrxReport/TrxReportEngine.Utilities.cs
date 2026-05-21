// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private static readonly Regex ReservedFileNamesRegex = BuildReservedFileNameRegex();

    private static readonly HashSet<char> InvalidFileNameChars =
    [
        '"',
        '<',
        '>',
        '|',
        '\0',
        (char)1,
        (char)2,
        (char)3,
        (char)4,
        (char)5,
        (char)6,
        (char)7,
        (char)8,
        (char)9,
        (char)10,
        (char)11,
        (char)12,
        (char)13,
        (char)14,
        (char)15,
        (char)16,
        (char)17,
        (char)18,
        (char)19,
        (char)20,
        (char)21,
        (char)22,
        (char)23,
        (char)24,
        (char)25,
        (char)26,
        (char)27,
        (char)28,
        (char)29,
        (char)30,
        (char)31,
        ':',
        '*',
        '?',
        '\\',
        '/',
        '@',
        '(',
        ')',
        '^',
        ' '
    ];

    private static string FormatDateTimeForRunName(DateTimeOffset date) =>

        // We use custom format string to make sure that runs are sorted in the same way on all intl machines.
        // This is both for directory names and for Data Warehouse.
        date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", DateTimeFormatInfo.InvariantInfo);

    private static string ReplaceInvalidFileNameChars(string fileName)
    {
        // Replace bad chars by this.
        char replacementChar = '_';
        char[] result = new char[fileName.Length];

        // Replace each invalid char with replacement char.
        for (int i = 0; i < fileName.Length; ++i)
        {
            result[i] = InvalidFileNameChars.Contains(fileName[i]) ? replacementChar : fileName[i];
        }

        // We trim spaces in the end because CreateFile/Dir trim those.
        string replaced = new string(result).TrimEnd();
        ArgumentGuard.Ensure(replaced.Length > 0, nameof(fileName), $"File name {fileName} is empty after removing invalid characters.");

        if (IsReservedFileName(replaced))
        {
            replaced = replacementChar + replaced;  // Cannot add to the end because it can have extensions.
        }

        return replaced;
    }

    private static bool IsReservedFileName(string fileName) =>

        // CreateFile:
        // The following reserved device names cannot be used as the name of a file:
        // CON, PRN, AUX, NUL, COM1, COM2, COM3, COM4, COM5, COM6, COM7, COM8, COM9,
        // LPT1, LPT2, LPT3, LPT4, LPT5, LPT6, LPT7, LPT8, and LPT9.
        // Also avoid these names followed by an extension, for example, NUL.tx7.
        // Windows NT: CLOCK$ is also a reserved device name.
        ReservedFileNamesRegex.IsMatch(fileName);

    private static Guid GuidFromString(string data)
    {
        byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(data));
        return new Guid(hash);
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$", RegexOptions.None, "en-150")]
    private static partial Regex BuildReservedFileNameRegex();
#else
    private static Regex BuildReservedFileNameRegex() => new(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$");
#endif

    // From xml spec (http://www.w3.org/TR/xml/#charsets) valid chars:
    // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
    // surrogate pairs represent the [#x10000-#x10FFFF] range and are valid.
    private static string RemoveInvalidXmlChar(string str)
    {
        StringBuilder? builder = null;
        // Invalid UTF-16 code units expand to six-character escape sequences.
        int builderCapacity = str.Length <= int.MaxValue / 2 ? str.Length * 2 : str.Length;

        for (int i = 0; i < str.Length; i++)
        {
            char current = str[i];
            bool isValidChar = IsValidXmlChar(current);
            bool isValidSurrogatePair = char.IsHighSurrogate(current) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]);

            if (isValidChar || isValidSurrogatePair)
            {
                if (builder is not null)
                {
                    builder.Append(current);
                    if (isValidSurrogatePair)
                    {
                        builder.Append(str[++i]);
                    }
                }
                else if (isValidSurrogatePair)
                {
                    i++;
                }

                continue;
            }

            builder ??= new StringBuilder(builderCapacity).Append(str, 0, i);
            builder.Append(ReplaceInvalidCharacterWithUniCodeEscapeSequence(current));
        }

        return builder?.ToString() ?? str;
    }

    private static bool IsValidXmlChar(char value) =>
        value is '\t' or '\n' or '\r' or (>= '\x20' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD');

    private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(char x) => $@"\u{(ushort)x:x4}";
}
