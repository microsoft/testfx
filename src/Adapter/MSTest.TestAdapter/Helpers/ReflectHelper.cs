// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
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

    internal /* for tests only, because of Moq */ ReflectHelper()
    {
    }

    public static ReflectHelper Instance => InstanceValue.Value;

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute. The type is checked exactly. If attribute is derived (inherits from) a class, e.g. [MyTestClass] from [TestClass] it won't match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for by fully qualified name.</typeparam>
    /// <param name="memberInfo">Member/Type to test.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public virtual bool IsNonDerivedAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        Guard.NotNull(memberInfo);

        // Get attributes defined on the member from the cache.
        Attribute[] attributes = GetCustomAttributesCached(memberInfo, inherit);

        foreach (Attribute attribute in attributes)
        {
            if (AttributeComparer.IsNonDerived<TAttribute>(attribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute. The type is checked exactly. If attribute is derived (inherits from) a class, e.g. [MyTestClass] from [TestClass] it won't match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for by fully qualified name.</typeparam>
    /// <param name="type">Type to test.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public virtual bool IsNonDerivedAttributeDefined<TAttribute>(Type type, bool inherit)
        where TAttribute : Attribute
        => IsNonDerivedAttributeDefined<TAttribute>((MemberInfo)type, inherit);

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it. e.g. [MyTestClass] from [TestClass] will match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="type">Type to test.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public virtual bool IsDerivedAttributeDefined<TAttribute>(Type type, bool inherit)
        where TAttribute : Attribute
        => IsDerivedAttributeDefined<TAttribute>((MemberInfo)type, inherit);

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it. e.g. [MyTestClass] from [TestClass] will match if you look for [TestClass]. The inherit parameter does not impact this checking.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="memberInfo">Member to inspect for attributes.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public virtual /* for testing */ bool IsDerivedAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        Guard.NotNull(memberInfo);

        // Get all attributes on the member.
        Attribute[] attributes = GetCustomAttributesCached(memberInfo, inherit);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, $"{nameof(ReflectHelper)}.{nameof(GetCustomAttributesCached)}: internal error: wrong value in the attributes dictionary.");

            if (AttributeComparer.IsDerived<TAttribute>(attribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the expected exception attribute. The function will try to
    /// get all the expected exception attributes defined for a testMethod.
    /// </summary>
    /// <param name="methodInfo">The MethodInfo instance.</param>
    /// <param name="testMethod">The test method.</param>
    /// <returns>
    /// The expected exception attribute found for this test. Null if not found.
    /// </returns>
    public virtual ExpectedExceptionBaseAttribute? ResolveExpectedExceptionHelper(MethodInfo methodInfo, TestMethod testMethod)
    {
        DebugEx.Assert(methodInfo != null, "MethodInfo should be non-null");

        // Get the expected exception attribute
        ExpectedExceptionBaseAttribute? expectedException;
        try
        {
            expectedException = GetFirstDerivedAttributeOrDefault<ExpectedExceptionBaseAttribute>(methodInfo, inherit: true);
        }
        catch (Exception ex)
        {
            // If construction of the attribute throws an exception, indicate that there was an
            // error when trying to run the test
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ExpectedExceptionAttributeConstructionException,
                testMethod.FullClassName,
                testMethod.Name,
                ex.GetFormattedExceptionMessage());
            throw new TypeInspectionException(errorMessage);
        }

        return expectedException ?? null;
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
    /// Gets first attribute that matches the type (but is not derived from it). Use this together with attribute that does not allow multiple.
    /// In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <param name="inherit">If we should inspect parents of this type.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    public TAttribute? GetFirstNonDerivedAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
    where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider, inherit);

        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (AttributeComparer.IsNonDerived<TAttribute>(cachedAttribute))
            {
                return (TAttribute)cachedAttribute;
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
    /// <param name="inherit">If we should inspect parents of this type.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    public virtual /* for tests, for moq */ TAttribute? GetFirstDerivedAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
    where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider, inherit);

        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (AttributeComparer.IsDerived<TAttribute>(cachedAttribute))
            {
                return (TAttribute)cachedAttribute;
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
        IEnumerable<TestCategoryBaseAttribute> methodCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(categoryAttributeProvider, inherit: true);
        IEnumerable<TestCategoryBaseAttribute> typeCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(owningType, inherit: true);
        IEnumerable<TestCategoryBaseAttribute> assemblyCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(owningType.Assembly, inherit: true);

        return methodCategories.Concat(typeCategories).Concat(assemblyCategories).SelectMany(c => c.TestCategories).ToArray();
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
    [Obsolete]
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
        => IsDerivedAttributeDefined<DoNotParallelizeAttribute>(testMethod, inherit: true)
        || IsDerivedAttributeDefined<DoNotParallelizeAttribute>(owningType, inherit: true);

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
    /// KeyValue pairs that are provided by TestOwnerAttribute of the given test method.
    /// </summary>
    /// <param name="ownerAttributeProvider">The member to inspect.</param>
    /// <returns>The owner trait.</returns>
    internal virtual Trait? GetTestOwnerAsTraits(MemberInfo ownerAttributeProvider)
    {
        string? owner = GetOwner(ownerAttributeProvider);

        return StringEx.IsNullOrEmpty(owner)
            ? null
            : new Trait("Owner", owner);
    }

    /// <summary>
    /// KeyValue pairs that are provided by TestPriorityAttributes of the given test method.
    /// </summary>
    /// <param name="testPriority">The priority.</param>
    /// <returns>The corresponding trait.</returns>
    internal virtual Trait? GetTestPriorityAsTraits(int? testPriority) => testPriority == null
            ? null
            : new Trait("Priority", ((int)testPriority).ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="priorityAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal virtual int? GetPriority(MemberInfo priorityAttributeProvider) =>
        GetFirstDerivedAttributeOrDefault<PriorityAttribute>(priorityAttributeProvider, inherit: true)?.Priority;

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
                .Select(x => GetFirstDerivedAttributeOrDefault<ClassCleanupAttribute>(x, inherit: true)?.CleanupBehavior))
            {
                classInfo.ClassCleanupMethod == null ? null : GetFirstDerivedAttributeOrDefault<ClassCleanupAttribute>(classInfo.ClassCleanupMethod, inherit: true)?.CleanupBehavior,
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
        IEnumerable<TestPropertyAttribute> testPropertyAttributes = GetDerivedAttributes<TestPropertyAttribute>(testPropertyProvider, inherit: true);

        if (testPropertyProvider.DeclaringType is { } testClass)
        {
            testPropertyAttributes = testPropertyAttributes.Concat(GetDerivedAttributes<TestPropertyAttribute>(testClass, inherit: true));
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
    internal virtual /* for tests, for moq */ IEnumerable<TAttributeType> GetDerivedAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider, bool inherit)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(attributeProvider, inherit);

        // Try to find the attribute that is derived from baseAttrType.
        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            if (AttributeComparer.IsDerived<TAttributeType>(attribute))
            {
                yield return (TAttributeType)attribute;
            }
        }
    }

    /// <summary>
    /// Returns owner if attribute is applied to TestMethod, else null.
    /// </summary>
    /// <param name="ownerAttributeProvider">The member to inspect.</param>
    /// <returns>owner if attribute is applied to TestMethod, else null.</returns>
    private string? GetOwner(MemberInfo ownerAttributeProvider)
    {
        OwnerAttribute? ownerAttribute = GetFirstDerivedAttributeOrDefault<OwnerAttribute>(ownerAttributeProvider, inherit: true);

        return ownerAttribute?.Owner;
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
                return attributes is null ? [] : attributes as Attribute[] ?? attributes.Cast<Attribute>().ToArray();
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
