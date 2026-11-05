// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private enum CollectionCheckStatus
    {
        Unknown,
        Contains,
        ContainsWithComparer,
    }

    private enum CountCheckStatus
    {
        Unknown,
        IsEmpty,
        HasCount,
    }

    private enum LinqPredicateCheckStatus
    {
        Unknown,
        Any,
        Count,
        WhereAny,
        WhereCount,
    }

    private static bool IsBCLCollectionType(ITypeSymbol type, INamedTypeSymbol objectTypeSymbol)
        // Check if the type implements IEnumerable (but is not string)
        => type.SpecialType != SpecialType.System_String && type.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_IEnumerable) &&
            IsBCLSymbol(type, objectTypeSymbol);

    /// <summary>
    /// Returns <see langword="true"/> when the invoked <c>Contains</c> method is the non-generic
    /// <see cref="System.Collections.IDictionary.Contains(object)"/> (or an implementation of it).
    /// That method checks for a matching <em>key</em>, whereas <c>Assert.Contains</c> enumerates the
    /// dictionary (yielding <see cref="System.Collections.DictionaryEntry"/> items), so the two are not
    /// equivalent and the code fix would silently change behavior.
    /// </summary>
    private static bool IsNonGenericDictionaryContains(IMethodSymbol containsMethod, INamedTypeSymbol? iDictionaryTypeSymbol)
    {
        if (iDictionaryTypeSymbol is null)
        {
            return false;
        }

        INamedTypeSymbol containingType = containsMethod.ContainingType;

        // Direct call through the interface, e.g. 'IDictionary dict; dict.Contains(key)'.
        if (SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, iDictionaryTypeSymbol))
        {
            return true;
        }

        // Call through a concrete type that implements IDictionary (e.g. Hashtable), where the invoked
        // 'Contains' is the implementation of 'IDictionary.Contains'.
        if (!containingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDictionaryTypeSymbol)))
        {
            return false;
        }

        foreach (IMethodSymbol dictionaryContains in iDictionaryTypeSymbol.GetMembers("Contains").OfType<IMethodSymbol>())
        {
            if (containingType.FindImplementationForInterfaceMember(dictionaryContains) is IMethodSymbol implementation &&
                SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition, containsMethod.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is one of <see cref="System.Span{T}"/>,
    /// <see cref="System.ReadOnlySpan{T}"/>, <see cref="System.Memory{T}"/> or <see cref="System.ReadOnlyMemory{T}"/>.
    /// These types expose <c>Length</c> and have <c>Assert.HasCount</c> overloads, but cannot satisfy
    /// the <see cref="System.Collections.Generic.IEnumerable{T}"/>-based collection assertions.
    /// </summary>
    private static bool IsSpanOrMemoryType(ITypeSymbol type)
        => type.OriginalDefinition is INamedTypeSymbol { Arity: 1, Name: "Span" or "ReadOnlySpan" or "Memory" or "ReadOnlyMemory", ContainingNamespace: { Name: "System" } containingNamespace } &&
            containingNamespace.ContainingNamespace?.IsGlobalNamespace == true;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="assertTypeSymbol"/> exposes a <c>HasCount</c>
    /// overload whose collection parameter matches the given span/memory <paramref name="spanOrMemoryType"/>.
    /// The span/memory <c>HasCount</c> overloads are compiled only for .NET (not .NET Framework), so this
    /// check prevents the analyzer from suggesting a code fix that would not compile for the targeted framework.
    /// </summary>
    private static bool AssertHasMatchingSpanOrMemoryHasCountOverload(INamedTypeSymbol assertTypeSymbol, ITypeSymbol spanOrMemoryType)
    {
        ITypeSymbol spanOrMemoryDefinition = spanOrMemoryType.OriginalDefinition;

        // Public HasCount span/memory overloads are HasCount<T>(int expected, ReadOnlySpan<T> collection, ...),
        // so the collection is the second parameter (ordinal 1).
        return assertTypeSymbol.GetMembers("HasCount")
            .OfType<IMethodSymbol>()
            .Where(method => method.Parameters.Length >= 2)
            .Any(method => SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type.OriginalDefinition, spanOrMemoryDefinition));
    }
}
