// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ArgumentGuard
{
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Helper to merge API difference across TFMs")]
    public static void IsNotNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    public static void IsNotNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        IsNotNull(argument, paramName);
        Ensure(argument.Length > 0, paramName!, $"{paramName} should not be empty");
    }

    public static void IsNotNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        IsNotNull(argument, paramName);
        Ensure(!RoslynString.IsNullOrWhiteSpace(argument), paramName!, $"{paramName} should not be empty or whitespace");
    }

    public static void Ensure([DoesNotReturnIf(false)] bool condition, string paramName, string errorMessage)
    {
        if (!condition)
        {
            throw new ArgumentException(errorMessage, paramName);
        }
    }
}
