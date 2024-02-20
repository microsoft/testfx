// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class IMethodSymbolExtensions
{
    public static bool IsPublicAndHasCorrectResultantVisibility(this IMethodSymbol methodSymbol, bool canDiscoverInternals)
    {
        // Even when we allow discovering internals, MSTest engine only supports the method being declared as public.
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        var resultantVisibility = methodSymbol.GetResultantVisibility();
        return canDiscoverInternals
            ? resultantVisibility is SymbolVisibility.Public or SymbolVisibility.Internal
            : resultantVisibility is SymbolVisibility.Public;
    }
}
