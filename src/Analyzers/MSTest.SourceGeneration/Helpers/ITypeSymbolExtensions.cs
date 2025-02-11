// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Analyzers.Utilities;

internal static class ITypeSymbolExtensions
{
    /// <summary>
    /// Returns the members of the given type, including inherited members.
    /// </summary>
    public static IEnumerable<ImmutableArray<ISymbol>> GetAllMembers(this INamedTypeSymbol symbol)
    {
        INamedTypeSymbol? currentSymbol = symbol;

        while (currentSymbol != null)
        {
            yield return currentSymbol.GetMembers();
            currentSymbol = currentSymbol.BaseType;
        }
    }

    public static IEnumerable<ImmutableArray<ISymbol>> GetAllMembers(this INamedTypeSymbol symbol, string name)
    {
        INamedTypeSymbol? currentSymbol = symbol;

        while (currentSymbol != null)
        {
            yield return currentSymbol.GetMembers(name);
            currentSymbol = currentSymbol.BaseType;
        }
    }

    public static bool Inherits(
        [NotNullWhen(returnValue: true)] this ITypeSymbol? type,
        [NotNullWhen(returnValue: true)] ITypeSymbol? possibleBase)
    {
        if (type == null || possibleBase == null)
        {
            return false;
        }

        switch (possibleBase.TypeKind)
        {
            case TypeKind.Class:
                if (type.TypeKind == TypeKind.Interface)
                {
                    return false;
                }

                return DerivesFrom(type, possibleBase, baseTypesOnly: true);

            case TypeKind.Interface:
                return DerivesFrom(type, possibleBase);

            default:
                return false;
        }
    }

    public static bool DerivesFrom(
        [NotNullWhen(returnValue: true)] this ITypeSymbol? symbol,
        [NotNullWhen(returnValue: true)] ITypeSymbol? candidateBaseType,
        bool baseTypesOnly = false,
        bool checkTypeParameterConstraints = true)
    {
        if (candidateBaseType == null || symbol == null)
        {
            return false;
        }

        if (!baseTypesOnly && candidateBaseType.TypeKind == TypeKind.Interface)
        {
            IEnumerable<ITypeSymbol> allInterfaces = symbol.AllInterfaces.OfType<ITypeSymbol>();
            if (SymbolEqualityComparer.Default.Equals(candidateBaseType.OriginalDefinition, candidateBaseType))
            {
                // Candidate base type is not a constructed generic type, so use original definition for interfaces.
                allInterfaces = allInterfaces.Select(i => i.OriginalDefinition);
            }

            if (allInterfaces.Contains(candidateBaseType, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        if (checkTypeParameterConstraints && symbol.TypeKind == TypeKind.TypeParameter)
        {
            var typeParameterSymbol = (ITypeParameterSymbol)symbol;
            foreach (ITypeSymbol constraintType in typeParameterSymbol.ConstraintTypes)
            {
                if (constraintType.DerivesFrom(candidateBaseType, baseTypesOnly, checkTypeParameterConstraints))
                {
                    return true;
                }
            }
        }

        while (symbol != null)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, candidateBaseType))
            {
                return true;
            }

            symbol = symbol.BaseType;
        }

        return false;
    }
}
