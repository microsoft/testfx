// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

internal static class AssertConditionAnalyzerHelper
{
    internal enum EqualityStatus
    {
        Unknown,
        Equal,
        NotEqual,
    }

    internal const string ExpectedParameterName = "expected";
    internal const string NotExpectedParameterName = "notExpected";
    internal const string ActualParameterName = "actual";
    internal const string ConditionParameterName = "condition";
    internal const string ValueParameterName = "value";

    internal static bool IsNotNullableType(IOperation valueArgumentOperation)
    {
        ITypeSymbol? valueArgType = valueArgumentOperation.GetReferencedMemberOrLocalOrParameter().GetReferencedMemberOrLocalOrParameter();
        return valueArgType is not null
            && valueArgType.IsValueType
            && valueArgType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    internal static IOperation? GetArgumentWithName(IInvocationOperation operation, string name)
        => operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == name)?.Value.WalkDownConversion();

    internal static IOperation? GetConditionArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ConditionParameterName);

    internal static IOperation? GetValueArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ValueParameterName);

    internal static EqualityStatus GetEqualityStatus(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
    {
        if (GetArgumentWithName(operation, expectedOrNotExpectedParameterName) is { } expectedOrNotExpectedArgument &&
            GetArgumentWithName(operation, ActualParameterName) is { } actualArgument)
        {
            if (expectedOrNotExpectedArgument.ConstantValue.HasValue &&
                actualArgument.ConstantValue.HasValue)
            {
                return Equals(expectedOrNotExpectedArgument.ConstantValue.Value, actualArgument.ConstantValue.Value) ? EqualityStatus.Equal : EqualityStatus.NotEqual;
            }

            if (AreSameReference(expectedOrNotExpectedArgument, actualArgument))
            {
                return EqualityStatus.Equal;
            }
        }

        // We are not sure about the equality status
        return EqualityStatus.Unknown;
    }

    internal static bool AreSameReference(IOperation? left, IOperation? right)
    {
        left = WalkDownConversionsAndParentheses(left);
        right = WalkDownConversionsAndParentheses(right);

        return (left, right) switch
        {
            (ILocalReferenceOperation leftLocal, ILocalReferenceOperation rightLocal)
                => SymbolEqualityComparer.Default.Equals(leftLocal.Local, rightLocal.Local),

            (IParameterReferenceOperation leftParameter, IParameterReferenceOperation rightParameter)
                => SymbolEqualityComparer.Default.Equals(leftParameter.Parameter, rightParameter.Parameter),

            (IFieldReferenceOperation leftField, IFieldReferenceOperation rightField)
                => SymbolEqualityComparer.Default.Equals(leftField.Field, rightField.Field)
                && AreSameInstance(leftField.Instance, rightField.Instance),

            (IPropertyReferenceOperation leftProperty, IPropertyReferenceOperation rightProperty)
                => SymbolEqualityComparer.Default.Equals(leftProperty.Property, rightProperty.Property)
                && AreSameInstance(leftProperty.Instance, rightProperty.Instance),

            (IInstanceReferenceOperation, IInstanceReferenceOperation) => true,

            _ => false,
        };
    }

    private static IOperation? WalkDownConversionsAndParentheses(IOperation? operation)
    {
        while (operation is IConversionOperation or IParenthesizedOperation)
        {
            operation = operation switch
            {
                IConversionOperation conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => operation,
            };
        }

        return operation;
    }

    private static bool AreSameInstance(IOperation? left, IOperation? right)
        => (left is null && right is null)
        || (left is not null && right is not null && AreSameReference(left, right));
}
