// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
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
}
