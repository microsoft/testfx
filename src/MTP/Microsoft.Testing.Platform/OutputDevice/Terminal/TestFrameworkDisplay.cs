// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal static class TestFrameworkDisplay
{
    public static string GetDisplayName(string? targetFramework, string? architecture, string? runtimeFramework = null)
    {
        string primary = RoslynString.IsNullOrWhiteSpace(targetFramework)
            ? RoslynString.IsNullOrWhiteSpace(architecture) ? string.Empty : architecture
            : RoslynString.IsNullOrWhiteSpace(architecture)
                ? targetFramework
                : $"{targetFramework}|{architecture}";

        if (RoslynString.IsNullOrWhiteSpace(runtimeFramework))
        {
            return primary;
        }

        if (!RoslynString.IsNullOrWhiteSpace(targetFramework) && IsKnownRuntimeFramework(runtimeFramework))
        {
            int platformSeparator = targetFramework.IndexOf('-');
            string baseTargetFramework = platformSeparator < 0 ? targetFramework : targetFramework[..platformSeparator];
            if (string.Equals(baseTargetFramework, runtimeFramework, StringComparison.OrdinalIgnoreCase))
            {
                return primary;
            }
        }

        string runtime = string.Format(CultureInfo.CurrentCulture, TerminalResources.RuntimeFramework, runtimeFramework);
        return primary.Length == 0 ? runtime : $"{primary}, {runtime}";
    }

    private static bool IsKnownRuntimeFramework(string framework)
    {
        if (framework.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            return Version.TryParse(framework[10..], out _);
        }

        if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string version = framework[3..];
        return Version.TryParse(version, out _)
            || (version.Length is 2 or 3 && version[0] == '4' && int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }
}
