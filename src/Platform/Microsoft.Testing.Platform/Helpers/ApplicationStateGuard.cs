// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* This is the canonical ApplicationStateGuard implementation.
 * Simplified copies live in other projects (Adapter, Analyzers, TestFramework); keep them in sync when changing behavior here. */

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[Embedded]
internal static class ApplicationStateGuard
{
    // These are internal invariant-violation diagnostics for "impossible" states; they are intentionally NOT
    // localized (matching e.g. System.Diagnostics.UnreachableException) so that this foundational helper has no
    // dependency on PlatformResources and can be shared as source (e.g. with dotnet/sdk's terminal reporter).
    private const string UnexpectedStateErrorMessage = "Unexpected state in file '{0}' at line '{1}'";
    private const string UnreachableLocationErrorMessage = "This program location is thought to be unreachable. File='{0}' Line={1}";

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
                UnexpectedStateErrorMessage,
                path, line));
        }
    }

    public static UnreachableException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new(string.Format(
            CultureInfo.InvariantCulture,
            UnreachableLocationErrorMessage,
            path, line));
}
