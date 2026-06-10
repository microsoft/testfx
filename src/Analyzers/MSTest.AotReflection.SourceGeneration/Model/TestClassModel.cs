// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MSTest.AotReflection.SourceGeneration.Model;

/// <summary>
/// A reified attribute application: the attribute class plus its ctor / named args, captured
/// from <see cref="Microsoft.CodeAnalysis.AttributeData"/> so the generator can emit the
/// equivalent <c>new TAttr(arg1, ...) { Name = ... }</c> at the call site.
/// </summary>
internal sealed record AttributeApplicationModel(
    string FullyQualifiedAttributeType,
    EquatableArray<TypedConstantModel> ConstructorArguments,
    EquatableArray<NamedArgumentModel> NamedArguments);

internal sealed record NamedArgumentModel(string Name, TypedConstantModel Value);

/// <summary>
/// Minimal projection of <see cref="Microsoft.CodeAnalysis.TypedConstant"/> that survives
/// across incremental generator runs (the real type isn't equatable).
/// </summary>
internal sealed record TypedConstantModel(
    ConstantValueKind Kind,
    string? FullyQualifiedType,
    object? PrimitiveValue,
    EquatableArray<TypedConstantModel> ArrayElements);

internal enum ConstantValueKind
{
    Primitive,
    Enum,
    Type,
    Array,
    Null,
}

internal sealed record TestParameterModel(string FullyQualifiedType, string Name);

internal sealed record TestMethodModel(
    string Name,
    bool IsStatic,
    bool IsAsync,
    bool ReturnsTask,
    bool ReturnsValueTask,
    bool ReturnsVoid,
    EquatableArray<TestParameterModel> Parameters,
    EquatableArray<AttributeApplicationModel> Attributes);

internal sealed record TestPropertyModel(
    string Name,
    string FullyQualifiedType,
    bool HasPublicSetter,
    EquatableArray<AttributeApplicationModel> Attributes);

internal sealed record TestConstructorModel(
    EquatableArray<TestParameterModel> Parameters);

/// <summary>
/// Assembly-scoped metadata captured at compile time so the consumer never has to call
/// <see cref="System.Reflection.Assembly.GetCustomAttributes(System.Type, bool)"/> for
/// attributes declared with <c>[assembly: ...]</c> in the same compilation.
/// </summary>
internal sealed record AssemblyMetadataModel(
    EquatableArray<AttributeApplicationModel> Attributes);

internal sealed record TestClassModel(
    string FullyQualifiedTypeName,
    string ContainingNamespace,
    string TypeName,
    bool IsAbstract,
    bool IsStatic,
    EquatableArray<TestConstructorModel> Constructors,
    EquatableArray<TestMethodModel> Methods,
    EquatableArray<TestPropertyModel> Properties,
    EquatableArray<AttributeApplicationModel> Attributes);

/// <summary>
/// Value-equatable wrapper around <see cref="ImmutableArray{T}"/> so incremental generation
/// can cache results between runs. Kept minimal — we don't need indexing in this PoC.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
        => _array = array;

    public int Length => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => _array[index];

    public ImmutableArray<T> AsImmutableArray()
        => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        ImmutableArray<T> left = AsImmutableArray();
        ImmutableArray<T> right = other.AsImmutableArray();
        for (int i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        // Combine element hashes (using the same fold .NET uses for HashCode.Combine of ints).
        int hash = 17;
        foreach (T item in AsImmutableArray())
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }
}

internal static class EquatableArrayExtensions
{
    private static readonly Func<object?, bool> NotNullTest = x => x is not null;

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
        => new(source.ToImmutableArray());

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
        => source.Where((Func<T?, bool>)NotNullTest)!;
}
