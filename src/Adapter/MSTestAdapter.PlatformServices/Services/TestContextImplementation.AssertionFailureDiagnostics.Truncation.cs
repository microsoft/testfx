// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    private static string? Truncate(string? value, int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        const string TruncationSuffix = "... <truncated>";
        bool requiresTruncation = value.Length > maximumLength;
        int contentLimit = requiresTruncation
            ? Math.Max(0, maximumLength - TruncationSuffix.Length)
            : maximumLength;
        var builder = new StringBuilder(Math.Min(maximumLength, value.Length + TruncationSuffix.Length));
        int index = 0;
        while (index < value.Length && builder.Length < contentLimit)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    if (builder.Length + 2 > contentLimit)
                    {
                        break;
                    }

                    builder.Append(current);
                    builder.Append(value[index + 1]);
                    index += 2;
                    continue;
                }

                builder.Append('\uFFFD');
                index++;
                continue;
            }

            builder.Append(char.IsLowSurrogate(current) ? '\uFFFD' : current);
            index++;
        }

        if (index < value.Length)
        {
            builder.Append(TruncationSuffix);
        }

        return builder.ToString();
    }
}
#endif
