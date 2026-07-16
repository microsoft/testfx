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
    /// same side-effect-free reference (see <see cref="HasIdenticalExpectedAndActual"/>) <em>and</em> a
    /// self-comparison of that value using the default equality comparer is provably always equal.
    /// </summary>
    internal static bool HasIdenticalExpectedAndActualWithBuiltInEquality(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
        => HasIdenticalExpectedAndActual(operation, expectedOrNotExpectedParameterName)
        && IsProvablyReflexiveSelfEquality(GetArgumentWithName(operation, expectedOrNotExpectedParameterName)?.Type);

    /// <summary>
    /// Returns <see langword="true"/> when the invoked <c>Assert</c> overload accepts a caller-supplied
    /// <see cref="System.Collections.Generic.IEqualityComparer{T}"/>, in which case the comparison can
    /// return any result and must not be treated as an always-true/always-false condition.
    /// </summary>
    internal static bool HasEqualityComparerParameter(IInvocationOperation operation)
        => operation.TargetMethod.Parameters.Any(static parameter =>
            parameter.Type is INamedTypeSymbol
            {
                Name: "IEqualityComparer",
                ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } },
            });

    /// <summary>
    /// Returns <see langword="true"/> when a self-comparison of a value of <paramref name="type"/> using the
    /// default equality comparer is provably always equal.
    /// </summary>
    /// <remarks>
    /// <c>Assert.AreEqual</c>/<c>AreNotEqual</c> compare using <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>,
    /// which dispatches to <see cref="System.IEquatable{T}"/><c>.Equals</c> when the type implements it, otherwise to the
    /// virtual <see cref="object.Equals(object)"/>. The result is only provable when the runtime type is known exactly
    /// (a value type or a sealed type) and that type does not customize equality with a potentially non-reflexive
    /// override. A non-sealed reference type, an interface, or a type parameter can hold an instance whose overridden
    /// equality is not reflexive, so nothing can be proven from the static type.
    /// </remarks>
    private static bool IsProvablyReflexiveSelfEquality(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        // A type parameter can be substituted with a type whose equality is not reflexive.
        if (type.TypeKind is TypeKind.TypeParameter)
        {
            return false;
        }

        // Arrays are non-sealed reference types but always use reflexive reference equality
        // (they never override Equals), so a self-comparison is provably always equal.
        if (type.TypeKind is TypeKind.Array)
        {
            return true;
        }

        // A non-sealed reference type (including object, base classes, and interfaces) can hold a derived
        // instance whose overridden Equals is not reflexive. Only value types and sealed types have an
        // exactly-known runtime type.
        return type is not { IsReferenceType: true, IsSealed: false }
            && !HasUserDefinedEquality(type);
    }

    private static bool HasUserDefinedEquality(ITypeSymbol type)
    {
        // Primitives, enums, and other well-known types have reflexive, built-in equality.
        if (type.SpecialType != SpecialType.None || type.TypeKind is TypeKind.Enum)
        {
            return false;
        }

        // The == operator is never consulted by EqualityComparer<T>.Default, so it is intentionally not checked here.
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
