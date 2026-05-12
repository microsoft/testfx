// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for mocking")]
internal class ReflectHelper : MarshalByRefObject
{
#pragma warning disable RS0030 // Do not use banned APIs
    private static readonly Lazy<ReflectHelper> InstanceValue = new(() => new());
#pragma warning restore RS0030 // Do not use banned APIs

    // PERF: Attribute caching is now centralized in ReflectionOperations._attributeCache.
    // ReflectHelper delegates to PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached
    // so that discovery and execution paths share a single cache, avoiding double memory usage.
    public static ReflectHelper Instance => InstanceValue.Value;

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it. e.g. [MyTestClass] from [TestClass] will match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method to inspect for attributes.</param>
    /// <returns>True if the attribute of the specified type is defined.</returns>
    public virtual /* for testing */ bool IsAttributeDefined<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
    {
        if (attributeProvider is null)
        {
            throw new ArgumentNullException(nameof(attributeProvider));
        }

        // Get all attributes on the member.
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, $"{nameof(ReflectHelper)}.{nameof(GetCustomAttributesCached)}: internal error: wrong value in the attributes dictionary.");

            if (attribute is TAttribute)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
#if NET5_0_OR_GREATER
    [Obsolete("MarshalByRefObject.InitializeLifetimeService is obsolete in .NET 5+. This override is required to maintain infinite lifetime service.")]
#endif
    public override object InitializeLifetimeService() => null!;

    /// <summary>
    /// Gets first attribute that matches the type.
    /// Use this together with attribute that does not allow multiple and is sealed. In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <returns>The attribute that is found or null.</returns>
    public virtual /* for tests, for moq */ TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
    {
        // If the attribute is not sealed, then it can allow multiple, even if AllowMultiple is false.
        // This happens when a derived type is also applied along with the base type.
        // Or, if the derived type modifies the attribute usage to allow multiple.
        // So we want to ensure this is only called for sealed attributes.
        DebugEx.Assert(typeof(TAttribute).IsSealed, $"Expected '{typeof(TAttribute)}' to be sealed, but was not.");

        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider);

        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (cachedAttribute is TAttribute cachedAttributeAsTAttribute)
            {
                return cachedAttributeAsTAttribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets first attribute that matches the type or is derived from it.
    /// Use this together with attribute that does not allow multiple. In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    public virtual /* for tests, for moq */ TAttribute? GetSingleAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider);

        TAttribute? foundAttribute = null;
        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (cachedAttribute is TAttribute cachedAttributeAsTAttribute)
            {
                if (foundAttribute is not null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Resource.DuplicateAttributeError, typeof(TAttribute)));
                }

                foundAttribute = cachedAttributeAsTAttribute;
            }
        }

        return foundAttribute;
    }

    /// <summary>
    /// Match return type of method.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="returnType">The return type to match.</param>
    /// <returns>True if there is a match.</returns>
    internal static bool MatchReturnType(MethodInfo method, Type returnType)
        => method.ReturnType.Equals(returnType);

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    internal static string[] GetTestCategories(MemberInfo categoryAttributeProvider, Type owningType)
    {
        Attribute[] methodAttributes = GetCustomAttributesCached(categoryAttributeProvider);
        Attribute[] typeAttributes = GetCustomAttributesCached(owningType);
        Attribute[] assemblyAttributes = GetCustomAttributesCached(owningType.Assembly);

        // Avoid LINQ iterator allocations by iterating the cached attribute arrays directly.
        // This follows the same allocation-free pattern used by GetTestPropertiesAsTraits.
        List<string>? categories = null;

        foreach (Attribute attribute in methodAttributes)
        {
            if (attribute is TestCategoryBaseAttribute categoryAttr)
            {
                (categories ??= []).AddRange(categoryAttr.TestCategories);
            }
        }

        foreach (Attribute attribute in typeAttributes)
        {
            if (attribute is TestCategoryBaseAttribute categoryAttr)
            {
                (categories ??= []).AddRange(categoryAttr.TestCategories);
            }
        }

        foreach (Attribute attribute in assemblyAttributes)
        {
            if (attribute is TestCategoryBaseAttribute categoryAttr)
            {
                (categories ??= []).AddRange(categoryAttr.TestCategories);
            }
        }

        return categories is null ? [] : [.. categories];
    }

    /// <summary>
    /// Gets the parallelization level set on an assembly.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    /// <returns> The parallelization level if set. -1 otherwise. </returns>
    internal static ParallelizeAttribute? GetParallelizeAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(ParallelizeAttribute))
            .OfType<ParallelizeAttribute>()
            .FirstOrDefault();

    /// <summary>
    /// Returns whether the assembly has discover internals attribute.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    internal static bool HasDiscoverInternalsAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(DiscoverInternalsAttribute)).Length > 0;

    /// <summary>
    /// Gets TestDataSourceDiscovery assembly level attribute.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    internal static TestDataSourceDiscoveryOption? GetTestDataSourceDiscoveryOption(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(TestDataSourceDiscoveryAttribute))
            .OfType<TestDataSourceDiscoveryAttribute>()
            .FirstOrDefault()?.DiscoveryOption;

    /// <summary>
    /// Gets TestDataSourceOptions assembly level attribute.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    /// <returns> The TestDataSourceOptionsAttribute if set. Null otherwise. </returns>
    internal static TestDataSourceOptionsAttribute? GetTestDataSourceOptions(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(TestDataSourceOptionsAttribute))
            .OfType<TestDataSourceOptionsAttribute>()
            .FirstOrDefault();

    /// <summary>
    /// Get the parallelization behavior for a test assembly.
    /// </summary>
    /// <param name="assembly">The test assembly.</param>
    /// <returns>True if test assembly should not run in parallel.</returns>
    internal static bool IsDoNotParallelizeSet(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(DoNotParallelizeAttribute))
            .Length != 0;

    /// <summary>
    /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
    /// </summary>
    /// <param name="testPropertyProvider">The member to inspect.</param>
    /// <returns>List of traits.</returns>
    internal static Trait[] GetTestPropertiesAsTraits(MethodInfo testPropertyProvider)
    {
        Attribute[] attributesFromMethod = GetCustomAttributesCached(testPropertyProvider);
        Attribute[] attributesFromClass = testPropertyProvider.ReflectedType is { } testClass ? GetCustomAttributesCached(testClass) : [];
        int countTestPropertyAttribute = 0;
        foreach (Attribute attribute in attributesFromMethod)
        {
            if (attribute is TestPropertyAttribute)
            {
                countTestPropertyAttribute++;
            }
        }

        foreach (Attribute attribute in attributesFromClass)
        {
            if (attribute is TestPropertyAttribute)
            {
                countTestPropertyAttribute++;
            }
        }

        if (countTestPropertyAttribute == 0)
        {
            // This is the common case that we optimize for. This method used to be an iterator (uses yield return) which is allocating unnecessarily in common cases.
            return [];
        }

        var traits = new Trait[countTestPropertyAttribute];
        int index = 0;
        foreach (Attribute attribute in attributesFromMethod)
        {
            if (attribute is TestPropertyAttribute testProperty)
            {
                traits[index++] = new Trait(testProperty.Name, testProperty.Value);
            }
        }

        foreach (Attribute attribute in attributesFromClass)
        {
            if (attribute is TestPropertyAttribute testProperty)
            {
                traits[index++] = new Trait(testProperty.Name, testProperty.Value);
            }
        }

        return traits;
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>An instance of the attribute.</returns>
    internal virtual /* for tests, for moq */ IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            if (attribute is TAttributeType attributeAsAttributeType)
            {
                yield return attributeAsAttributeType;
            }
        }
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <typeparam name="TState">The type of state to be passed to Action.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="state">The state to pass to action.</param>
    internal static void PerformActionOnAttribute<TAttributeType, TState>(ICustomAttributeProvider attributeProvider, Action<TAttributeType, TState?> action, TState? state)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider);
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            if (attribute is TAttributeType attributeAsAttributeType)
            {
                action(attributeAsAttributeType, state);
            }
        }
    }

    /// <summary>
    /// Gets and caches the attributes for the given type, or method.
    /// Delegates to <see cref="PlatformServiceProvider.Instance"/> so that
    /// discovery and execution share a single attribute cache.
    /// </summary>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>attributes defined.</returns>
    internal static Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(attributeProvider);

    internal static /* for tests */ void ClearCache()
    {
        // Delegate to the shared cache in ReflectionOperations.
        if (PlatformServiceProvider.Instance?.ReflectionOperations is ReflectionOperations reflectionOperations)
        {
            reflectionOperations.ClearCache();
        }
    }
}
