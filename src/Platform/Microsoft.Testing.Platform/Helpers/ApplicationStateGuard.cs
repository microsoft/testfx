// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !PLATFORM_MSBUILD
using Microsoft.Testing.Platform.Resources;
#endif

namespace Microsoft.Testing.Platform.Helpers;

internal static class ApplicationStateGuard
{
    public static void Ensure([DoesNotReturnIf(false)] bool condition, string errorMessage)
    {
        if (!condition)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    public static void Ensure([DoesNotReturnIf(false)] bool condition, [CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
#if PLATFORM_MSBUILD
                "Unexpected state in file '{0}' at line '{1}'",
#else
                PlatformResources.UnexpectedStateErrorMessage,
#endif
                path, line));
        }
    }

    public static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new(string.Format(
            CultureInfo.InvariantCulture,
#if PLATFORM_MSBUILD
            "This program location is thought to be unreachable. File='{0}' Line={1}",
#else
            PlatformResources.UnreachableLocationErrorMessage,
#endif
            path, line));
}
