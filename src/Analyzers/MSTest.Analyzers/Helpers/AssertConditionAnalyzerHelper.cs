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
        && IsProvablyReflexiveSelfEquality(GetComparedType(operation, expectedOrNotExpectedParameterName));

    /// <summary>
    /// Returns <see langword="true"/> when the invoked <c>Assert</c> overload is passed a non-default
    /// <see cref="System.Collections.Generic.IEqualityComparer{T}"/> argument, in which case the comparison can
    /// return any result and must not be treated as an always-true/always-false condition. A <see langword="null"/>
    /// comparer is treated by MSTest as <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>, so it
    /// does not change the equality semantics and is not considered a custom comparer here.
    /// </summary>
    internal static bool HasNonDefaultEqualityComparerArgument(IInvocationOperation operation)
    {
        IParameterSymbol? comparerParameter = operation.TargetMethod.Parameters.FirstOrDefault(static parameter =>
            parameter.Type is INamedTypeSymbol
            {
                Name: "IEqualityComparer",
                ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } },
            });

        if (comparerParameter is not { Type: INamedTypeSymbol { TypeArguments: [{ } comparerElementType] } })
        {
            return false;
        }

        // A null (or omitted) comparer, or an explicit EqualityComparer<T>.Default, is equivalent to the default
        // comparer and does not change the equality semantics. Any other comparer (including a non-constant one)
        // can return an arbitrary result. Strip only built-in conversions so a user-defined conversion cannot hide
        // a null source that it turns into a non-null comparer.
        IOperation? comparerArgument = GetRawArgumentValueWithName(operation, comparerParameter.Name)?.WalkDownBuiltInConversion();
        return comparerArgument is not null
            && comparerArgument.ConstantValue is not { HasValue: true, Value: null }
            && !IsDefaultEqualityComparerReference(comparerArgument, comparerElementType);
    }

    // EqualityComparer<X>.Default is only the default comparer for T when X is T. IEqualityComparer<T> is
    // contravariant, so e.g. EqualityComparer<Base>.Default can be passed as IEqualityComparer<Derived>, in
    // which case it is a non-default comparer that may return a different result.
    private static bool IsDefaultEqualityComparerReference(IOperation operation, ITypeSymbol comparerElementType)
        => operation is IPropertyReferenceOperation { Property: { Name: "Default", ContainingType: INamedTypeSymbol { Name: "EqualityComparer", TypeArguments: [{ } elementType], ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } } } }
            && SymbolEqualityComparer.Default.Equals(elementType, comparerElementType);

    /// <summary>
    /// Gets the type <c>T</c> whose <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/> the
    /// invoked <c>Assert.AreEqual</c>/<c>AreNotEqual</c> overload uses to compare the values. For the generic
    /// overloads this is the method type argument (not the operand's static type, which may differ through an
    /// implicit conversion); for non-generic overloads it is the declared parameter type.
    /// </summary>
    private static ITypeSymbol? GetComparedType(IInvocationOperation operation, string expectedOrNotExpectedParameterName)
        => operation.TargetMethod.TypeArguments is [{ } typeArgument]
            ? typeArgument
            : operation.TargetMethod.Parameters.FirstOrDefault(parameter => parameter.Name == expectedOrNotExpectedParameterName)?.Type;

    /// <summary>
    /// Returns <see langword="true"/> when a self-comparison of a value of <paramref name="type"/> using the
    /// default equality comparer is provably always equal.
    /// </summary>
    /// <remarks>
    /// <c>Assert.AreEqual</c>/<c>AreNotEqual</c> compare using <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>,
    /// which dispatches to <see cref="System.IEquatable{T}"/><c>.Equals</c> when the type implements it, otherwise to the
    /// virtual <see cref="object.Equals(object)"/>. Reflexivity can only be proven conservatively:
    /// <list type="bullet">
    /// <item>primitives, <see cref="string"/>, and enums have reflexive built-in equality;</item>
    /// <item>arrays and sealed reference types without customized equality use reflexive reference equality;</item>
    /// <item>a non-sealed reference type, an interface, or a type parameter can hold an instance whose overridden
    /// equality is not reflexive;</item>
    /// <item>a non-primitive value type relying on the default field-based <c>ValueType.Equals</c> (or <c>Nullable&lt;T&gt;</c>,
    /// which delegates to the underlying type) may compare a field whose equality is not reflexive, and a custom struct
    /// override could itself be non-reflexive.</item>
    /// </list>
    /// </remarks>
    private static bool IsProvablyReflexiveSelfEquality(ITypeSymbol? type)
    {
        if (type is null || type.TypeKind is TypeKind.TypeParameter)
        {
            return false;
        }

        if (type.IsReferenceType)
        {
            // Arrays always use reflexive reference equality (they never override Equals). Any other reference
            // type is only provable when its runtime type is known exactly (sealed) and it does not customize
            // equality with a potentially non-reflexive override. Non-sealed types, interfaces, and object can
            // hold a derived instance whose overridden Equals is not reflexive.
            return type.TypeKind is TypeKind.Array
                || (type.IsSealed && !HasUserDefinedEquality(type));
        }

        // Value types: only primitives (and enums) have provably reflexive built-in equality. Nullable<T> and
        // structs relying on the default (or a custom) equality could be non-reflexive.
        return type.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T
            && (type.SpecialType is not SpecialType.None || type.TypeKind is TypeKind.Enum);
    }

    private static bool HasUserDefinedEquality(ITypeSymbol type)
    {
        // string overrides Equals but its equality is reflexive; it is handled as a primitive by the caller.
        if (type.SpecialType != SpecialType.None)
        {
            return false;
        }

        // The == operator is never consulted by EqualityComparer<T>.Default, so it is intentionally not checked here.
        for (ITypeSymbol? current = type;
            current is { SpecialType: not SpecialType.System_Object and not SpecialType.System_ValueType };
            current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(nameof(object.Equals)))
            {
                if (member is IMethodSymbol { IsOverride: true, Parameters: [{ Type.SpecialType: SpecialType.System_Object }] })
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
