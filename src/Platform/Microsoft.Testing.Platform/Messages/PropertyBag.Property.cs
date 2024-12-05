// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed partial class PropertyBag
{
    [DebuggerTypeProxy(typeof(PropertyDebugView))]
    internal /* for testing */ sealed class Property(IProperty current, Property? next = null) : IEnumerable<IProperty>
    {
        public int Count
        {
            get
            {
                int count = 1;
                Property current = this;

                while (current?.Next != null)
                {
                    current = current.Next;
                    count++;
                }

                return count;
            }
        }

        public IProperty Current { get; } = current;

        public Property? Next { get; } = next;

        public bool Contains(IProperty property)
        {
            if (property == Current)
            {
                return true;
            }

            Property current = this;

            while (current?.Next != null)
            {
                current = current.Next;

                if (current.Current == property)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Any<TProperty>()
        {
            if (Current is TProperty)
            {
                return true;
            }

            Property current = this;

            while (current?.Next != null)
            {
                current = current.Next;

                if (current.Current is TProperty)
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<TProperty> OfType<TProperty>()
        {
            if (Current is TProperty property)
            {
                yield return property;
            }

            Property current = this;

            while (current?.Next != null)
            {
                current = current.Next;

                if (current.Current is TProperty property2)
                {
                    yield return property2;
                }
            }
        }

        public IEnumerator<IProperty> GetEnumerator() => new PropertyEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new PropertyEnumerator(this);

        internal struct PropertyEnumerator(Property properties) : IEnumerator<IProperty>
        {
            private readonly Property _properties = properties;

            private Property? _current;

            // https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerator-1.current?view=netframework-4.8#remarks
            public readonly IProperty Current => _current is null ? throw new InvalidOperationException("Invalid Current state, possible wrong usage.") : _current.Current;

            // https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerator-1.current?view=netframework-4.8#remarks
            readonly object IEnumerator.Current => _current is null ? throw new InvalidOperationException("Invalid Current state, possible wrong usage.") : _current.Current;

            public bool MoveNext()
            {
                if (_current is null)
                {
                    _current = _properties;
                    return true;
                }

                while (_current.Next is not null)
                {
                    _current = _current.Next;
                    return true;
                }

                return false;
            }

            public void Reset() => _current = null;

            public void Dispose() => _current = null;
        }
    }

    // internal debug view class for PropertyDebugView
    internal sealed class PropertyDebugView
    {
        private readonly Property _property;

        public PropertyDebugView(Property property)
        {
            Guard.NotNull(property);
            _property = property;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IProperty[] Items => _property.AsEnumerable().ToArray();
    }
}
