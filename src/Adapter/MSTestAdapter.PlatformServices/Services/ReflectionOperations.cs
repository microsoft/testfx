// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
internal sealed class ReflectionOperations : MarshalByRefObject, IReflectionOperations
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags Everything = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    // PERF: This was moved from Dictionary<MemberInfo, Dictionary<string, object>> to Concurrent<ICustomAttributeProvider, Attribute[]>
    // storing an array allows us to store multiple attributes of the same type if we find them. It also has lower memory footprint, and is faster
    // when we are going through the whole collection. Giving us overall better perf.
    private readonly ConcurrentDictionary<ICustomAttributeProvider, Attribute[]> _attributeCache = [];

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
    {
        object[] attributes = memberInfo.GetCustomAttributes(typeof(Attribute), inherit: true);

        // Ensures that when the return of this method is used with GetCustomAttributesCached
        // then we are already Attribute[] to avoid LINQ Cast and extra array allocation.
        // This assert is solely for performance. Nothing "functional" will go wrong if the assert failed.
        Debug.Assert(attributes is Attribute[], $"Expected Attribute[], found '{attributes.GetType()}'.");
        return attributes;
    }

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type)
        => assembly.GetCustomAttributes(type, inherit: true);

#pragma warning disable IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
        => classType.GetConstructors(DeclaredOnlyLookup);

    public MethodInfo[] GetDeclaredMethods(Type classType)
        => classType.GetMethods(DeclaredOnlyLookup);

    public PropertyInfo[] GetDeclaredProperties(Type type)
        => type.GetProperties(DeclaredOnlyLookup);

    public Type[] GetDefinedTypes(Assembly assembly)
        => assembly.GetTypes();

    public MethodInfo[] GetRuntimeMethods(Type type)
        => type.GetMethods(Everything);

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
        => includeNonPublic
            ? declaringType.GetMethod(methodName, Everything, null, parameters, null)
            : declaringType.GetMethod(methodName, parameters);

    public PropertyInfo? GetRuntimeProperty(Type classType, string testContextPropertyName, bool includeNonPublic)
        => includeNonPublic
            ? classType.GetProperty(testContextPropertyName, Everything)
            : classType.GetProperty(testContextPropertyName);

    public Type? GetType(string typeName)
        => Type.GetType(typeName);

    public Type? GetType(Assembly assembly, string typeName)
        => assembly.GetType(typeName);

    public object? CreateInstance(Type type, object?[] parameters)
        => Activator.CreateInstance(type, parameters);
#pragma warning restore IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning restore IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it. e.g. [MyTestClass] from [TestClass] will match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="memberInfo">Member to inspect for attributes.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo)
        where TAttribute : Attribute
    {
        Ensure.NotNull(memberInfo);

        // Get all attributes on the member.
        Attribute[] attributes = GetCustomAttributesCached(memberInfo);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, $"{nameof(ReflectionOperations)}.{nameof(GetCustomAttributesCached)}: internal error: wrong value in the attributes dictionary.");

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
    [Obsolete]
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
    public TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
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
    public TAttribute? GetSingleAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
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
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>An instance of the attribute.</returns>
    public IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectionOperations.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            if (attribute is TAttributeType attributeAsAttributeType)
            {
                yield return attributeAsAttributeType;
            }
        }
    }

    /// <summary>
    /// Gets and caches the attributes for the given type, or method.
    /// </summary>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>attributes defined.</returns>
    public Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider)
    {
        // If the information is cached, then use it otherwise populate the cache using
        // the reflection APIs.
        return _attributeCache.GetOrAdd(attributeProvider, GetAttributes);

        // We are avoiding func allocation here.
        static Attribute[] GetAttributes(ICustomAttributeProvider attributeProvider)
        {
            // Populate the cache
            try
            {
                object[]? attributes = NotCachedReflectionAccessor.GetCustomAttributesNotCached(attributeProvider);
                return attributes is null ? [] : attributes as Attribute[] ?? [.. attributes.Cast<Attribute>()];
            }
            catch (Exception ex)
            {
                // Get the exception description
                string description;
                try
                {
                    // Can throw if the Message or StackTrace properties throw exceptions
                    description = ex.ToString();
                }
                catch (Exception ex2)
                {
                    description = string.Format(CultureInfo.CurrentCulture, Resource.ExceptionOccuredWhileGettingTheExceptionDescription, ex.GetType().FullName, ex2.GetType().FullName);                               // ex.GetType().FullName +
                }

                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(Resource.FailedToGetCustomAttribute, attributeProvider.GetType().FullName!, description);
                }

                return [];
            }
        }
    }

    /// <summary>
    /// Reflection helper that is accessing Reflection directly, and won't cache the results.
    /// </summary>
    internal static class NotCachedReflectionAccessor
    {
        /// <summary>
        /// Get custom attributes on a member without cache. Be CAREFUL where you use this, repeatedly accessing reflection without caching the results degrades the performance.
        /// </summary>
        /// <param name="attributeProvider">Member for which attributes needs to be retrieved.</param>
        /// <returns>All attributes of give type on member.</returns>
        public static object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider)
        {
            IReflectionOperations reflectionOperations = PlatformServiceProvider.Instance.ReflectionOperations;
            object[] attributesArray = attributeProvider is MemberInfo memberInfo
                ? reflectionOperations.GetCustomAttributes(memberInfo)
                : reflectionOperations.GetCustomAttributes((Assembly)attributeProvider, typeof(Attribute));

            return attributesArray; // TODO: Investigate if we rely on NRE
        }
    }

    internal /* for tests */ void ClearCache()
        // Tests manipulate the platform reflection provider, and we end up caching different attributes than the class / method actually has.
        => _attributeCache.Clear();
}
