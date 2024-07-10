// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Extensions
{
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
                    return GetResultantVisibility(symbol.ContainingSymbol);

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
    }
}
