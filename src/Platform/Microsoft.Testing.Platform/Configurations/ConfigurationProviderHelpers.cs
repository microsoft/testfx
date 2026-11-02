// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

internal static class ConfigurationProviderHelpers
{
    public static IEnumerable<string> GetChildKeys(IEnumerable<string> keys, string? parentPath)
    {
        string prefix = parentPath is null
            ? string.Empty
            : parentPath + PlatformConfigurationConstants.KeyDelimiter;
        HashSet<string> childKeys = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (string key in keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string remainder = key.Substring(prefix.Length);
            if (remainder.Length == 0)
            {
                continue;
            }

            int delimiterIndex = remainder.IndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal);
            childKeys.Add(delimiterIndex < 0 ? remainder : remainder.Substring(0, delimiterIndex));
        }

        return childKeys;
    }
}
