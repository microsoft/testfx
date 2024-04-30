// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        SymbolVisibility resultantVisibility = methodSymbol.GetResultantVisibility();
        return canDiscoverInternals
            ? resultantVisibility is SymbolVisibility.Public or SymbolVisibility.Internal
            : resultantVisibility is SymbolVisibility.Public;
    }

    public static bool IsAssemblyInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol assemblyInitializeAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, assemblyInitializeAttributeSymbol));

    public static bool IsAssemblyCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol assemblyCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, assemblyCleanupAttributeSymbol));

    public static bool IsClassInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol classInitializeAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classInitializeAttributeSymbol));

    public static bool IsClassCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol classCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classCleanupAttributeSymbol));

    public static bool IsTestInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol testInitializeAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testInitializeAttributeSymbol));

    public static bool IsTestCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol testCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testCleanupAttributeSymbol));
}
