// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private enum EqualityCheckStatus
    {
        Unknown,
        Equals,
        NotEquals,
    }

    private static EqualityCheckStatus RecognizeEqualityCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        out SyntaxNode? toBecomeExpected,
        out SyntaxNode? toBecomeActual,
        out ITypeSymbol? leftType,
        out ITypeSymbol? rightType)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation constantPattern1 } isPattern1)
        {
            toBecomeExpected = constantPattern1.Syntax;
            toBecomeActual = isPattern1.Value.Syntax;
            leftType = constantPattern1.WalkDownConversion().Type;
            rightType = isPattern1.Value.WalkDownConversion().Type;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } binaryOperation1 &&
            !IsExcludedOperator(binaryOperation1.OperatorMethod, objectTypeSymbol))
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation1.RightOperand.Syntax;
            toBecomeActual = binaryOperation1.LeftOperand.Syntax;
            leftType = binaryOperation1.RightOperand.WalkDownConversion().Type;
            rightType = binaryOperation1.LeftOperand.WalkDownConversion().Type;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation constantPattern2 } } isPattern2)
        {
            toBecomeExpected = constantPattern2.Syntax;
            toBecomeActual = isPattern2.Value.Syntax;
            leftType = constantPattern2.WalkDownConversion().Type;
            rightType = isPattern2.Value.WalkDownConversion().Type;
            return EqualityCheckStatus.NotEquals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } binaryOperation2 &&
            !IsExcludedOperator(binaryOperation2.OperatorMethod, objectTypeSymbol))
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation2.RightOperand.Syntax;
            toBecomeActual = binaryOperation2.LeftOperand.Syntax;
            leftType = binaryOperation2.RightOperand.WalkDownConversion().Type;
            rightType = binaryOperation2.LeftOperand.WalkDownConversion().Type;
            return EqualityCheckStatus.NotEquals;
        }

        toBecomeExpected = null;
        toBecomeActual = null;
        leftType = null;
        rightType = null;
        return EqualityCheckStatus.Unknown;
    }
}
