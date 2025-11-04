// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

internal static class DumpWorkaround
{
    /// <summary>
    /// Gets MSBuild global properties to work around crash/hang dump issues on macOS.
    /// </summary>
    /// <returns>
    /// On macOS, returns "-p:UseAppHost=false" to work around createdump not working correctly on apphost.
    /// On other platforms, returns an empty string.
    /// </returns>
    /// <remarks>
    /// Workaround: createdump doesn't work correctly on the apphost on macOS.
    /// But it works correctly on the dotnet process.
    /// So, disable apphost on macOS for now.
    /// Related: https://github.com/dotnet/runtime/issues/119945
    /// </remarks>
    public static string GetGlobalPropertiesWorkaround()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "-p:UseAppHost=false";
        }

        return string.Empty;
    }
}
