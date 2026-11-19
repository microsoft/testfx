// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private static string FormatDateTimeForRunName(DateTimeOffset date) =>

        // We use custom format string to make sure that runs are sorted in the same way on all intl machines.
        // This is both for directory names and for Data Warehouse.
        date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", DateTimeFormatInfo.InvariantInfo);

    private static Guid GuidFromString(string data)
    {
        byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(data));
        return new Guid(hash);
    }

    /// <summary>
    /// Computes the TRX test id and test definition name used for a test result. Results that share the same id
    /// share a single <c>TestDefinition</c>, so their explicitly provided definition names must agree.
    /// </summary>
    internal static (string Id, string TestDefinitionName, bool IsExplicitlyProvided) GetTestDefinitionIdentity(string uid, string displayName, string? explicitTestDefinitionName)
    {
        // If already a guid (it's the case for at least MSTest), use that guid directly.
        // Otherwise, convert the string to a guid.
        if (!Guid.TryParse(uid, out Guid guid))
        {
            guid = GuidFromString(uid);
        }

        // NOTE: In VSTest, MSTestDiscoverer.TmiTestId property is preferred if present.
        return explicitTestDefinitionName is { } explicitName
            ? (guid.ToString(), RemoveInvalidXmlChar(explicitName), true)
            : (guid.ToString(), RemoveInvalidXmlChar(displayName), false);
    }

    internal static Guid CreateMergeRunId(IReadOnlyList<string> inputPaths)
        => GuidFromString(string.Join("\0", inputPaths.Select(Path.GetFullPath).OrderBy(path => path, StringComparer.Ordinal)));

    internal static Guid CreateMergeRunId(IReadOnlyList<string> inputPaths, IReadOnlyList<string?> executionIds)
        => inputPaths.Count == executionIds.Count
            ? GuidFromString(string.Join(
                "\0",
                inputPaths.Select((path, index) => $"{Path.GetFullPath(path)}\0{executionIds[index]}")
                    .OrderBy(value => value, StringComparer.Ordinal)))
            : throw new ArgumentException("Input paths and execution IDs must have the same count.", nameof(executionIds));

    // From xml spec (http://www.w3.org/TR/xml/#charsets) valid chars:
    // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
    // surrogate pairs represent the [#x10000-#x10FFFF] range and are valid.
    private static string RemoveInvalidXmlChar(string str)
    {
        StringBuilder? builder = null;
        // Invalid UTF-16 code units expand to six-character escape sequences.
        int builderCapacity = GetInvalidXmlCharBuilderCapacity(str.Length);

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

            builder ??= new StringBuilder(str, 0, i, builderCapacity);
            builder.Append(ReplaceInvalidCharacterWithUniCodeEscapeSequence(current));
        }

        return builder?.ToString() ?? str;
    }

    private static bool IsValidXmlChar(char value) =>
        value is '\t' or '\n' or '\r' or (>= '\x20' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD');

    private static int GetInvalidXmlCharBuilderCapacity(int length)
    {
        int extraCapacity = length / 2;
        return length <= int.MaxValue - extraCapacity ? length + extraCapacity : length;
    }

    private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(char x) => $@"\u{(ushort)x:x4}";
}
