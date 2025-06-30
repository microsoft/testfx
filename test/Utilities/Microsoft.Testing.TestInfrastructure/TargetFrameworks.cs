// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class TargetFrameworks
{
    public static string[] Net { get; } =
    [
        "net9.0",
#if !SKIP_INTERMEDIATE_TARGET_FRAMEWORKS
        "net8.0",
        "net7.0",
        "net6.0",
#endif
    ];

    public static IEnumerable<object[]> NetForDynamicData { get; } =
        Net.Select(tfm => new object[] { tfm });

    public static string NetCurrent { get; } = Net[0];

    public static string[] NetFramework { get; } = ["net462"];

    public static string[] All { get; }
        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? [.. Net, .. NetFramework]
            : Net;

    public static IEnumerable<object[]> AllForDynamicData { get; } =
        All.Select(tfm => new object[] { tfm });

    public static string ToMSBuildTargetFrameworks(this string[] targetFrameworksEntries)
        => string.Join(';', targetFrameworksEntries);
}
