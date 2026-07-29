// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

/// <summary>
/// Attribute filtering logic shared by every <see cref="Interface.IReflectionOperations"/> implementation
/// and by <c>ReflectHelper</c>.
/// </summary>
/// <remarks>
/// The methods take an already-resolved (and usually cached) attribute array so that each caller keeps
/// ownership of how attributes are obtained, while the filtering semantics — including the localized error
/// message raised for duplicated attributes — live in a single place and cannot diverge between the
/// reflection-based and source-generated implementations.
/// </remarks>
internal static class AttributeQueryHelper
{
    /// <summary>
    /// Checks whether <paramref name="attributes"/> contains an attribute of the given type, or an attribute that
    /// derives from it. e.g. <c>[MyTestClass]</c> deriving from <c>[TestClass]</c> matches when looking for <c>[TestClass]</c>.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <returns>True if an attribute of the specified type is present.</returns>
    public static bool IsAttributeDefined<TAttribute>(Attribute[] attributes)
        where TAttribute : Attribute
    {
        foreach (Attribute attribute in attributes)
        {
            AssertNotNull<TAttribute>(attribute);

            if (attribute is TAttribute)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the first attribute that matches the type.
    /// Use this together with an attribute that does not allow multiple and is sealed. In such case there cannot be
    /// more attributes, and this will avoid the cost of checking for more than one attribute.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TAttribute"/> being sealed is a caller contract enforced by a DEBUG-only assert. On .NET
    /// Framework DEBUG builds <see cref="DebugEx.Assert(bool, string)"/> calls <see cref="Environment.FailFast(string, Exception)"/>,
    /// so violating it takes down the test host instead of failing a single test. Use
    /// <see cref="GetSingleAttributeOrDefault{TAttribute}"/> or <see cref="GetAttributes{TAttribute}"/> for non-sealed
    /// attribute types.
    /// </remarks>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <returns>The attribute that is found or null.</returns>
    public static TAttribute? GetFirstAttributeOrDefault<TAttribute>(Attribute[] attributes)
        where TAttribute : Attribute
    {
        // If the attribute is not sealed, then it can allow multiple, even if AllowMultiple is false.
        // This happens when a derived type is also applied along with the base type.
        // Or, if the derived type modifies the attribute usage to allow multiple.
        // So we want to ensure this is only called for sealed attributes.
        DebugEx.Assert(typeof(TAttribute).IsSealed, $"Expected '{typeof(TAttribute)}' to be sealed, but was not.");

        foreach (Attribute attribute in attributes)
        {
            AssertNotNull<TAttribute>(attribute);

            if (attribute is TAttribute attributeAsTAttribute)
            {
                return attributeAsTAttribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the single attribute that matches the type or is derived from it.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    public static TAttribute? GetSingleAttributeOrDefault<TAttribute>(Attribute[] attributes)
        where TAttribute : Attribute
    {
        TAttribute? foundAttribute = null;
        foreach (Attribute attribute in attributes)
        {
            AssertNotNull<TAttribute>(attribute);

            if (attribute is TAttribute attributeAsTAttribute)
            {
                if (foundAttribute is not null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.DuplicateAttributeError, typeof(TAttribute)));
                }

                foundAttribute = attributeAsTAttribute;
            }
        }

        return foundAttribute;
    }

    /// <summary>
    /// Gets every attribute which is of the given type or a subtype of it.
    /// </summary>
    /// <remarks>
    /// Filtering is lazy, but <paramref name="attributes"/> is captured eagerly by the caller, so any argument
    /// validation performed while resolving the attribute array happens before enumeration starts.
    /// </remarks>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <returns>The matching attributes, in declaration order.</returns>
    public static IEnumerable<TAttribute> GetAttributes<TAttribute>(Attribute[] attributes)
        where TAttribute : Attribute
    {
        foreach (Attribute attribute in attributes)
        {
            AssertNotNull<TAttribute>(attribute);

            if (attribute is TAttribute attributeAsTAttribute)
            {
                yield return attributeAsTAttribute;
            }
        }
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every attribute which is of the given type or a subtype of it.
    /// </summary>
    /// <remarks>
    /// This is the allocation-free counterpart of <see cref="GetAttributes{TAttribute}"/>: it avoids the iterator
    /// state machine that enumerating the returned sequence would allocate.
    /// </remarks>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <typeparam name="TState">The type of state to be passed to <paramref name="action"/>.</typeparam>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="state">The state to pass to <paramref name="action"/>.</param>
    public static void PerformActionOnAttribute<TAttribute, TState>(Attribute[] attributes, Action<TAttribute, TState?> action, TState? state)
        where TAttribute : Attribute
    {
        foreach (Attribute attribute in attributes)
        {
            AssertNotNull<TAttribute>(attribute);

            if (attribute is TAttribute attributeAsTAttribute)
            {
                action(attributeAsTAttribute, state);
            }
        }
    }

    [Conditional("DEBUG")]
    private static void AssertNotNull<TAttribute>(Attribute attribute)
        where TAttribute : Attribute
        => DebugEx.Assert(attribute is not null, $"{nameof(AttributeQueryHelper)}: internal error: null entry in the attributes array while looking for '{typeof(TAttribute)}'.");
}
