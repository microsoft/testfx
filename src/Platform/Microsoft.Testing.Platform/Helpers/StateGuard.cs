// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Testing.Platform.Helpers;

internal static class StateGuard
{
    public static void Ensure([DoesNotReturnIf(false)] bool condition, string? errorMessage = null, [CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            errorMessage ??= "This program location is thought to be unreachable.";
            throw new InvalidOperationException($"{errorMessage}\nFile='{path}' Line={line}");
        }
    }
}
