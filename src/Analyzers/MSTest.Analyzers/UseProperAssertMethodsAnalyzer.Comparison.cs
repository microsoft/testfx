// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private enum ComparisonCheckStatus
    {
        Unknown,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    private static ComparisonCheckStatus RecognizeComparisonCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? iComparableOfTSymbol,
        out SyntaxNode? leftExpression,
        out SyntaxNode? rightExpression)
    {
        if (operation is IBinaryOperation binaryOperation &&
            !IsExcludedOperator(binaryOperation.OperatorMethod, objectTypeSymbol))
        {
            leftExpression = binaryOperation.LeftOperand.Syntax;
            rightExpression = binaryOperation.RightOperand.Syntax;

            return CanUseTypesWithComparableAsserts(iComparableOfTSymbol, binaryOperation.LeftOperand.Type, binaryOperation.RightOperand.Type)
                ? binaryOperation.OperatorKind switch
                {
                    BinaryOperatorKind.GreaterThan => ComparisonCheckStatus.GreaterThan,
                    BinaryOperatorKind.GreaterThanOrEqual => ComparisonCheckStatus.GreaterThanOrEqual,
                    BinaryOperatorKind.LessThan => ComparisonCheckStatus.LessThan,
                    BinaryOperatorKind.LessThanOrEqual => ComparisonCheckStatus.LessThanOrEqual,
                    _ => ComparisonCheckStatus.Unknown,
                }
                : ComparisonCheckStatus.Unknown;
        }

        leftExpression = null;
        rightExpression = null;
        return ComparisonCheckStatus.Unknown;
    }

    private static bool CanUseTypesWithComparableAsserts(INamedTypeSymbol? iComparableOfTSymbol, ITypeSymbol? leftType, ITypeSymbol? rightType)
        => iComparableOfTSymbol is not null
            && leftType is not null
            && rightType is not null
            && SymbolEqualityComparer.Default.Equals(leftType, rightType)
            && ImplementsIComparableOfSelf(iComparableOfTSymbol, leftType);

    private static bool ImplementsIComparableOfSelf(INamedTypeSymbol iComparableOfTSymbol, ITypeSymbol type)
    {
        INamedTypeSymbol comparableOfSelf = iComparableOfTSymbol.Construct(type);
        return type.AllInterfaces.Any(interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType, comparableOfSelf));
    }
}
