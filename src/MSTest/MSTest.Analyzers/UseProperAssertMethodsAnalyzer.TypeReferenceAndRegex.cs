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

    private static bool CanRewriteNewAssertion(IInvocationOperation assertion)
        => assertion.Arguments.TakeWhile(argument => argument.Parameter?.Ordinal != 0).All(argument => argument.IsImplicit) &&
            assertion.Arguments.All(argument => argument.IsImplicit || argument.Parameter?.Ordinal is 0 or 1);

    private static bool CanRewriteTypeAssertion(IInvocationOperation assertion)
        => assertion.Arguments.All(argument => argument.IsImplicit || argument.Parameter?.Ordinal is 0 or 1);

    private static bool HasCompatibleAssertion(INamedTypeSymbol assertType, string name, int arity, SpecialType firstParameterType)
        => assertType.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == arity &&
            method.Parameters.Length >= 2 &&
            method.Parameters[0].Type.SpecialType == firstParameterType &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_String &&
            method.Parameters[1].IsOptional);

    private static bool HasCompatibleTypeAssertion(INamedTypeSymbol assertType, bool hasMessage)
        => assertType.GetMembers("IsInstanceOfType").OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == 0 &&
            method.Parameters.Length >= (hasMessage ? 3 : 2) &&
            method.Parameters[0].Name == "value" &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
            method.Parameters[1].Name == "expectedType" &&
            method.Parameters[1].Type.Name == "Type" &&
            method.Parameters[1].Type.ContainingNamespace.ToDisplayString() == "System" &&
            (!hasMessage ||
             (method.Parameters[2].Name == "message" &&
              method.Parameters[2].Type.SpecialType == SpecialType.System_String)) &&
            method.Parameters.Skip(hasMessage ? 3 : 2).All(parameter => parameter.IsOptional));

    private static bool HasCompatibleSameAssertion(INamedTypeSymbol assertType, string name)
        => assertType.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Parameters.Length >= 3 &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_String &&
            method.Parameters[2].IsOptional &&
            ((method.Arity == 1 &&
              SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.TypeParameters[0]) &&
              SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, method.TypeParameters[0])) ||
             (method.Arity == 0 &&
              method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
              method.Parameters[1].Type.SpecialType == SpecialType.System_Object)));

    private static bool HasCompatibleRegexAssertion(INamedTypeSymbol assertType, string name, IInvocationOperation regexInvocation)
        => assertType.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == 0 &&
            method.Parameters.Length >= 3 &&
            (regexInvocation.TargetMethod.IsStatic
                ? method.Parameters[0].Type.SpecialType == SpecialType.System_String
                : SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, regexInvocation.TargetMethod.ContainingType)) &&
            method.Parameters[1].Type.SpecialType == SpecialType.System_String &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_String &&
            method.Parameters[2].IsOptional);

    private static bool CanRewriteReferenceEqualsWithoutReorderingEffects(IOperation operation)
        => operation is IInvocationOperation invocation &&
            (invocation.Arguments.FirstOrDefault(argument => !argument.IsImplicit)?.Parameter?.Ordinal == 0 ||
             (TryGetArgumentForParameterOrdinal(invocation, 0, out IArgumentOperation? first) &&
              TryGetArgumentForParameterOrdinal(invocation, 1, out IArgumentOperation? second) &&
              IsStableAssertionArgument(first.Value) &&
              IsStableAssertionArgument(second.Value)));

    private static bool CanRewriteRegexWithoutReorderingEffects(IInvocationOperation assertion, IOperation operation)
        => operation is IInvocationOperation invocation &&
            assertion.Arguments.All(argument =>
                argument.Parameter?.Ordinal == 0 ||
                argument.IsImplicit ||
                argument.Value.ConstantValue.HasValue) &&
            (!invocation.TargetMethod.IsStatic ||
             (TryGetArgumentForParameterOrdinal(invocation, 0, out IArgumentOperation? input) &&
              TryGetArgumentForParameterOrdinal(invocation, 1, out IArgumentOperation? pattern) &&
              IsStableAssertionArgument(input.Value) &&
              IsStableAssertionArgument(pattern.Value)));

    private static bool IsStableAssertionArgument(IOperation operation)
        => operation switch
        {
            IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion => IsStableAssertionArgument(conversion.Operand),
            ILocalReferenceOperation or IParameterReferenceOperation => true,
            _ => operation.ConstantValue.HasValue,
        };

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

        // The suggested methods take the value as object. The positive assertion is non-generic
        // so a mismatched value remains a soft failure inside Assert.Scope().
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
        CommonConversion firstToSecond = compilation.ClassifyCommonConversion(first.Type!, second.Type!);
        CommonConversion secondToFirst = compilation.ClassifyCommonConversion(second.Type!, first.Type!);
        if (!(firstToSecond.IsImplicit && !firstToSecond.IsUserDefined) &&
            !(secondToFirst.IsImplicit && !secondToFirst.IsUserDefined))
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
            !TryGetArgumentValueWithoutImplicitConversions(invocation, 0, out IOperation? input) ||
            IsConstantNull(input))
        {
            return false;
        }

        if (targetMethod.IsStatic)
        {
            if (targetMethod.Parameters.Length != 2 ||
                !TryGetArgumentValueWithoutImplicitConversions(invocation, 1, out IOperation? pattern) ||
                IsConstantNull(pattern))
            {
                return false;
            }

            patternExpression = pattern.Syntax;
        }
        else
        {
            if (targetMethod.Parameters.Length != 1 ||
                invocation.Instance is null or { IsImplicit: true } ||
                IsConstantNull(invocation.Instance))
            {
                return false;
            }

            patternExpression = invocation.Instance.Syntax;
        }

        inputExpression = input.Syntax;
        return true;
    }

    private static bool IsConstantNull(IOperation operation)
        => operation.ConstantValue.HasValue && operation.ConstantValue.Value is null;

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
            if (conversion.OperatorMethod is not null)
            {
                argumentValue = null;
                return false;
            }

            argumentValue = conversion.Operand;
        }

        return true;
    }
}
