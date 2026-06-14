// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers.Helpers;

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

    internal static bool HasIdenticalExpectedAndActual(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
        => GetRawArgumentValueWithName(operation, expectedOrNotExpectedParameterName) is { } expectedArgument
        && GetRawArgumentValueWithName(operation, ActualParameterName) is { } actualArgument
        && expectedArgument.IsEquivalentReferenceTo(actualArgument);

    internal static IOperation? GetArgumentWithName(IInvocationOperation operation, string name)
        => operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == name)?.Value.WalkDownConversion();

    internal static IOperation? GetConditionArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ConditionParameterName);

    internal static IOperation? GetValueArgument(IInvocationOperation operation)
        => GetArgumentWithName(operation, ValueParameterName);

    internal static EqualityStatus GetEqualityStatus(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
    {
        if (GetArgumentWithName(operation, expectedOrNotExpectedParameterName) is { } expectedOrNotExpectedArgument &&
            GetArgumentWithName(operation, ActualParameterName) is { } actualArgument &&
            expectedOrNotExpectedArgument.ConstantValue.HasValue &&
            actualArgument.ConstantValue.HasValue)
        {
            return Equals(expectedOrNotExpectedArgument.ConstantValue.Value, actualArgument.ConstantValue.Value) ? EqualityStatus.Equal : EqualityStatus.NotEqual;
        }

        // We are not sure about the equality status
        return EqualityStatus.Unknown;
    }

    internal static bool IsNotNullableType(IOperation valueArgumentOperation)
    {
        ITypeSymbol? valueArgType = valueArgumentOperation.GetReferencedMemberOrLocalOrParameter().GetReferencedMemberOrLocalOrParameter();
        return valueArgType is not null
            && valueArgType.IsValueType
            && valueArgType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    private static IOperation? GetRawArgumentValueWithName(IInvocationOperation operation, string name)
        => operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == name)?.Value;
}
