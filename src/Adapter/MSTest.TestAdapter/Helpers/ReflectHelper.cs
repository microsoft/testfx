// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal class ReflectHelper : MarshalByRefObject
{
    // It is okay to use this api here, because we need to access the real reflection.
#pragma warning disable RS0030 // Do not use banned APIs
    private static readonly Lazy<ReflectHelper> InstanceValue = new(() => new ReflectHelper(new NotCachedReflectHelper()));
#pragma warning restore RS0030 // Do not use banned APIs

    // Caches below could be unified by using ICustomAttributeProvider. But the underlying IReflectionOperations is public and we would have to change or duplicate it.
    private readonly Dictionary<ICustomAttributeProvider, Attribute[]> _inheritedAttributeCache = [];
    private readonly Dictionary<ICustomAttributeProvider, Attribute[]> _nonInheritedAttributeCache = [];

    internal /* for tests only */ ReflectHelper(INotCachedReflectHelper notCachedReflectHelper) =>
        NotCachedReflectHelper = notCachedReflectHelper;

    private readonly AttributeComparer _attributeComparer = new();

    public static ReflectHelper Instance => InstanceValue.Value;

    public INotCachedReflectHelper NotCachedReflectHelper { get; }

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
        if (memberInfo == null)
        {
            throw new ArgumentNullException(nameof(memberInfo));
        }

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
    /// <param name="type">Type to test.</param>
    /// <param name="inherit">Inspect inheritance chain of the member or class. E.g. if parent class has this attribute defined.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    public bool IsDerivedAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        if (memberInfo == null)
        {
            throw new ArgumentNullException(nameof(memberInfo));
        }

        // Get all attributes on the member.
        Attribute[] attributes = GetCustomAttributesCached(memberInfo, inherit);
        if (attributes == null)
        {
            // TODO:
            bool a = true;
            if (a)
            {
                throw new NotSupportedException("THIS FALLBACK!");
            }

            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"{nameof(ReflectHelper)}.{nameof(GetCustomAttributesCached)}: {Resource.FailedFetchAttributeCache}");

            return IsNonDerivedAttributeDefined<TAttribute>(memberInfo, inherit);
        }

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
        IEnumerable<ExpectedExceptionBaseAttribute> expectedExceptions;
        try
        {
            expectedExceptions = GetDerivedAttributes<ExpectedExceptionBaseAttribute>(methodInfo, inherit: true);
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

        // TODO: we can probably do better if we grab the enumerator? 
        if (!expectedExceptions.Any())
        {
            return null;
        }

        // Verify that there is only one attribute (multiple attributes derived from
        // ExpectedExceptionBaseAttribute are not allowed on a test method)
        if (expectedExceptions.Count() > 1)
        {
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_MultipleExpectedExceptionsOnTestMethod,
                testMethod.FullClassName,
                testMethod.Name);
            throw new TypeInspectionException(errorMessage);
        }

        // Set the expected exception attribute to use for this test
        ExpectedExceptionBaseAttribute expectedException = expectedExceptions.First();

        return expectedException;
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

    public TAttribute? GetSingleNonDerivedAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit, bool nullOnMultiple)
        where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider, inherit);

        int count = 0;
        TAttribute? foundAttribute = default;
        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (AttributeComparer.IsNonDerived<TAttribute>(cachedAttribute))
            {
                foundAttribute = (TAttribute)cachedAttribute;
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        // We found what we were looking for.
        if (count == 1)
        {
            return foundAttribute;
        }

        return nullOnMultiple
            ? null
            : throw new InvalidOperationException($"Found {count} instances of attribute {typeof(TAttribute)} on class, but only single one was expected.");
    }

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

    public TAttribute? GetSingleDerivedAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit, bool nullOnMultiple)
    where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(attributeProvider, inherit);

        int count = 0;
        TAttribute? foundAttribute = default;
        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (AttributeComparer.IsDerived<TAttribute>(cachedAttribute))
            {
                foundAttribute = (TAttribute)cachedAttribute;
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        // We found what we were looking for.
        if (count == 1)
        {
            return foundAttribute;
        }

        return nullOnMultiple
            ? null
            : throw new InvalidOperationException($"Found {count} instances of attribute {typeof(TAttribute)} on class, but only single one was expected.");
    }

    public TAttribute? GetFirstDerivedAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
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
    internal static bool MatchReturnType(MethodInfo method, Type returnType) => method == null
            ? throw new ArgumentNullException(nameof(method))
            : returnType == null ? throw new ArgumentNullException(nameof(returnType)) : method.ReturnType.Equals(returnType);

    /// <summary>
    /// Returns true when the method is declared in the assembly where the type is declared.
    /// </summary>
    /// <param name="method">The method to check for.</param>
    /// <param name="type">The type declared in the assembly to check.</param>
    /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
    internal virtual bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => method.DeclaringType!.GetTypeInfo().Assembly.Equals(type.GetTypeInfo().Assembly); // TODO: Investigate if we rely on NRE

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    internal virtual string[] GetTestCategories(MemberInfo categoryAttributeProvider, Type owningType)
    {
        IEnumerable<TestCategoryBaseAttribute>? methodCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(categoryAttributeProvider, inherit: true);
        IEnumerable<TestCategoryBaseAttribute>? typeCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(owningType, inherit: true);
        IEnumerable<TestCategoryBaseAttribute>? assemblyCategories = GetDerivedAttributes<TestCategoryBaseAttribute>(owningType.Assembly, inherit: true);

        return methodCategories.Concat(typeCategories).Concat(assemblyCategories).SelectMany(c => c.TestCategories).ToArray();
    }

    /// <summary>
    /// Gets the parallelization level set on an assembly.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    /// <returns> The parallelization level if set. -1 otherwise. </returns>
    internal static ParallelizeAttribute? GetParallelizeAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(ParallelizeAttribute))
            !.OfType<ParallelizeAttribute>() // TODO: Investigate if we rely on NRE
            .FirstOrDefault();

    /// <summary>
    /// Get the parallelization behavior for a test method.
    /// </summary>
    /// <param name="testMethod">Test method.</param>
    /// <param name="owningType">The type that owns <paramref name="testMethod"/>.</param>
    /// <returns>True if test method should not run in parallel.</returns>
    internal bool IsDoNotParallelizeSet(MemberInfo testMethod, Type owningType)
        => IsDerivedAttributeDefined<DoNotParallelizeAttribute>(testMethod, inherit: false)
        || IsDerivedAttributeDefined<DoNotParallelizeAttribute>(owningType, inherit: false);

    /// <summary>
    /// Get the parallelization behavior for a test assembly.
    /// </summary>
    /// <param name="assembly">The test assembly.</param>
    /// <returns>True if test assembly should not run in parallel.</returns>
    internal static bool IsDoNotParallelizeSet(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(DoNotParallelizeAttribute))
            !.Length != 0; // TODO: Investigate if we rely on NRE

    /// <summary>
    /// Gets the class cleanup lifecycle set on an assembly.
    /// </summary>
    /// <param name="assembly"> The test assembly. </param>
    /// <returns> The class cleanup lifecycle attribute if set. null otherwise. </returns>
    internal static ClassCleanupExecutionAttribute? GetClassCleanupAttribute(Assembly assembly)
        => PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(assembly, typeof(ClassCleanupExecutionAttribute))
            !.OfType<ClassCleanupExecutionAttribute>() // TODO: Investigate if we rely on NRE
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
        GetSingleDerivedAttributeOrDefault<PriorityAttribute>(priorityAttributeProvider, inherit: true, nullOnMultiple: true)?.Priority;

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="ignoreAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal virtual string? GetIgnoreMessage(MemberInfo ignoreAttributeProvider) =>
        GetSingleDerivedAttributeOrDefault<IgnoreAttribute>(ignoreAttributeProvider, inherit: true, nullOnMultiple: true)?.IgnoreMessage;

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
                classInfo.BaseClassCleanupMethodsStack
                .Select(x => x.GetCustomAttribute<ClassCleanupAttribute>(true)?.CleanupBehavior))
            {
                classInfo.ClassCleanupMethod?.GetCustomAttribute<ClassCleanupAttribute>(true)?.CleanupBehavior,
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

        foreach (TestPropertyAttribute testProperty in testPropertyAttributes)
        {
            Trait testPropertyPair = testProperty.Name == null
                ? new Trait(string.Empty, testProperty.Value)
                : new Trait(testProperty.Name, testProperty.Value);
            yield return testPropertyPair;
        }
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>An instance of the attribute.</returns>
    internal TAttributeType? GetDerivedAttribute<TAttributeType>(MemberInfo memberInfo, bool inherit)
        where TAttributeType : Attribute
    {
        Attribute[] attributes = GetCustomAttributesCached(memberInfo, inherit);

        foreach (Attribute attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            if (AttributeComparer.IsDerived<TAttributeType>(attribute))
            {
                return (TAttributeType)attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>An instance of the attribute.</returns>
    internal IEnumerable<TAttributeType> GetDerivedAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider, bool inherit)
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
        OwnerAttribute? ownerAttribute = GetSingleDerivedAttributeOrDefault<OwnerAttribute>(ownerAttributeProvider, inherit: true, nullOnMultiple: true);

        return ownerAttribute?.Owner;
    }



    /// <summary>
    /// Gets and caches the attributes for the given type, or method.
    /// </summary>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>attributes defined.</returns>
    private Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        if (inherit)
        {
            lock (_inheritedAttributeCache)
            {
                return GetOrAddAttributes(_inheritedAttributeCache, attributeProvider, inherit: true);
            }
        }
        else
        {
            lock (_nonInheritedAttributeCache)
            {
                return GetOrAddAttributes(_nonInheritedAttributeCache, attributeProvider, inherit: false);
            }
        }

        // If the information is cached, then use it otherwise populate the cache using
        // the reflection APIs.
        Attribute[] GetOrAddAttributes(Dictionary<ICustomAttributeProvider, Attribute[]> cache, ICustomAttributeProvider attributeProvider, bool inherit)
        {
            if (cache.TryGetValue(attributeProvider, out Attribute[]? attributes))
            {
                return attributes;
            }

            // Populate the cache
            try
            {
                // This is where we get the data for our cache. It required to use call to Reflection here.
#pragma warning disable RS0030 // Do not use banned APIs
                attributes = NotCachedReflectHelper.GetCustomAttributesNotCached(attributeProvider, inherit)?.Cast<Attribute>().ToArray() ?? Array.Empty<Attribute>();
#pragma warning restore RS0030 // Do not use banned APIs
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

                // Since we cannot check by attribute names, do it in reflection way.
                // Note 1: this will not work for different version of assembly but it is better than nothing.
                // Note 2: we cannot cache this because we don't know if there are other attributes defined.

                // TODO: handle this instead polluting the api with null check, that we don't handle correctly in most places. This path is already expensive and this way we can at least keep it unified.
                return null;
            }

            DebugEx.Assert(attributes != null, "attributes should not be null.");

            cache.Add(attributeProvider, attributes);

            return attributes;
        }
    }

    internal IEnumerable<TAttribute>? GetNonDerivedAttributes<TAttribute>(MethodInfo methodInfo, bool inherit)
        where TAttribute : Attribute
    {
        Attribute[] cachedAttributes = GetCustomAttributesCached(methodInfo, inherit);

        foreach (Attribute cachedAttribute in cachedAttributes)
        {
            if (AttributeComparer.IsNonDerived<TAttribute>(cachedAttribute))
            {
                yield return (TAttribute)cachedAttribute;
            }
        }
    }
}

internal class AttributeComparer
{
    public static bool IsNonDerived<TAttribute>(Attribute attribute) =>
        attribute is TAttribute;

    public static bool IsDerived<TAttribute>(KeyValuePair<string, Attribute> cachedAttribute) =>
        IsDerived<TAttribute>(cachedAttribute.Value);

    public static bool IsDerived<TAttribute>(Attribute attribute)
    {
        Type attributeType = attribute.GetType();
        // IsSubclassOf returns false when the types are equal.
        return attributeType == typeof(TAttribute)
            || attributeType.IsSubclassOf(typeof(TAttribute));
    }
}

internal interface INotCachedReflectHelper
{
    object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit);
    TAttribute[]? GetCustomAttributesNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit);
    bool IsDerivedAttributeDefinedNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit);
}

/// <summary>
/// Reflection helper that is accessing Reflection directly, and won't cache the results. This is used internally by ReflectHelper.
/// Outside of ReflectHelper this should be used only to do the most basic checks on classes, and types, that will determine that a class or a method is NOT
/// part of the discovery or execution. E.g. checking that a class has TestClass attribute.
/// </summary>
internal class NotCachedReflectHelper : INotCachedReflectHelper
{
    /// <summary>
    /// Checks if an attribute of the given type, or and attribute inheriting from that type, is defined on the class or a method.
    /// Use this to check single attribute on a class or method to see if it is eligible for being a test.
    /// DO NOT use this repeatedly on a type or method that you already know is a test.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to check.</typeparam>
    public virtual bool IsDerivedAttributeDefinedNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        if (attributeProvider is MemberInfo memberInfo)
        {
            // This is cheaper than getting all the attributes and filtering them. This will return true for
            // classes that have [TestClass], and for methods that have [TestMethod].
            if (PlatformServiceProvider.Instance.ReflectionOperations.IsAttributeDefined(memberInfo, typeof(TAttribute), inherit))
            {
                return true;
            }
        }

        // This tries to find an attribute that derives from the given attribute e.g. [TestMethod].
        // This is more expensive than the check above.
        foreach (object attribute in GetCustomAttributesNotCached(attributeProvider, inherit))
        {
            if (AttributeComparer.IsDerived<TAttribute>((Attribute)attribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get custom attributes on a member without cache. 
    /// </summary>
    /// <param name="attributeProvider">Member for which attributes needs to be retrieved.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    [return: NotNullIfNotNull(nameof(attributeProvider))]
    public object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        if (attributeProvider == null)
        {
            return null;
        }

        object[] attributesArray;

        if (attributeProvider is MemberInfo memberInfo)
        {
            attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(memberInfo, inherit);
        }
        else
        {
            attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes((Assembly)attributeProvider, typeof(Attribute));
        }

        return attributesArray; // TODO: Investigate if we rely on NRE
    }

    /// <summary>
    /// Get custom attributes on a member without cache. 
    /// </summary>
    /// <param name="attributeProvider">Member for which attributes needs to be retrieved.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    [return: NotNullIfNotNull(nameof(attributeProvider))]
    public TAttribute[]? GetCustomAttributesNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        if (attributeProvider == null)
        {
            return null;
        }

        object[] attributesArray;

        if (attributeProvider is MemberInfo memberInfo)
        {
            attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(memberInfo, inherit);
        }
        else
        {
            attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes((Assembly)attributeProvider, typeof(Attribute));
        }

        return attributesArray!.OfType<TAttribute>().ToArray();
    }

}
