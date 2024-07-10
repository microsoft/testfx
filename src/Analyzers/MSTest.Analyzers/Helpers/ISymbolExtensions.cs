// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
