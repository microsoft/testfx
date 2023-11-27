// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.Resources;

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
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnexpectedStateErrorMessage, path, line));
        }
    }

    public static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnreachableLocationErrorMessage, path, line));
}
