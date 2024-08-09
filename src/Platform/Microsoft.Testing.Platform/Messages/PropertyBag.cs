// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed partial class PropertyBag
{
    // Optimized access to the TestNodeStateProperty, it's one of the most used property.
#pragma warning disable SA1401 // Fields should be private
    internal /* for testing */ TestNodeStateProperty? _testNodeStateProperty;
#pragma warning restore SA1401 // Fields should be private

    // We use a linked list to avoid array allocation when we don't have properties or we have few.
    // We expected anyway good locality because the usage is usually
    // * I fill the property bag with properties "locally"
    // * I read the properties
    // We expect mostly immutable behavior after the push inside the bus.
#pragma warning disable SA1401 // Fields should be private
    internal /* for testing */ Property? _property;
#pragma warning restore SA1401 // Fields should be private

    public PropertyBag()
    {
    }

    public PropertyBag(params IProperty[] properties)
    {
        Guard.NotNull(properties);

        if (properties.Length == 0)
        {
            return;
        }

        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i] is TestNodeStateProperty testNodeStateProperty)
            {
                if (_testNodeStateProperty is not null)
                {
                    ThrowDuplicatedPropertyType(properties[i]);
                }

                _testNodeStateProperty = testNodeStateProperty;
            }
            else
            {
                if (_property is null)
                {
                    _property = new(properties[i]);
                }
                else
                {
                    if (_property.Contains(properties[i]))
                    {
                        ThrowDuplicatedPropertyInstance(properties[i]);
                    }

                    _property = new(properties[i], _property);
                }
            }
        }
    }

    public PropertyBag(IEnumerable<IProperty> properties)
    {
        Guard.NotNull(properties);

        foreach (IProperty property in properties)
        {
            if (property is TestNodeStateProperty testNodeStateProperty)
            {
                if (_testNodeStateProperty is not null)
                {
                    ThrowDuplicatedPropertyType(property);
                }

                _testNodeStateProperty = testNodeStateProperty;
            }
            else
            {
                if (_property is null)
                {
                    _property = new(property);
                }
                else
                {
                    if (_property.Contains(property))
                    {
                        ThrowDuplicatedPropertyInstance(property);
                    }

                    _property = new(property, _property);
                }
            }
        }
    }

    public int Count => _property is null
        ? _testNodeStateProperty is null ? 0 : 1
        : _property.Count + (_testNodeStateProperty is not null ? 1 : 0);

    public void Add(IProperty property)
    {
        Guard.NotNull(property);

        // Optimized access to the TestNodeStateProperty, it's one of the most used property.
        if (property is TestNodeStateProperty testNodeStateProperty)
        {
            if (_testNodeStateProperty is not null)
            {
                ThrowDuplicatedPropertyType(property);
            }

            _testNodeStateProperty = testNodeStateProperty;
        }
        else
        {
            if (_property is null)
            {
                _property = new(property);
            }
            else
            {
                if (_property.Contains(property))
                {
                    ThrowDuplicatedPropertyInstance(property);
                }

                _property = new(property, _property);
            }
        }
    }

    public bool Any<TProperty>()
        where TProperty : IProperty
    {
        if (_testNodeStateProperty is TProperty)
        {
            return true;
        }

        // We don't want to allocate an array if we know that we're looking for a TestNodeStateProperty
        return !typeof(TestNodeStateProperty).IsAssignableFrom(typeof(TProperty)) && _property?.Any<TProperty>() == true;
    }

    public TProperty? SingleOrDefault<TProperty>()
        where TProperty : IProperty
    {
        if (_testNodeStateProperty is TProperty testNodeStateProperty)
        {
            return testNodeStateProperty;
        }

        // We don't want to allocate an array if we know that we're looking for a TestNodeStateProperty
        if (typeof(TestNodeStateProperty).IsAssignableFrom(typeof(TProperty)))
        {
            return default;
        }

        if (_property is null || _property.Count == 0)
        {
            return default;
        }

        IEnumerable<TProperty> matchingValues = _property.OfType<TProperty>();

        using IEnumerator<TProperty> enumerator = matchingValues.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return default;
        }

        TProperty property = enumerator.Current!;
        return enumerator.MoveNext()
            ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
            : property;
    }

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "No interop")]
    public TProperty Single<TProperty>()
        where TProperty : IProperty
    {
        if (_testNodeStateProperty is TProperty testNodeStateProperty)
        {
            return testNodeStateProperty;
        }

        // We don't want to allocate an array if we know that we're looking for a TestNodeStateProperty
        if (typeof(TestNodeStateProperty).IsAssignableFrom(typeof(TProperty)))
        {
            throw new InvalidOperationException($"Could not find a property of type '{typeof(TProperty)}'.");
        }

        IEnumerable<TProperty> matchingValues = _property is null ? Array.Empty<TProperty>() : _property.OfType<TProperty>();

        return !matchingValues.Any()
            ? throw new InvalidOperationException($"Could not find a property of type '{typeof(TProperty)}'.")
            : matchingValues.Skip(1).Any()
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : matchingValues.First();
    }

    public TProperty[] OfType<TProperty>()
        where TProperty : IProperty
    {
        if (_testNodeStateProperty is TProperty testNodeStateProperty)
        {
            return [testNodeStateProperty];
        }

        // We don't want to allocate an array if we know that we're looking for a TestNodeStateProperty
        return typeof(TestNodeStateProperty).IsAssignableFrom(typeof(TProperty))
            ? []
            : _property is null ? [] : _property.OfType<TProperty>().ToArray();
    }

    public IEnumerable<IProperty> AsEnumerable()
        => new PropertyBagEnumerable(_property, _testNodeStateProperty);

    // Duck typing for the enumerator, to avoid the direct usage of LINQ extension methods.
    // For LINQ usage, please use the AsEnumerable() method.
    public IEnumerator<IProperty> GetEnumerator()
        => new PropertyBagEnumerator(_property, _testNodeStateProperty);

    [DoesNotReturn]
    private static void ThrowDuplicatedPropertyType(IProperty property)
        => throw new InvalidOperationException($"Duplicated property of type '{property}'");

    [DoesNotReturn]
    private static void ThrowDuplicatedPropertyInstance(IProperty property)
        => throw new InvalidOperationException($"Duplicated property instance of type '{property}'");
}
