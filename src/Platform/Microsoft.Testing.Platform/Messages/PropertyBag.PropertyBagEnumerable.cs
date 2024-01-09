// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed partial class PropertyBag
{
    private readonly struct PropertyBagEnumerable(Property? properties, TestNodeStateProperty? testNodeStateProperty) : IEnumerable<IProperty>
    {
        private readonly Property? _properties = properties;
        private readonly TestNodeStateProperty? _testNodeStateProperty = testNodeStateProperty;

        public IEnumerator<IProperty> GetEnumerator() => new PropertyBagEnumerator(_properties, _testNodeStateProperty);

        IEnumerator IEnumerable.GetEnumerator() => new PropertyBagEnumerator(_properties, _testNodeStateProperty);
    }

    private struct PropertyBagEnumerator(Property? properties, TestNodeStateProperty? testNodeStateProperty) : IEnumerator<IProperty>
    {
        private readonly Property? _properties = properties;
        private readonly TestNodeStateProperty? _testNodeStateProperty = testNodeStateProperty;
        private Property? _currentPropertyObj;
        private IProperty? _current;

        // https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerator-1.current?view=netframework-4.8#remarks
        public readonly IProperty Current => _current is null ? throw new InvalidOperationException("Invalid Current state, possible wrong usage.") : _current;

        // https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerator-1.current?view=netframework-4.8#remarks
        readonly object IEnumerator.Current => _current is null ? throw new InvalidOperationException("Invalid Current state, possible wrong usage.") : _current;

        public bool MoveNext()
        {
            if (_properties is not null)
            {
                if (_currentPropertyObj is null)
                {
                    _currentPropertyObj = _properties;
                    _current = _currentPropertyObj.Current;
                    return true;
                }

                while (_currentPropertyObj.Next is not null)
                {
                    _currentPropertyObj = _currentPropertyObj.Next;
                    _current = _currentPropertyObj.Current;
                    return true;
                }
            }

            if (!ReferenceEquals(_testNodeStateProperty, _current) && _testNodeStateProperty is not null)
            {
                _current = _testNodeStateProperty;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _currentPropertyObj = null;
            _current = null;
        }

        public void Dispose()
        {
            _currentPropertyObj = null;
            _current = null;
        }
    }
}
