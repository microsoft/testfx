// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestFrameworkCapabilitiesExtensions
{
    public static bool HasCapability<T>(this ITestFrameworkCapabilities capabilities)
        where T : ICapability
        => capabilities.GetCapability<T>() is not null;

    public static T? GetCapability<T>(this ITestFrameworkCapabilities capabilities)
        where T : ICapability
    {
        foreach (ICapability capability in capabilities.Capabilities)
        {
            if (capability is T implementedCapability)
            {
                return implementedCapability;
            }
        }

        return default;
    }
}
