// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class ISymbolExtensions
{
    public static ITypeSymbol? GetReferencedMemberOrLocalOrParameter(this ISymbol? symbol) => symbol switch
    {
        IEventSymbol eventSymbol => eventSymbol.Type,

        IFieldSymbol fieldSymbol => fieldSymbol.Type,

        IMethodSymbol methodSymbol => methodSymbol.ReturnType,

        IPropertySymbol propertySymbol => propertySymbol.Type,

        ILocalSymbol localSymbol => localSymbol.Type,

        _ => null,
    };
}
