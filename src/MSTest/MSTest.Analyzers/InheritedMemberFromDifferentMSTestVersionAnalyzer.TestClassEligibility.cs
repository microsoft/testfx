// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class InheritedMemberFromDifferentMSTestVersionAnalyzer
{
    private static bool IsDiscoverableTestClassVisibility(INamedTypeSymbol classSymbol, bool canDiscoverInternals)
    {
        // Mirror TypeValidator.TypeHasValidAccessibility: the class and every containing type must be public, or (when
        // the assembly opts into DiscoverInternals) public or internal. Nested protected/protected-internal/
        // private-protected/private types are never discovered, so GetResultantVisibility (which collapses protected to
        // public) is not precise enough here.
        for (INamedTypeSymbol? type = classSymbol; type is not null; type = type.ContainingType)
        {
            bool accessible = canDiscoverInternals
                ? type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                : type.DeclaredAccessibility == Accessibility.Public;
            if (!accessible)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNestedInGenericType(INamedTypeSymbol classSymbol)
    {
        for (INamedTypeSymbol? container = classSymbol.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    private static IAssemblySymbol? GetFrameworkAssembly(
        INamedTypeSymbol classSymbol,
        HashSet<IAssemblySymbol> globallyVisibleAssemblies,
        bool hasGloballyVisibleFramework)
    {
        IAssemblySymbol? fallback = null;
        foreach (IAssemblySymbol assembly in classSymbol.GetAttributes()
            .Select(attribute => GetCanonicalMSTestAttribute(attribute.AttributeClass))
            .OfType<INamedTypeSymbol>()
            .Where(canonicalAttribute => string.Equals(canonicalAttribute.Name, TestClassAttributeName, StringComparison.Ordinal))
            .Select(canonicalAttribute => canonicalAttribute.ContainingAssembly))
        {
            if (globallyVisibleAssemblies.Contains(assembly))
            {
                return assembly;
            }

            fallback ??= assembly;
        }

        return fallback is null || hasGloballyVisibleFramework ? null : fallback;
    }
}
