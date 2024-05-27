// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Internal.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public static class TargetFrameworks
{
    public static TestArgumentsEntry<string>[] Net { get; } =
    [
        new("net8.0", "net8.0"),
        new("net7.0", "net7.0"),
        new("net6.0", "net6.0")
    ];

    public static TestArgumentsEntry<string> NetCurrent { get; } = Net[0];

    public static TestArgumentsEntry<string>[] NetFramework { get; } = [new("net462", "net462")];

    public static TestArgumentsEntry<string>[] All { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Net.Concat(NetFramework).ToArray()
        : Net;

    public static string ToMSBuildTargetFrameworks(this TestArgumentsEntry<string>[] targetFrameworksEntries) => string.Join(";", targetFrameworksEntries.Select(x => x.Arguments));
}
