// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private enum NullCheckStatus
    {
        Unknown,
        IsNull,
        IsNotNull,
    }

    private static bool IsIsNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            typeOfExpressionUnderTest = isPatternOperation.Value.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static bool IsIsNotNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            typeOfExpressionUnderTest = isPatternOperation.Value.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static bool IsEqualsNullBinaryOperator(IOperation operation, INamedTypeSymbol objectTypeSymbol, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } binaryOperation &&
            !IsExcludedOperator(binaryOperation.OperatorMethod, objectTypeSymbol))
        {
            if (binaryOperation.RightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
            {
                expressionUnderTest = binaryOperation.LeftOperand.Syntax;
                typeOfExpressionUnderTest = binaryOperation.LeftOperand.WalkDownConversion().Type;
                return true;
            }

            if (binaryOperation.LeftOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
            {
                expressionUnderTest = binaryOperation.RightOperand.Syntax;
                typeOfExpressionUnderTest = binaryOperation.RightOperand.WalkDownConversion().Type;
                return true;
            }
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static bool IsNotEqualsNullBinaryOperator(IOperation operation, INamedTypeSymbol objectTypeSymbol, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } binaryOperation &&
            !IsExcludedOperator(binaryOperation.OperatorMethod, objectTypeSymbol))
        {
            if (binaryOperation.RightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
            {
                expressionUnderTest = binaryOperation.LeftOperand.Syntax;
                typeOfExpressionUnderTest = binaryOperation.LeftOperand.WalkDownConversion().Type;
                return true;
            }

            if (binaryOperation.LeftOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
            {
                expressionUnderTest = binaryOperation.RightOperand.Syntax;
                typeOfExpressionUnderTest = binaryOperation.RightOperand.WalkDownConversion().Type;
                return true;
            }
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static NullCheckStatus RecognizeNullCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        // Note that expressionUnderTest is guaranteed to be non-null when the method returns a value other than NullCheckStatus.Unknown.
        // Given the current nullability attributes, there is no way to express this.
        out SyntaxNode? expressionUnderTest,
        out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (IsIsNullPattern(operation, out expressionUnderTest, out typeOfExpressionUnderTest) ||
            IsEqualsNullBinaryOperator(operation, objectTypeSymbol, out expressionUnderTest, out typeOfExpressionUnderTest))
        {
            return NullCheckStatus.IsNull;
        }
        else if (IsIsNotNullPattern(operation, out expressionUnderTest, out typeOfExpressionUnderTest) ||
            IsNotEqualsNullBinaryOperator(operation, objectTypeSymbol, out expressionUnderTest, out typeOfExpressionUnderTest))
        {
            return NullCheckStatus.IsNotNull;
        }

        return NullCheckStatus.Unknown;
    }
}
