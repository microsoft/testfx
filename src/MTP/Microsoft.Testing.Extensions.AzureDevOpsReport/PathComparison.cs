// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class PathComparison
{
    public static StringComparison Comparison { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
