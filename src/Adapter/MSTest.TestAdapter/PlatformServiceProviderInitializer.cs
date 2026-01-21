// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Services;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Helper class to ensure PlatformServiceProvider is properly initialized with all required services.
/// </summary>
internal static class PlatformServiceProviderInitializer
{
    private static int s_initialized;

    /// <summary>
    /// Ensures that PlatformServiceProvider is initialized with all required services.
    /// This method is thread-safe and idempotent.
    /// </summary>
    internal static void EnsureInitialized()
    {
        if (Interlocked.CompareExchange(ref s_initialized, 1, 0) == 0)
        {
            PlatformServiceProvider.Instance.ManagedNameUtilityService = ManagedNameUtilityService.Instance;
        }
    }
}
