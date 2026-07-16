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

    /// <summary>
    /// Returns <see langword="true"/> when <c>expected</c>/<c>notExpected</c> and <c>actual</c> are the
    /// same side-effect-free reference (see <see cref="HasIdenticalExpectedAndActual"/>) <em>and</em> the
    /// compared type does not route equality through user code.
    /// </summary>
    /// <remarks>
    /// <c>Assert.AreEqual</c>/<c>AreNotEqual</c> compare using <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>,
    /// which calls user-provided equality when the type overrides <see cref="object.Equals(object)"/> or implements
    /// <see cref="System.IEquatable{T}"/>. In those cases a self-comparison can legitimately be used to test the type's
    /// equality contract and might not always evaluate to the same result, so it must not be reported as an
    /// always-true/always-false condition.
    /// </remarks>
    internal static bool HasIdenticalExpectedAndActualWithBuiltInEquality(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
        => HasIdenticalExpectedAndActual(operation, expectedOrNotExpectedParameterName)
        && !HasUserDefinedEquality(GetArgumentWithName(operation, expectedOrNotExpectedParameterName)?.Type);

    private static bool HasUserDefinedEquality(ITypeSymbol? type)
    {
        // Primitives, enums, and other well-known types have reflexive, built-in equality.
        if (type is null || type.SpecialType != SpecialType.None || type.TypeKind is TypeKind.Enum)
        {
            return false;
        }

        // A type parameter can be substituted with any type, including one whose equality is not
        // reflexive, so we cannot prove the comparison is always true. Treat it conservatively.
        if (type.TypeKind is TypeKind.TypeParameter)
        {
            return true;
        }

        // Assert.AreEqual/AreNotEqual compare with EqualityComparer<T>.Default, which dispatches to
        // IEquatable<T>.Equals when the type implements it, otherwise to the virtual object.Equals.
        // The == operator is never consulted, so it is intentionally not checked here.
        for (ITypeSymbol? current = type;
            current is { SpecialType: not SpecialType.System_Object and not SpecialType.System_ValueType };
            current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is IMethodSymbol { IsOverride: true, Name: nameof(object.Equals), Parameters: [{ Type.SpecialType: SpecialType.System_Object }] })
                {
                    return true;
                }
            }
        }

        foreach (INamedTypeSymbol @interface in type.AllInterfaces)
        {
            if (@interface is { Name: "IEquatable", TypeArguments: [{ } typeArgument], ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
                && SymbolEqualityComparer.Default.Equals(typeArgument, type))
            {
                return true;
            }
        }

        return false;
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
