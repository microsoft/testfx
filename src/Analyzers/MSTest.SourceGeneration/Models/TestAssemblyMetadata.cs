// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

/// <summary>
/// Equatable snapshot of a discovered test class. Source-generator pipeline values must be
/// value-equatable so that incremental caching works; this record carries primitive data only.
/// </summary>
internal sealed record TestClassMetadata(
    string FullyQualifiedName,
    string DisplayName,
    string? Namespace,
    EquatableArray<TestMethodMetadata> Methods);

/// <summary>
/// Equatable snapshot of a discovered test method.
/// </summary>
internal sealed record TestMethodMetadata(string Name, EquatableArray<string> ParameterTypes);

/// <summary>
/// Equatable snapshot for the full test assembly, used as the input to the emitter.
/// </summary>
internal sealed record TestAssemblyMetadata(
    string AssemblyName,
    EquatableArray<TestClassMetadata> Classes);

/// <summary>
/// Minimal value-equatable wrapper around an <see cref="ImmutableArray{T}"/>. Source generators
/// need value equality on collections; <see cref="ImmutableArray{T}"/> only has reference equality
/// out of the box, which would cause cache misses on every compilation tick.
/// </summary>
/// <typeparam name="T">The element type stored in the underlying <see cref="ImmutableArray{T}"/>.</typeparam>
internal readonly record struct EquatableArray<T>(ImmutableArray<T> Items)
    where T : IEquatable<T>
{
    public int Count => Items.IsDefault ? 0 : Items.Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(Items.IsDefault ? ImmutableArray<T>.Empty : Items)).GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        ImmutableArray<T> a = Items.IsDefault ? ImmutableArray<T>.Empty : Items;
        ImmutableArray<T> b = other.Items.IsDefault ? ImmutableArray<T>.Empty : other.Items;
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        if (Items.IsDefault)
        {
            return 0;
        }

        int hash = 17;
        foreach (T item in Items)
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }
}
