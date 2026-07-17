// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

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

    private static CountCheckStatus RecognizeCountCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? enumerableTypeSymbol,
        out SyntaxNode? collectionExpression)
    {
        collectionExpression = null;

        // Check for collection.Count > 0 or collection.Length > 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan, LeftOperand: IPropertyReferenceOperation propertyRef, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef, objectTypeSymbol) is { } expression)
        {
            collectionExpression = expression;
            return CountCheckStatus.HasCount;
        }

        // Check for enumerable.Count() > 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan, LeftOperand: IInvocationOperation linqCountInv1, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetLinqCountNoPredicate(linqCountInv1, enumerableTypeSymbol, out SyntaxNode? linqExpr1))
        {
            collectionExpression = linqExpr1;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 < collection.Count or 0 < collection.Length
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.LessThan, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef2 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef2, objectTypeSymbol) is { } expression2)
        {
            collectionExpression = expression2;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 < enumerable.Count()
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.LessThan, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IInvocationOperation linqCountInv2 } &&
            TryGetLinqCountNoPredicate(linqCountInv2, enumerableTypeSymbol, out SyntaxNode? linqExpr2))
        {
            collectionExpression = linqExpr2;
            return CountCheckStatus.HasCount;
        }

        // Check for collection.Count != 0 or collection.Length != 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: IPropertyReferenceOperation propertyRef3, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef3, objectTypeSymbol) is { } expression3)
        {
            collectionExpression = expression3;
            return CountCheckStatus.HasCount;
        }

        // Check for enumerable.Count() != 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: IInvocationOperation linqCountInv3, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetLinqCountNoPredicate(linqCountInv3, enumerableTypeSymbol, out SyntaxNode? linqExpr3))
        {
            collectionExpression = linqExpr3;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 != collection.Count or 0 != collection.Length (reverse order)
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef4 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef4, objectTypeSymbol) is { } expression4)
        {
            collectionExpression = expression4;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 != enumerable.Count()
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IInvocationOperation linqCountInv4 } &&
            TryGetLinqCountNoPredicate(linqCountInv4, enumerableTypeSymbol, out SyntaxNode? linqExpr4))
        {
            collectionExpression = linqExpr4;
            return CountCheckStatus.HasCount;
        }

        // Check for collection.Count == 0 or collection.Length == 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, LeftOperand: IPropertyReferenceOperation propertyRef5, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef5, objectTypeSymbol) is { } expression5)
        {
            collectionExpression = expression5;
            return CountCheckStatus.IsEmpty;
        }

        // Check for enumerable.Count() == 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, LeftOperand: IInvocationOperation linqCountInv5, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetLinqCountNoPredicate(linqCountInv5, enumerableTypeSymbol, out SyntaxNode? linqExpr5))
        {
            collectionExpression = linqExpr5;
            return CountCheckStatus.IsEmpty;
        }

        // Check for 0 == collection.Count or 0 == collection.Length (reverse order)
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef6 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef6, objectTypeSymbol) is { } expression6)
        {
            collectionExpression = expression6;
            return CountCheckStatus.IsEmpty;
        }

        // Check for 0 == enumerable.Count()
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IInvocationOperation linqCountInv6 } &&
            TryGetLinqCountNoPredicate(linqCountInv6, enumerableTypeSymbol, out SyntaxNode? linqExpr6))
        {
            collectionExpression = linqExpr6;
            return CountCheckStatus.IsEmpty;
        }

        // Check for enumerable.Any() (no predicate) - direct invocation.
        // NOTE: We return HasCount here because the caller (AnalyzeIsTrueOrIsFalseInvocation)
        // maps HasCount → IsNotEmpty (for IsTrue) and HasCount → IsEmpty (for IsFalse),
        // which is the correct behavior for Any(). This method is NOT called from the
        // AreEqual/AreNotEqual path (which uses the two-argument overload), so the HasCount
        // value won't be misinterpreted as suggesting Assert.HasCount for Any().
        if (TryGetLinqAnyNoPredicate(operation, enumerableTypeSymbol, out SyntaxNode? linqAnyExpr))
        {
            collectionExpression = linqAnyExpr;
            return CountCheckStatus.HasCount;
        }

        return CountCheckStatus.Unknown;
    }

    private static CountCheckStatus RecognizeCountCheck(
        IOperation expectedArgument,
        IOperation actualArgument,
        INamedTypeSymbol assertTypeSymbol,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? enumerableTypeSymbol,
        out SyntaxNode? nodeToBeReplaced1,
        out SyntaxNode? replacement1,
        out SyntaxNode? nodeToBeReplaced2,
        out SyntaxNode? replacement2)
    {
        // Check if actualArgument is a Span/ReadOnlySpan/Memory/ReadOnlyMemory '.Length'.
        // These types have HasCount overloads but no IsEmpty/IsNotEmpty overloads, so we always
        // suggest Assert.HasCount (even when the expected value is 0) and never Assert.IsEmpty.
        // We only do this when the referenced MSTest framework actually exposes a matching span/memory
        // HasCount overload (these are guarded out on .NET Framework targets); otherwise the code fix
        // would generate a call that does not compile.
        if (actualArgument is IPropertyReferenceOperation { Property.Name: "Length" } spanLengthRef &&
            spanLengthRef.Instance?.Type is { } spanInstanceType &&
            IsSpanOrMemoryType(spanInstanceType) &&
            IsBCLSymbol(spanInstanceType, objectTypeSymbol) &&
            AssertHasMatchingSpanOrMemoryHasCountOverload(assertTypeSymbol, spanInstanceType))
        {
            // Assert.HasCount takes int, so skip if expectedCount is not an int (e.g. int?, long, uint, decimal).
            if (expectedArgument.Type?.SpecialType == SpecialType.System_Int32)
            {
                nodeToBeReplaced1 = actualArgument.Syntax; // span.Length
                replacement1 = spanLengthRef.Instance.Syntax; // span
                nodeToBeReplaced2 = null;
                replacement2 = null;
                return CountCheckStatus.HasCount;
            }

            nodeToBeReplaced1 = null;
            replacement1 = null;
            nodeToBeReplaced2 = null;
            replacement2 = null;
            return CountCheckStatus.Unknown;
        }

        // Check if actualArgument is a count/length property
        if (actualArgument is IPropertyReferenceOperation propertyRef &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef, objectTypeSymbol) is { } expression)
        {
            bool isEmpty = expectedArgument.ConstantValue.HasValue &&
                expectedArgument.ConstantValue.Value is int expectedValue &&
                expectedValue == 0;

            if (isEmpty)
            {
                // We have Assert.AreEqual(0, collection.Count/Length)
                // We want Assert.IsEmpty(collection)
                // So, only a single replacement is needed. We replace collection.Count with collection.
                nodeToBeReplaced1 = actualArgument.Syntax; // collection.Count
                replacement1 = expression; // collection
                nodeToBeReplaced2 = expectedArgument.Syntax; // 0
                replacement2 = null;
                return CountCheckStatus.IsEmpty;
            }
            else
            {
                // We have Assert.AreEqual(expectedCount, collection.Count/Length)
                // We want Assert.HasCount(expectedCount, collection)
                // Assert.HasCount takes int, so skip if expectedCount is not an int (e.g. int?, long, uint, decimal).
                if (expectedArgument.Type?.SpecialType != SpecialType.System_Int32)
                {
                    nodeToBeReplaced1 = null;
                    replacement1 = null;
                    nodeToBeReplaced2 = null;
                    replacement2 = null;
                    return CountCheckStatus.Unknown;
                }

                // So, only a single replacement is needed. We replace collection.Count with collection.
                nodeToBeReplaced1 = actualArgument.Syntax; // collection.Count
                replacement1 = expression; // collection
                nodeToBeReplaced2 = null;
                replacement2 = null;
                return CountCheckStatus.HasCount;
            }
        }

        // Check if actualArgument is a LINQ Count() call with no predicate
        if (actualArgument is IInvocationOperation linqCountInvocation &&
            TryGetLinqCountNoPredicate(linqCountInvocation, enumerableTypeSymbol, out SyntaxNode? linqCollection))
        {
            bool isEmpty = expectedArgument.ConstantValue.HasValue &&
                expectedArgument.ConstantValue.Value is int expectedLinqValue &&
                expectedLinqValue == 0;

            if (isEmpty)
            {
                // We have Assert.AreEqual(0, enumerable.Count())
                // We want Assert.IsEmpty(enumerable)
                nodeToBeReplaced1 = actualArgument.Syntax; // enumerable.Count()
                replacement1 = linqCollection; // enumerable
                nodeToBeReplaced2 = expectedArgument.Syntax; // 0
                replacement2 = null;
                return CountCheckStatus.IsEmpty;
            }
            else
            {
                // We have Assert.AreEqual(expectedCount, enumerable.Count())
                // We want Assert.HasCount(expectedCount, enumerable)
                // Assert.HasCount takes int, so skip if expectedCount is not an int (e.g. int?, long, uint, decimal).
                if (expectedArgument.Type?.SpecialType != SpecialType.System_Int32)
                {
                    nodeToBeReplaced1 = null;
                    replacement1 = null;
                    nodeToBeReplaced2 = null;
                    replacement2 = null;
                    return CountCheckStatus.Unknown;
                }

                nodeToBeReplaced1 = actualArgument.Syntax; // enumerable.Count()
                replacement1 = linqCollection; // enumerable
                nodeToBeReplaced2 = null;
                replacement2 = null;
                return CountCheckStatus.HasCount;
            }
        }

        nodeToBeReplaced1 = null;
        replacement1 = null;
        nodeToBeReplaced2 = null;
        replacement2 = null;
        return CountCheckStatus.Unknown;
    }

    private static SyntaxNode? TryGetCollectionExpressionIfBCLCollectionLengthOrCount(IPropertyReferenceOperation propertyReference, INamedTypeSymbol objectTypeSymbol)
        => propertyReference.Property.Name is "Count" or "Length" &&
            propertyReference.Instance?.Type is not null &&
            (propertyReference.Instance.Type.TypeKind == TypeKind.Array || IsBCLCollectionType(propertyReference.Property.ContainingType, objectTypeSymbol))
                ? propertyReference.Instance.Syntax
                : null;

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
