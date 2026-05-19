// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// Reference-equality comparer used for keys in the topology maps of <see cref="EquivalenceComparer"/>.
    /// </summary>
    private sealed class ReferenceObjectComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceObjectComparer Instance = new();

        private ReferenceObjectComparer()
        {
        }

        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Cached set of public instance properties and fields for a type, with both an alphabetically-sorted
    /// array (for deterministic traversal) and a name-keyed dictionary (for O(1) lookup).
    /// </summary>
    private sealed class MemberLookup
    {
        internal MemberLookup(MemberAccessor[] sorted, IReadOnlyDictionary<string, MemberAccessor> byName)
        {
            Sorted = sorted;
            ByName = byName;
        }

        internal MemberAccessor[] Sorted { get; }

        internal IReadOnlyDictionary<string, MemberAccessor> ByName { get; }
    }

    /// <summary>
    /// Describes how to read a single member (property or field) from an instance.
    /// </summary>
    private sealed class MemberAccessor
    {
        private readonly PropertyInfo? _property;
        private readonly FieldInfo? _field;

        internal MemberAccessor(string name, Type memberType, PropertyInfo property)
        {
            Name = name;
            MemberType = memberType;
            _property = property;
        }

        internal MemberAccessor(string name, Type memberType, FieldInfo field)
        {
            Name = name;
            MemberType = memberType;
            _field = field;
        }

        internal string Name { get; }

        internal Type MemberType { get; }

        internal object? GetValue(object instance)
            => _property is not null ? _property.GetValue(instance) : _field!.GetValue(instance);
    }
}
