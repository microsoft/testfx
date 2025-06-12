// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
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

    // PERF: This was moved from Dictionary<MemberInfo, Dictionary<string, object>> to Concurrent<ICustomAttributeProvider, Attribute[]>
    // storing an array allows us to store multiple attributes of the same type if we find them. It also has lower memory footprint, and is faster
    // when we are going through the whole collection. Giving us overall better perf.
    private readonly ConcurrentDictionary<ICustomAttributeProvider, Attribute[]> _inheritedAttributeCache = [];
    private readonly ConcurrentDictionary<ICustomAttributeProvider, Attribute[]> _nonInheritedAttributeCache = [];

    public static ReflectHelper Instance => InstanceValue.Value;

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it. e.g. [MyTestClass] from [TestClass] will match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="memberInfo">Member to inspect for attributes.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public virtual /* for testing */ bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        Guard.NotNull(memberInfo);

        // Get all attributes on the member.
        Attribute[] attributes = GetCustomAttributesCached(memberInfo, inherit);

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
    [Obsolete]
#endif
    public override object InitializeLifetimeService() => null!;

    /// <summary>
    /// Gets first attribute that matches the type or is derived from it.
    /// Use this together with attribute that does not allow multiple. In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <param name="inherit">If we should inspect parents of this type.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    public virtual /* for tests, for moq */ TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
        where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider, inherit);

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
    /// Match return type of method.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="returnType">The return type to match.</param>
    /// <returns>True if there is a match.</returns>
    internal static bool MatchReturnType(MethodInfo method, Type returnType)
    {
        Guard.NotNull(method);
        Guard.NotNull(returnType);
        return method.ReturnType.Equals(returnType);
    }

    /// <summary>
    /// Returns true when the method is declared in the assembly where the type is declared.
    /// </summary>
    /// <param name="method">The method to check for.</param>
    /// <param name="type">The type declared in the assembly to check.</param>
    /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
    internal virtual bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => method.DeclaringType!.Assembly.Equals(type.Assembly); // TODO: Investigate if we rely on NRE

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    internal virtual /* for tests, we are mocking this */ string[] GetTestCategories(MemberInfo categoryAttributeProvider, Type owningType)
    {
        IEnumerable<TestCategoryBaseAttribute> methodCategories = GetAttributes<TestCategoryBaseAttribute>(categoryAttributeProvider, inherit: true);
        IEnumerable<TestCategoryBaseAttribute> typeCategories = GetAttributes<TestCategoryBaseAttribute>(owningType, inherit: true);
        IEnumerable<TestCategoryBaseAttribute> assemblyCategories = GetAttributes<TestCategoryBaseAttribute>(owningType.Assembly, inherit: true);

        return [.. methodCategories.Concat(typeCategories).Concat(assemblyCategories).SelectMany(c => c.TestCategories)];
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
    /// Gets the test id generation strategy set on an assembly.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    internal static TestIdGenerationStrategy GetTestIdGenerationStrategy(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(TestIdGenerationStrategyAttribute))
            .OfType<TestIdGenerationStrategyAttribute>()
            .FirstOrDefault()?.Strategy ?? TestIdGenerationStrategy.FullyQualified;

    /// <summary>
    /// Gets discover internals assembly level attribute.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    internal static DiscoverInternalsAttribute? GetDiscoverInternalsAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(DiscoverInternalsAttribute))
            .OfType<DiscoverInternalsAttribute>()
            .FirstOrDefault();

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
    /// Get the parallelization behavior for a test method.
    /// </summary>
    /// <param name="testMethod">Test method.</param>
    /// <param name="owningType">The type that owns <paramref name="testMethod"/>.</param>
    /// <returns>True if test method should not run in parallel.</returns>
    internal bool IsDoNotParallelizeSet(MemberInfo testMethod, Type owningType)
        => IsAttributeDefined<DoNotParallelizeAttribute>(testMethod, inherit: true)
        || IsAttributeDefined<DoNotParallelizeAttribute>(owningType, inherit: true);

    /// <summary>
    /// Get the parallelization behavior for a test assembly.
    /// </summary>
    /// <param name="assembly">The test assembly.</param>
    /// <returns>True if test assembly should not run in parallel.</returns>
    internal static bool IsDoNotParallelizeSet(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(DoNotParallelizeAttribute))
            .Length != 0;

    /// <summary>
    /// Gets the class cleanup lifecycle set on an assembly.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    /// <returns> The class cleanup lifecycle attribute if set. null otherwise. </returns>
    internal static ClassCleanupExecutionAttribute? GetClassCleanupAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(ClassCleanupExecutionAttribute))
            .OfType<ClassCleanupExecutionAttribute>()
            .FirstOrDefault();

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="priorityAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal virtual int? GetPriority(MemberInfo priorityAttributeProvider) =>
        GetFirstAttributeOrDefault<PriorityAttribute>(priorityAttributeProvider, inherit: true)?.Priority;

    /// <summary>
    /// Gets the class cleanup lifecycle for the class, if set.
    /// </summary>
    /// <param name="classInfo">The class to inspect.</param>
    /// <returns>Returns <see cref="ClassCleanupBehavior"/> if provided, otherwise <c>null</c>.</returns>
    internal virtual ClassCleanupBehavior? GetClassCleanupBehavior(TestClassInfo classInfo)
    {
        // TODO: not discovery related but seems expensive and unnecessary, because we do inheritance lookup, and to put the method into the stack we've already did this lookup before?
        if (!classInfo.HasExecutableCleanupMethod)
        {
            return null;
        }

        var cleanupBehaviors =
            new HashSet<ClassCleanupBehavior?>(
                classInfo.BaseClassCleanupMethods
                .Select(x => GetFirstAttributeOrDefault<ClassCleanupAttribute>(x, inherit: true)?.CleanupBehavior))
            {
                classInfo.ClassCleanupMethod == null ? null : GetFirstAttributeOrDefault<ClassCleanupAttribute>(classInfo.ClassCleanupMethod, inherit: true)?.CleanupBehavior,
            };

        return cleanupBehaviors.Contains(ClassCleanupBehavior.EndOfClass)
            ? ClassCleanupBehavior.EndOfClass
            : cleanupBehaviors.Contains(ClassCleanupBehavior.EndOfAssembly) ? ClassCleanupBehavior.EndOfAssembly : null;
    }

    /// <summary>
    /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
    /// </summary>
    /// <param name="testPropertyProvider">The member to inspect.</param>
    /// <returns>List of traits.</returns>
    internal virtual IEnumerable<Trait> GetTestPropertiesAsTraits(MemberInfo testPropertyProvider)
    {
        IEnumerable<TestPropertyAttribute> testPropertyAttributes = GetAttributes<TestPropertyAttribute>(testPropertyProvider, inherit: true);

        if (testPropertyProvider.DeclaringType is { } testClass)
        {
            testPropertyAttributes = testPropertyAttributes.Concat(GetAttributes<TestPropertyAttribute>(testClass, inherit: true));
        }

        foreach (TestPropertyAttribute testProperty in testPropertyAttributes)
        {
            var testPropertyPair = new Trait(testProperty.Name, testProperty.Value);
            yield return testPropertyPair;
        }
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>An instance of the attribute.</returns>
    internal virtual /* for tests, for moq */ IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider, bool inherit)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider, inherit);

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
    /// Gets and caches the attributes for the given type, or method.
    /// </summary>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>attributes defined.</returns>
    internal Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        // If the information is cached, then use it otherwise populate the cache using
        // the reflection APIs.
        return inherit
            ? _inheritedAttributeCache.GetOrAdd(attributeProvider, GetAttributesInheriting)
            : _nonInheritedAttributeCache.GetOrAdd(attributeProvider, GetAttributesNonInheriting);

        // We are avoiding func allocation here.
        static Attribute[] GetAttributesInheriting(ICustomAttributeProvider key)
            => GetAttributes(key, inherit: true);

        static Attribute[] GetAttributesNonInheriting(ICustomAttributeProvider key)
            => GetAttributes(key, inherit: false);

        static Attribute[] GetAttributes(ICustomAttributeProvider attributeProvider, bool inherit)
        {
            // Populate the cache
            try
            {
                object[]? attributes = NotCachedReflectionAccessor.GetCustomAttributesNotCached(attributeProvider, inherit);
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

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.FailedToGetCustomAttribute, attributeProvider.GetType().FullName!, description);

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
        /// <param name="inherit">If inherited type of attribute.</param>
        /// <returns>All attributes of give type on member.</returns>
        public static object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit)
        {
            object[] attributesArray = attributeProvider is MemberInfo memberInfo
                ? PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(memberInfo, inherit)
                : PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes((Assembly)attributeProvider, typeof(Attribute));

            return attributesArray; // TODO: Investigate if we rely on NRE
        }
    }

    internal /* for tests */ void ClearCache()
    {
        // Tests manipulate the platform reflection provider, and we end up caching different attributes than the class / method actually has.
        _inheritedAttributeCache.Clear();
        _nonInheritedAttributeCache.Clear();
    }
}
