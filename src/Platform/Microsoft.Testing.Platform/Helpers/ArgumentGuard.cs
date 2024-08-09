// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ArgumentGuard
{
    public static void Ensure([DoesNotReturnIf(false)] bool condition, string paramName, string errorMessage)
    {
        if (!condition)
        {
            throw new ArgumentException(errorMessage, paramName);
        }
    }
}
