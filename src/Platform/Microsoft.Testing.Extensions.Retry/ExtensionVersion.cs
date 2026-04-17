// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Policy;

internal static class ExtensionVersion
{
    public static readonly string DefaultSemVer = GetDefaultSemVer();

    private static string GetDefaultSemVer()
    {
        Assembly extensionAssembly = typeof(ExtensionVersion).Assembly;
        return extensionAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? extensionAssembly.GetName().Version?.ToString()
            ?? string.Empty;
    }
}
