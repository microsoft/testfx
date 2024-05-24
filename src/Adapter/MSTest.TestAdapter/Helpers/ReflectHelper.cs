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
    private static readonly Lazy<ReflectHelper> InstanceValue = new(() => new ReflectHelper());

    /// <summary>
    /// Contains the memberInfo Vs the name/type of the attributes defined on that member. (FYI: - MemberInfo denotes properties, fields, methods, events).
    /// </summary>
    private readonly Dictionary<MemberInfo, Dictionary<string, object>> _attributeCache = [];

    internal ReflectHelper()
    {
    }

    public static ReflectHelper Instance => InstanceValue.Value;

    /// <summary>
    /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="memberInfo">Member/Type to test.</param>
    /// <param name="inherit">Look through inheritance or not.</param>
    /// <returns>True if the attribute of the specified type is defined.</returns>
    public virtual bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        if (memberInfo == null)
        {
            throw new ArgumentNullException(nameof(memberInfo));
        }

        // Get attributes defined on the member from the cache.
        Dictionary<string, object>? attributes = GetAttributes(memberInfo, inherit);
        string requiredAttributeQualifiedName = typeof(TAttribute).AssemblyQualifiedName!;
        if (attributes == null)
        {
            // If we could not obtain all attributes from cache, just get the one we need.
            TAttribute[] specificAttributes = GetCustomAttributes<TAttribute>(memberInfo, inherit);
            return specificAttributes.Any(a => string.Equals(a.GetType().AssemblyQualifiedName, requiredAttributeQualifiedName, StringComparison.Ordinal));
        }

        return attributes.ContainsKey(requiredAttributeQualifiedName);
    }

    /// <summary>
    /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="type">Member/Type to test.</param>
    /// <param name="inherit">Look through inheritance or not.</param>
    /// <returns>True if the specified attribute is defined on the type.</returns>
    public virtual bool IsAttributeDefined<TAttribute>(Type type, bool inherit)
        where TAttribute : Attribute
        => IsAttributeDefined<TAttribute>((MemberInfo)type.GetTypeInfo(), inherit);

    /// <summary>
    /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="typeInfo">Type info to test.</param>
    /// <param name="inherit">Look through inheritance or not.</param>
    /// <returns>True if the specified attribute is defined on the type.</returns>
    public virtual bool IsAttributeDefined<TAttribute>(TypeInfo typeInfo, bool inherit)
        where TAttribute : Attribute
        => IsAttributeDefined<TAttribute>((MemberInfo)typeInfo, inherit);

    /// <summary>
    /// Returns true when specified class/member has attribute derived from specific attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The base attribute type.</typeparam>
    /// <param name="type">The type.</param>
    /// <param name="inherit">Should look at inheritance tree.</param>
    /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
    public virtual bool HasAttributeDerivedFrom<TAttribute>(Type type, bool inherit)
        where TAttribute : Attribute
        => HasAttributeDerivedFrom<TAttribute>((MemberInfo)type.GetTypeInfo(), inherit);

    /// <summary>
    /// Returns true when specified class/member has attribute derived from specific attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The base attribute type.</typeparam>
    /// <param name="typeInfo">The type info.</param>
    /// <param name="inherit">Should look at inheritance tree.</param>
    /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
    public virtual bool HasAttributeDerivedFrom<TAttribute>(TypeInfo typeInfo, bool inherit)
        where TAttribute : Attribute
        => HasAttributeDerivedFrom<TAttribute>((MemberInfo)typeInfo, inherit);

    /// <summary>
    /// Returns true when specified class/member has attribute derived from specific attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The base attribute type.</typeparam>
    /// <param name="memberInfo">The member info.</param>
    /// <param name="inherit">Should look at inheritance tree.</param>
    /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
    public bool HasAttributeDerivedFrom<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        if (memberInfo == null)
        {
            throw new ArgumentNullException(nameof(memberInfo));
        }

        // Get all attributes on the member.
        Dictionary<string, object>? attributes = GetAttributes(memberInfo, inherit);
        if (attributes == null)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"{nameof(ReflectHelper)}.{nameof(GetAttributes)}: {Resource.FailedFetchAttributeCache}");

            return IsAttributeDefined<TAttribute>(memberInfo, inherit);
        }

        // Try to find the attribute that is derived from baseAttrType.
        foreach (object attribute in attributes.Values)
        {
            DebugEx.Assert(attribute != null, $"{nameof(ReflectHelper)}.{nameof(GetAttributes)}: internal error: wrong value in the attributes dictionary.");

            Type attributeType = attribute.GetType();
            if (attributeType.IsSubclassOf(typeof(TAttribute)))
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
        ExpectedExceptionBaseAttribute[]? expectedExceptions;
        try
        {
            expectedExceptions = GetCustomAttributes<ExpectedExceptionBaseAttribute>(methodInfo, true);
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

        if (expectedExceptions == null || expectedExceptions.Length == 0)
        {
            return null;
        }

        // Verify that there is only one attribute (multiple attributes derived from
        // ExpectedExceptionBaseAttribute are not allowed on a test method)
        if (expectedExceptions.Length > 1)
        {
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_MultipleExpectedExceptionsOnTestMethod,
                testMethod.FullClassName,
                testMethod.Name);
            throw new TypeInspectionException(errorMessage);
        }

        // Set the expected exception attribute to use for this test
        ExpectedExceptionBaseAttribute expectedException = expectedExceptions[0];

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

    internal static TAttribute[]? GetAttributes<TAttribute>(MethodBase methodBase, bool inherit)
        where TAttribute : Attribute
    {
        TAttribute[]? attributeArray = GetCustomAttributes<TAttribute>(methodBase, inherit);
        return attributeArray == null || attributeArray.Length == 0
            ? null
            : attributeArray;
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
    /// Get custom attributes on a member for both normal and reflection only load.
    /// </summary>
    /// <typeparam name="TAttribute">Type of attribute to retrieve.</typeparam>
    /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    internal static TAttribute[]? GetCustomAttributes<TAttribute>(MemberInfo? memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        if (memberInfo == null)
        {
            return null;
        }

        object[] attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
            memberInfo,
            typeof(TAttribute),
            inherit);

        return attributesArray!.OfType<TAttribute>().ToArray(); // TODO: Investigate if we rely on NRE
    }

    /// <summary>
    /// Get custom attributes on a member for both normal and reflection only load.
    /// </summary>
    /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    internal static object[]? GetCustomAttributes(MemberInfo memberInfo, bool inherit)
    {
        if (memberInfo == null)
        {
            return null;
        }

        object[] attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
            memberInfo,
            inherit);

        return attributesArray!.ToArray(); // TODO: Investigate if we rely on NRE
    }

    /// <summary>
    /// Returns the first attribute of the specified type or null if no attribute
    /// of the specified type is set on the method.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to return.</typeparam>
    /// <param name="method">The method on which the attribute is defined.</param>
    /// <returns>The attribute or null if none exists.</returns>
    internal TAttribute? GetAttribute<TAttribute>(MethodInfo method)
        where TAttribute : Attribute
    {
        if (IsAttributeDefined<TAttribute>(method, false))
        {
            TAttribute[] attributes = GetCustomAttributes<TAttribute>(method, false);
            DebugEx.Assert(attributes.Length == 1, "Should only be one attribute.");
            return attributes[0];
        }

        return null;
    }

    /// <summary>
    /// Returns true when the method is declared in the assembly where the type is declared.
    /// </summary>
    /// <param name="method">The method to check for.</param>
    /// <param name="type">The type declared in the assembly to check.</param>
    /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
    internal virtual bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => method.DeclaringType!.Assembly.Equals(type.GetTypeInfo().Assembly); // TODO: Investigate if we rely on NRE

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    internal virtual string[] GetCategories(MemberInfo categoryAttributeProvider, Type owningType)
    {
        IEnumerable<object> categories = GetCustomAttributesRecursively(categoryAttributeProvider, owningType);
        List<string> testCategories = [];

        if (categories != null)
        {
            foreach (TestCategoryBaseAttribute category in categories.Cast<TestCategoryBaseAttribute>())
            {
                testCategories.AddRange(category.TestCategories);
            }
        }

        return testCategories.ToArray();
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
        => GetCustomAttributes<DoNotParallelizeAttribute>(testMethod).Length != 0
        || GetCustomAttributes<DoNotParallelizeAttribute>(owningType.GetTypeInfo()).Length != 0;

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
    /// Gets custom attributes at the class and assembly for a method.
    /// </summary>
    /// <param name="attributeProvider">Method Info or Member Info or a Type.</param>
    /// <param name="owningType">The type that owns <paramref name="attributeProvider"/>.</param>
    /// <returns>The categories of the specified type on the method. </returns>
    internal IEnumerable<object> GetCustomAttributesRecursively(MemberInfo attributeProvider, Type owningType)
    {
        TestCategoryBaseAttribute[]? categories = GetCustomAttributes<TestCategoryBaseAttribute>(attributeProvider);
        if (categories != null)
        {
            categories = categories.Concat(GetCustomAttributes<TestCategoryBaseAttribute>(owningType.GetTypeInfo())).ToArray();
        }

        if (categories != null)
        {
            categories = categories.Concat(GetCustomAttributeForAssembly<TestCategoryBaseAttribute>(owningType.GetTypeInfo())).ToArray();
        }

        return categories ?? Enumerable.Empty<object>();
    }

    /// <summary>
    /// Gets the custom attributes on the assembly of a member info
    /// NOTE: having it as separate virtual method, so that we can extend it for testing.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to find.</typeparam>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <returns>Custom attributes defined.</returns>
    internal virtual TAttribute[] GetCustomAttributeForAssembly<TAttribute>(MemberInfo memberInfo)
        where TAttribute : Attribute
        => PlatformServiceProvider.Instance.ReflectionOperations
            .GetCustomAttributes(memberInfo.Module.Assembly, typeof(TAttribute))
            !.OfType<TAttribute>()
            .ToArray();

    /// <summary>
    /// Gets the custom attributes of the provided type on a memberInfo.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <param name="attributeProvider"> The member to reflect on. </param>
    /// <returns>Attributes defined.</returns>
    internal virtual TAttribute[] GetCustomAttributes<TAttribute>(MemberInfo attributeProvider)
        where TAttribute : Attribute
        => GetCustomAttributes<TAttribute>(attributeProvider, true);

    /// <summary>
    /// Gets the first custom attribute of the provided type on a memberInfo.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <param name="attributeProvider"> The member to reflect on. </param>
    /// <returns>Attribute defined.</returns>
    internal virtual TAttribute? GetCustomAttribute<TAttribute>(MemberInfo attributeProvider)
        where TAttribute : Attribute
    {
        TAttribute[] attribute = GetCustomAttributes<TAttribute>(attributeProvider, true);

        return attribute == null || attribute.Length != 1
            ? null
            : attribute[0];
    }

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
    internal virtual int? GetPriority(MemberInfo priorityAttributeProvider)
    {
        PriorityAttribute[] priorityAttribute = GetCustomAttributes<PriorityAttribute>(priorityAttributeProvider, true);

        return priorityAttribute == null || priorityAttribute.Length != 1
            ? null
            : priorityAttribute[0].Priority;
    }

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="ignoreAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal virtual string? GetIgnoreMessage(MemberInfo ignoreAttributeProvider)
    {
        IgnoreAttribute[]? ignoreAttribute = GetCustomAttributes<IgnoreAttribute>(ignoreAttributeProvider, true);

        return ignoreAttribute is null || ignoreAttribute.Length == 0
            ? null
            : ignoreAttribute[0].IgnoreMessage;
    }

    /// <summary>
    /// Gets the class cleanup lifecycle for the class, if set.
    /// </summary>
    /// <param name="classInfo">The class to inspect.</param>
    /// <returns>Returns <see cref="ClassCleanupBehavior"/> if provided, otherwise <c>null</c>.</returns>
    internal virtual ClassCleanupBehavior? GetClassCleanupBehavior(TestClassInfo classInfo)
    {
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
        TestPropertyAttribute[] testPropertyAttributes = GetCustomAttributes<TestPropertyAttribute>(testPropertyProvider, true);

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
        Dictionary<string, object>? attributes = GetAttributes(memberInfo, inherit);
        if (attributes == null)
        {
            return null;
        }

        // Try to find the attribute that is derived from baseAttrType.
        foreach (object attribute in attributes.Values)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            Type attributeType = attribute.GetType();
            if (attributeType.Equals(typeof(TAttributeType)) || attributeType.IsSubclassOf(typeof(TAttributeType)))
            {
                return attribute as TAttributeType;
            }
        }

        return null;
    }

    /// <summary>
    /// Get attribute defined on a method which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="type">The type to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>An instance of the attribute.</returns>
    internal static TAttributeType? GetDerivedAttribute<TAttributeType>(Type type, bool inherit)
        where TAttributeType : Attribute
    {
        object[] attributes = GetCustomAttributes(type.GetTypeInfo(), inherit);
        if (attributes == null)
        {
            return null;
        }

        // Try to find the attribute that is derived from baseAttrType.
        foreach (object attribute in attributes)
        {
            DebugEx.Assert(attribute != null, "ReflectHelper.DefinesAttributeDerivedFrom: internal error: wrong value in the attributes dictionary.");

            Type attributeType = attribute.GetType();
            if (attributeType.Equals(typeof(TAttributeType)) || attributeType.IsSubclassOf(typeof(TAttributeType)))
            {
                return attribute as TAttributeType;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns owner if attribute is applied to TestMethod, else null.
    /// </summary>
    /// <param name="ownerAttributeProvider">The member to inspect.</param>
    /// <returns>owner if attribute is applied to TestMethod, else null.</returns>
    private static string? GetOwner(MemberInfo ownerAttributeProvider)
    {
        OwnerAttribute[] ownerAttribute = GetCustomAttributes<OwnerAttribute>(ownerAttributeProvider, true);

        return ownerAttribute == null || ownerAttribute.Length != 1
            ? null
            : ownerAttribute[0].Owner;
    }

    /// <summary>
    /// Get the Attributes (TypeName/TypeObject) for a given member.
    /// </summary>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>attributes defined.</returns>
    private Dictionary<string, object>? GetAttributes(MemberInfo memberInfo, bool inherit)
    {
        // If the information is cached, then use it otherwise populate the cache using
        // the reflection APIs.
        lock (_attributeCache)
        {
            if (_attributeCache.TryGetValue(memberInfo, out Dictionary<string, object>? attributes))
            {
                return attributes;
            }

            // Populate the cache
            attributes = [];

            object[]? customAttributesArray;
            try
            {
                customAttributesArray = GetCustomAttributes(memberInfo, inherit);
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

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.FailedToGetCustomAttribute, memberInfo.GetType().FullName!, description);

                // Since we cannot check by attribute names, do it in reflection way.
                // Note 1: this will not work for different version of assembly but it is better than nothing.
                // Note 2: we cannot cache this because we don't know if there are other attributes defined.
                return null;
            }

            DebugEx.Assert(customAttributesArray != null, "attributes should not be null.");

            foreach (object customAttribute in customAttributesArray)
            {
                Type attributeType = customAttribute.GetType();
                attributes[attributeType.AssemblyQualifiedName!] = customAttribute;
            }

            _attributeCache.Add(memberInfo, attributes);

            return attributes;
        }
    }
}
