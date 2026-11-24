// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private const string RegexFullTypeName = "System.Text.RegularExpressions.Regex";

    private enum TypeCheckStatus
    {
        Unknown,
        IsType,
        IsNotType,
    }

    /// <summary>
    /// Recognizes <c>x is T</c> and <c>x is not T</c>.
    /// </summary>
    private static TypeCheckStatus RecognizeTypeCheck(Compilation compilation, IOperation operation, [NotNullWhen(true)] out SyntaxNode? valueExpression)
    {
        valueExpression = null;
        IOperation valueOperand;
        ITypeSymbol checkedType;
        TypeCheckStatus status;

        switch (operation)
        {
            case IIsTypeOperation { IsNegated: false } isTypeOperation:
                valueOperand = isTypeOperation.ValueOperand;
                checkedType = isTypeOperation.TypeOperand;
                status = TypeCheckStatus.IsType;
                break;

            case IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: ITypePatternOperation typePattern } } isPatternOperation:
                valueOperand = isPatternOperation.Value;
                checkedType = typePattern.MatchedType;
                status = TypeCheckStatus.IsNotType;
                break;

            default:
                return TypeCheckStatus.Unknown;
        }

        // The suggested methods are generic (T) and take the value as object.
        if (checkedType is IPointerTypeSymbol or IFunctionPointerTypeSymbol or IErrorTypeSymbol ||
            checkedType.IsRefLikeType ||
            checkedType.SpecialType == SpecialType.System_Void ||
            valueOperand.Type is null ||
            !CanUseTypeAsObject(compilation, valueOperand.Type))
        {
            return TypeCheckStatus.Unknown;
        }

        valueExpression = valueOperand.Syntax;
        return status;
    }

    /// <summary>
    /// Recognizes <c>object.ReferenceEquals(a, b)</c> where both operands are reference types.
    /// </summary>
    private static bool RecognizeReferenceEqualsCheck(Compilation compilation, IOperation operation, [NotNullWhen(true)] out SyntaxNode? firstExpression, [NotNullWhen(true)] out SyntaxNode? secondExpression)
    {
        firstExpression = null;
        secondExpression = null;

        if (operation is not IInvocationOperation { TargetMethod: { Name: "ReferenceEquals", IsStatic: true, Parameters.Length: 2 } targetMethod } invocation ||
            targetMethod.ContainingType.SpecialType != SpecialType.System_Object ||
            !TryGetArgumentValueWithoutImplicitConversions(invocation, 0, out IOperation? first) ||
            !TryGetArgumentValueWithoutImplicitConversions(invocation, 1, out IOperation? second))
        {
            return false;
        }

        // Value types are boxed by ReferenceEquals so the check doesn't compare references.
        // Null literals are better expressed as IsNull/IsNotNull and are left alone here.
        if (!IsReferenceTypeOperand(first) || !IsReferenceTypeOperand(second))
        {
            return false;
        }

        // AreSame<T>(T, T) requires inferring T from both operands.
        if (!compilation.ClassifyCommonConversion(first.Type!, second.Type!).IsImplicit &&
            !compilation.ClassifyCommonConversion(second.Type!, first.Type!).IsImplicit)
        {
            return false;
        }

        firstExpression = first.Syntax;
        secondExpression = second.Syntax;
        return true;

        static bool IsReferenceTypeOperand(IOperation operand)
            => operand.Type is { IsReferenceType: true } &&
                operand.WalkDownConversion().Type is { IsReferenceType: true };
    }

    /// <summary>
    /// Recognizes <c>Regex.IsMatch(input, pattern)</c> and <c>regex.IsMatch(input)</c>.
    /// Overloads accepting options, timeouts, start positions or spans are not recognized.
    /// </summary>
    private static bool RecognizeRegexIsMatchCheck(IOperation operation, [NotNullWhen(true)] out SyntaxNode? patternExpression, [NotNullWhen(true)] out SyntaxNode? inputExpression)
    {
        patternExpression = null;
        inputExpression = null;

        if (operation is not IInvocationOperation { TargetMethod: { Name: "IsMatch" } targetMethod } invocation ||
            targetMethod.ContainingType.ToDisplayString() != RegexFullTypeName ||
            !targetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String) ||
            !TryGetArgumentValueWithoutImplicitConversions(invocation, 0, out IOperation? input))
        {
            return false;
        }

        if (targetMethod.IsStatic)
        {
            if (targetMethod.Parameters.Length != 2 ||
                !TryGetArgumentValueWithoutImplicitConversions(invocation, 1, out IOperation? pattern))
            {
                return false;
            }

            patternExpression = pattern.Syntax;
        }
        else
        {
            if (targetMethod.Parameters.Length != 1 ||
                invocation.Instance is null or { IsImplicit: true })
            {
                return false;
            }

            patternExpression = invocation.Instance.Syntax;
        }

        inputExpression = input.Syntax;
        return true;
    }

    private static bool TryGetArgumentValueWithoutImplicitConversions(IInvocationOperation operation, int ordinal, [NotNullWhen(true)] out IOperation? argumentValue)
    {
        if (!TryGetArgumentForParameterOrdinal(operation, ordinal, out IArgumentOperation? argument) ||
            argument.IsImplicit)
        {
            argumentValue = null;
            return false;
        }

        argumentValue = argument.Value;
        while (argumentValue is IConversionOperation { IsImplicit: true } conversion)
        {
            argumentValue = conversion.Operand;
        }

        return true;
    }
}
