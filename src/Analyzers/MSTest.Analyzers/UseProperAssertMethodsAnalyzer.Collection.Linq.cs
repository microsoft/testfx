// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private static CollectionCheckStatus RecognizeCollectionMethodCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? enumerableTypeSymbol,
        INamedTypeSymbol? iDictionaryTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? itemExpression,
        out SyntaxNode? comparerExpression)
    {
        if (operation is IInvocationOperation invocation)
        {
            string methodName = invocation.TargetMethod.Name;

            // Check for Collection.Contains(item)
            // We need to also ensure that invocation.TargetMethod.OriginalDefinition.Parameters[0] matches the type of the ienumerable returned by invocation.TargetMethod.ContainingType.OriginalDefinition.AllInterfaces
            if (methodName == "Contains" &&
                invocation.Arguments.Length == 1 &&
                invocation.TargetMethod.OriginalDefinition.Parameters.Length == 1 &&
                invocation.TargetMethod.OriginalDefinition.Parameters[0].Type is { } containsParameterType)
            {
                if (IsBCLCollectionType(invocation.TargetMethod.ContainingType, objectTypeSymbol))
                {
                    ITypeSymbol? enumerableElementType = invocation.TargetMethod.ContainingType.OriginalDefinition.AllInterfaces.FirstOrDefault(
                        i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)?.TypeArguments[0];

                    if (enumerableElementType is null && IsNonGenericDictionaryContains(invocation.TargetMethod, iDictionaryTypeSymbol))
                    {
                        // Non-generic 'System.Collections.IDictionary.Contains(object key)' checks for a matching *key*,
                        // whereas 'Assert.Contains' enumerates the dictionary (yielding 'DictionaryEntry' items).
                        // These have different semantics, so suggesting 'Assert.Contains' here would change behavior.
                        collectionExpression = null;
                        itemExpression = null;
                        comparerExpression = null;
                        return CollectionCheckStatus.Unknown;
                    }

                    if (enumerableElementType is null || enumerableElementType.Equals(containsParameterType, SymbolEqualityComparer.Default))
                    {
                        // If enumerableElementType is null, we expect that this is a non-generic IEnumerable. So we simply report the diagnostic.
                        // If it's not null, ensure that the "T" of "IEnumerable<T>" matches the type of the Contains method.
                        // The type comparison here is not for "substituted" types.
                        // So, when analyzing KeyedCollection<TKey, TItem>.Contains(TKey), we will have:
                        // 1. containsParameterType as the symbol referring to "TKey".
                        // 2. enumerableElementType as the symbol referring to "TItem".
                        // So, even if we are dealing with KeyedCollection<string, string>, the types won't match, and we won't produce a diagnostic.
                        collectionExpression = invocation.Instance?.Syntax;
                        itemExpression = invocation.Arguments[0].Value.Syntax;
                        comparerExpression = null;
                        return CollectionCheckStatus.Contains;
                    }
                }
            }

            // Handle LINQ Enumerable.Contains<TSource>(this IEnumerable<TSource>, TSource)
            // In the Roslyn operation model, LINQ extension calls appear with ContainingType == Enumerable
            // and Arguments includes the 'this' parameter, so Arguments.Length == 2.
            if (methodName == "Contains" &&
                invocation.Arguments.Length == 2 &&
                enumerableTypeSymbol is not null &&
                SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
            {
                collectionExpression = invocation.Arguments[0].Value.Syntax;
                itemExpression = invocation.Arguments[1].Value.Syntax;
                comparerExpression = null;
                return CollectionCheckStatus.Contains;
            }

            // Handle LINQ Enumerable.Contains<TSource>(this IEnumerable<TSource>, TSource, IEqualityComparer<TSource>)
            // In the Roslyn operation model, Arguments includes the 'this' parameter, so Arguments.Length == 3.
            if (methodName == "Contains" &&
                invocation.Arguments.Length == 3 &&
                enumerableTypeSymbol is not null &&
                SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
            {
                collectionExpression = invocation.Arguments[0].Value.Syntax;
                itemExpression = invocation.Arguments[1].Value.Syntax;
                comparerExpression = invocation.Arguments[2].Value.Syntax;
                return CollectionCheckStatus.ContainsWithComparer;
            }
        }

        collectionExpression = null;
        itemExpression = null;
        comparerExpression = null;
        return CollectionCheckStatus.Unknown;
    }

    private static LinqPredicateCheckStatus RecognizeLinqPredicateCheck(
        IOperation operation,
        INamedTypeSymbol? enumerableTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? predicateExpression,
        out IOperation? countOperation)
    {
        collectionExpression = null;
        predicateExpression = null;
        countOperation = null;

        if (enumerableTypeSymbol is null ||
            operation is not IInvocationOperation invocation)
        {
            return LinqPredicateCheckStatus.Unknown;
        }

        string methodName = invocation.TargetMethod.Name;

        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
        {
            return LinqPredicateCheckStatus.Unknown;
        }

        // Check for Where().Method() patterns
        if (invocation.Arguments.Length == 1)
        {
            if (TryMatchWherePattern(invocation, "Any", enumerableTypeSymbol, out collectionExpression, out predicateExpression))
            {
                return LinqPredicateCheckStatus.WhereAny;
            }

            if (TryMatchWherePattern(invocation, "Count", enumerableTypeSymbol, out collectionExpression, out predicateExpression))
            {
                countOperation = operation;
                return LinqPredicateCheckStatus.WhereCount;
            }
        }

        // Check for direct Method(predicate) patterns
        switch (methodName)
        {
            case "Any":
                if (TryMatchLinqMethod(invocation, "Any", enumerableTypeSymbol, out collectionExpression, out predicateExpression))
                {
                    return LinqPredicateCheckStatus.Any;
                }

                break;

            case "Count":
                if (TryMatchLinqMethod(invocation, "Count", enumerableTypeSymbol, out collectionExpression, out predicateExpression))
                {
                    countOperation = operation;
                    return LinqPredicateCheckStatus.Count;
                }

                break;
        }

        return LinqPredicateCheckStatus.Unknown;
    }

    private static bool TryMatchWherePattern(
        IInvocationOperation invocation,
        string methodName,
        INamedTypeSymbol enumerableTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? predicateExpression)
    {
        if (invocation.TargetMethod.Name == methodName &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol) &&
            invocation.Arguments.Length == 1 &&
            invocation.Arguments[0].Value is IInvocationOperation whereInvocation &&
            whereInvocation.TargetMethod.Name == "Where" &&
            SymbolEqualityComparer.Default.Equals(whereInvocation.TargetMethod.ContainingType, enumerableTypeSymbol) &&
            whereInvocation.Arguments.Length == 2)
        {
            collectionExpression = whereInvocation.Arguments[0].Value.Syntax;
            predicateExpression = whereInvocation.Arguments[1].Value.Syntax;
            return true;
        }

        collectionExpression = null;
        predicateExpression = null;
        return false;
    }

    private static bool TryMatchLinqMethod(
        IInvocationOperation invocation,
        string methodName,
        INamedTypeSymbol enumerableTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? predicateExpression)
    {
        if (invocation.TargetMethod.Name == methodName &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
        {
            // Extension method with predicate: Method(collection, predicate)
            if (invocation.Arguments.Length == 2)
            {
                collectionExpression = invocation.Arguments[0].Value.Syntax;
                predicateExpression = invocation.Arguments[1].Value.Syntax;
                return true;
            }

            // Instance method or extension without predicate: Method(collection)
            else if (invocation.Arguments.Length == 1)
            {
                collectionExpression = invocation.Instance?.Syntax ?? invocation.Arguments[0].Value.Syntax;
                predicateExpression = null;
                return true;
            }
        }

        collectionExpression = null;
        predicateExpression = null;
        return false;
    }

    /// <summary>
    /// Sets <paramref name="collectionExpression"/> to the collection syntax node if the operation is a LINQ <c>Count()</c> call with no predicate,
    /// and returns <see langword="true"/>; otherwise sets it to <see langword="null"/> and returns <see langword="false"/>.
    /// </summary>
    private static bool TryGetLinqCountNoPredicate(IOperation operation, INamedTypeSymbol? enumerableTypeSymbol, [NotNullWhen(true)] out SyntaxNode? collectionExpression)
    {
        // LINQ Count() with no predicate is Enumerable.Count<TSource>(this IEnumerable<TSource>)
        // In the Roslyn operation model, extension calls include 'this' in Arguments, so Arguments.Length == 1.
        if (enumerableTypeSymbol is not null &&
            operation is IInvocationOperation invocation &&
            invocation.TargetMethod.Name == "Count" &&
            invocation.Arguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
        {
            collectionExpression = invocation.Arguments[0].Value.Syntax;
            return true;
        }

        collectionExpression = null;
        return false;
    }

    /// <summary>
    /// Sets <paramref name="collectionExpression"/> to the collection syntax node if the operation is a LINQ <c>Any()</c> call with no predicate,
    /// and returns <see langword="true"/>; otherwise sets it to <see langword="null"/> and returns <see langword="false"/>.
    /// </summary>
    private static bool TryGetLinqAnyNoPredicate(IOperation operation, INamedTypeSymbol? enumerableTypeSymbol, [NotNullWhen(true)] out SyntaxNode? collectionExpression)
    {
        // LINQ Any() with no predicate is Enumerable.Any<TSource>(this IEnumerable<TSource>)
        // In the Roslyn operation model, extension calls include 'this' in Arguments, so Arguments.Length == 1.
        if (enumerableTypeSymbol is not null &&
            operation is IInvocationOperation invocation &&
            invocation.TargetMethod.Name == "Any" &&
            invocation.Arguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, enumerableTypeSymbol))
        {
            collectionExpression = invocation.Arguments[0].Value.Syntax;
            return true;
        }

        collectionExpression = null;
        return false;
    }
}
