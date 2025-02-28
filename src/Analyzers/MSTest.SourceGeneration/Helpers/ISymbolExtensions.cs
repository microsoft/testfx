// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Analyzers.Utilities;

internal static class ISymbolExtensions
{
    public static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
    {
        // Start by assuming it's visible.
        SymbolVisibility visibility = SymbolVisibility.Public;

        switch (symbol.Kind)
        {
            case SymbolKind.Alias:
                // Aliases are uber private.  They're only visible in the same file that they
                // were declared in.
                return SymbolVisibility.Private;

            case SymbolKind.Parameter:
                // Parameters are only as visible as their containing symbol
                return symbol.ContainingSymbol.GetResultantVisibility();

            case SymbolKind.TypeParameter:
                // Type Parameters are private.
                return SymbolVisibility.Private;
        }

        while (symbol != null && symbol.Kind != SymbolKind.Namespace)
        {
            switch (symbol.DeclaredAccessibility)
            {
                // If we see anything private, then the symbol is private.
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                    return SymbolVisibility.Private;

                // If we see anything internal, then knock it down from public to
                // internal.
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = SymbolVisibility.Internal;
                    break;

                    // For anything else (Public, Protected, ProtectedOrInternal), the
                    // symbol stays at the level we've gotten so far.
            }

            symbol = symbol.ContainingSymbol;
        }

        return visibility;
    }

    public static ITypeSymbol? GetMemberType(this ISymbol? symbol)
        => symbol switch
        {
            IEventSymbol eventSymbol => eventSymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            IMethodSymbol methodSymbol => methodSymbol.ReturnType,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            _ => null,
        };
}
