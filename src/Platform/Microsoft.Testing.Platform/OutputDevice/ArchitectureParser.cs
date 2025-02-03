// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class ArchitectureParser
{
    private static readonly char[] Dash = new char[] { '-' };

    public static string GetShortArchitecture(string runtimeIdentifier)
        => runtimeIdentifier.Contains('-')
            ? runtimeIdentifier.Split(Dash, 2)[1]
            : runtimeIdentifier;
}
