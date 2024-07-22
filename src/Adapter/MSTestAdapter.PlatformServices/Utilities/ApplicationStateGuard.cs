// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter;

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
            throw new InvalidOperationException($"Unexpected state in file '{path}' at line '{line}'");
        }
    }

    public static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new($"This program location is thought to be unreachable. File='{path}' Line={line}");
}
