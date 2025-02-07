// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// Provides extension methods for <see cref="ITestFrameworkCapabilities"/>.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestFrameworkCapabilitiesExtensions
{
    /// <summary>
    /// Checks whether the test framework capabilities contain a capability of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the capability.</typeparam>
    /// <param name="capabilities">The test framework capabilities.</param>
    /// <returns><c>true</c> if the framework contains the capability; <c>false</c> otherwise.</returns>
    public static bool HasCapability<T>(this ITestFrameworkCapabilities capabilities)
        where T : ICapability
        => capabilities.GetCapability<T>() is not null;

    /// <summary>
    /// Gets the capability of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the capability.</typeparam>
    /// <param name="capabilities">The test framework capabilities.</param>
    /// <returns>The capability.</returns>
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
