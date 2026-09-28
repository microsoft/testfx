// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

internal static class CommandLineOptionParser
{
    internal static string? TryGetOptionValue(IReadOnlyList<string> arguments, string option)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            if (string.Equals(arguments[i], option, StringComparison.Ordinal))
            {
                return i + 1 < arguments.Count ? arguments[i + 1] : null;
            }

            if (TryGetInlineOptionValue(arguments[i], option, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    internal static bool TryGetInlineOptionValue(string argument, string option, out string? value)
    {
        if (argument.Length > option.Length
            && argument.StartsWith(option, StringComparison.Ordinal)
            && argument[option.Length] is '=' or ':')
        {
            value = argument.Substring(option.Length + 1);
            return true;
        }

        value = null;
        return false;
    }
}
