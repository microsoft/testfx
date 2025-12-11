// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents a property bag.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class.
    /// </summary>
    public PropertyBag()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class.
    /// </summary>
    /// <param name="properties">The collection of properties.</param>
    public PropertyBag(params IProperty[] properties)
    {
        Ensure.NotNull(properties);

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class.
    /// </summary>
    /// <param name="properties">The collection of properties.</param>
    public PropertyBag(IEnumerable<IProperty> properties)
    {
        Ensure.NotNull(properties);

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

    /// <summary>
    /// Gets the number of properties in the bag.
    /// </summary>
    public int Count => _property is null
        ? _testNodeStateProperty is null ? 0 : 1
        : _property.Count + (_testNodeStateProperty is not null ? 1 : 0);

    /// <summary>
    /// Adds a property to the bag.
    /// </summary>
    /// <param name="property">The property to add.</param>
    public void Add(IProperty property)
    {
        Ensure.NotNull(property);

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

    /// <summary>
    /// Determines whether the bag contains a property of the specified type.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <returns><c>true</c> if the bag contains a property of the specified type; <c>false</c> otherwise.</returns>
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

    /// <summary>
    /// Returns the only property of the <typeparamref name="TProperty"/> type, or default, and throws an exception if there is more than one element.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <returns>The single item of the given type or default.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than one property of the given type was found.</exception>
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

    /// <summary>
    /// Returns the only property of the <typeparamref name="TProperty"/> type, and throws an exception if there is not exactly one element.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <returns>The single property of the given type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not exactly one property of the given type was found.</exception>
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

        IEnumerable<TProperty> matchingValues = _property is null ? [] : _property.OfType<TProperty>();

        return !matchingValues.Any()
            ? throw new InvalidOperationException($"Could not find a property of type '{typeof(TProperty)}'.")
            : matchingValues.Skip(1).Any()
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : matchingValues.First();
    }

    /// <summary>
    /// Gets the properties of the <typeparamref name="TProperty"/> type.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>An array of properties matching the given type.</returns>
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
            : _property is null ? [] : [.. _property.OfType<TProperty>()];
    }

    /// <summary>
    /// Returns an enumerable that iterates through the collection.
    /// </summary>
    /// <returns>The collection of properties.</returns>
    public IEnumerable<IProperty> AsEnumerable()
        => new PropertyBagEnumerable(_property, _testNodeStateProperty);

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator of <see cref="IProperty"/>.</returns>
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
