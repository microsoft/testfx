// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1822 // Mark members as static

using System.Collections.Concurrent;
using System.Diagnostics;
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
    // Cache empty attributes
    private static readonly Dictionary<string, object>? EmptyAttributes = new(0);

    private static readonly Lazy<ReflectHelper> InstanceValue = new(() => new ReflectHelper());

    /// <summary>
    /// Contains the memberInfo Vs the name/type of the attributes defined on that member. (FYI: - MemberInfo denotes properties, fields, methods, events).
    /// </summary>
    private static readonly ConcurrentDictionary<MemberInfo, Dictionary<string, object>> AttributeCache = [];

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
    public bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        Debug.Assert(condition: memberInfo != null, "MemberInfo should not be null");

        // Get attributes defined on the member from the cache.
        Dictionary<string, object>? attributes = GetAttributes(memberInfo!, inherit);
        if (attributes?.Count == 0)
        {
            return false;
        }

        if (attributes == null)
        {
            // If we could not obtain all attributes from cache, just get the one we need.
            var specificAttributes = GetCustomAttributes<TAttribute>(memberInfo, inherit);
            return specificAttributes.Any(a => string.Equals(a!.GetType().AssemblyQualifiedName, typeof(TAttribute).AssemblyQualifiedName!, StringComparison.Ordinal));
        }

        return attributes.ContainsKey(typeof(TAttribute).AssemblyQualifiedName!);
    }

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
        Debug.Assert(condition: memberInfo != null, "MethodInfo should be non-null");

        // Get all attributes on the member.
        Dictionary<string, object>? attributes = GetAttributes(memberInfo!, inherit);
        if (attributes?.Count == 0)
        {
            return false;
        }

        if (attributes == null)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"{nameof(ReflectHelper)}.{nameof(GetAttributes)}: {Resource.FailedFetchAttributeCache}");

            return IsAttributeDefined<TAttribute>(memberInfo!, inherit);
        }

        // Try to find the attribute that is derived from baseAttrType.
        foreach (object attribute in attributes.Values)
        {
            DebugEx.Assert(attribute != null, $"{nameof(ReflectHelper)}.{nameof(GetAttributes)}: internal error: wrong value in the attributes dictionary.");

            Type attributeType = attribute.GetType();
            if (attributeType.GetTypeInfo().IsSubclassOf(typeof(TAttribute)))
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
    public ExpectedExceptionBaseAttribute? ResolveExpectedExceptionHelper(MethodInfo methodInfo, TestMethod testMethod)
    {
        DebugEx.Assert(methodInfo != null, "MethodInfo should be non-null");

        // Get the expected exception attribute
        IEnumerable<ExpectedExceptionBaseAttribute>? expectedExceptions;
        try
        {
            expectedExceptions = GetCustomAttributes<ExpectedExceptionBaseAttribute>(methodInfo, true)!;
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

        if (expectedExceptions == null || !expectedExceptions.Any())
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

    internal IEnumerable<TAttribute>? GetAttributes<TAttribute>(MethodBase methodBase, bool inherit)
        where TAttribute : Attribute
    {
        IEnumerable<TAttribute>? attributeArray = GetCustomAttributes<TAttribute>(methodBase, inherit)!;
        return attributeArray == null || !attributeArray.Any()
            ? null
            : attributeArray;
    }

    /// <summary>
    /// Match return type of method.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="returnType">The return type to match.</param>
    /// <returns>True if there is a match.</returns>
    internal static bool MatchReturnType(MethodInfo method, Type returnType)
    {
        Debug.Assert(condition: method != null, "MethodInfo cannot be null");
        Debug.Assert(condition: returnType != null, "returnType cannot be null");
        return method!.ReturnType.Equals(returnType);
    }

    /// <summary>
    /// Get custom attributes on a member for both normal and reflection only load.
    /// </summary>
    /// <typeparam name="TAttribute">Type of attribute to retrieve.</typeparam>
    /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    internal IEnumerable<TAttribute?>? GetCustomAttributes<TAttribute>(MemberInfo? memberInfo, bool inherit)
        where TAttribute : Attribute
    {
        Debug.Assert(condition: memberInfo != null, "MemberInfo cannot be null");
        var attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
            memberInfo!,
            typeof(TAttribute),
            inherit);

        return attributesArray!.OfType<TAttribute>(); // TODO: Investigate if we rely on NRE
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
        Debug.Assert(condition: memberInfo != null, "MemberInfo cannot be null");

        var attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
            memberInfo!,
            inherit);

        return attributesArray;
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
            IEnumerable<TAttribute> attributes = GetCustomAttributes<TAttribute>(method, false)!;
            DebugEx.Assert(attributes.Count() == 1, "Should only be one attribute.");
            return attributes.First();
        }

        return null;
    }

    /// <summary>
    /// Returns true when the method is declared in the assembly where the type is declared.
    /// </summary>
    /// <param name="method">The method to check for.</param>
    /// <param name="type">The type declared in the assembly to check.</param>
    /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
    internal bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        => method.DeclaringType!.Assembly.Equals(type.Assembly); // TODO: Investigate if we rely on NRE

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    internal string[] GetCategories(MemberInfo categoryAttributeProvider, Type owningType)
    {
        var categories = GetCustomAttributesRecursively(categoryAttributeProvider, owningType);
        if (categories != null && categories.Any())
        {
            List<string> testCategories = new(categories.Count());
            foreach (TestCategoryBaseAttribute category in categories.Cast<TestCategoryBaseAttribute>())
            {
                testCategories.AddRange(category.TestCategories);
            }

            return testCategories.ToArray();
        }

        return Array.Empty<string>();
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
        => GetCustomAttributes<DoNotParallelizeAttribute>(testMethod).Any()
        || GetCustomAttributes<DoNotParallelizeAttribute>(owningType.GetTypeInfo()).Any();

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
        var categories = GetCustomAttributes<TestCategoryBaseAttribute>(attributeProvider);
        if (categories != null)
        {
            categories = categories.Concat(GetCustomAttributes<TestCategoryBaseAttribute>(owningType.GetTypeInfo()));
        }

        if (categories != null)
        {
            categories = categories.Concat(GetCustomAttributeForAssembly<TestCategoryBaseAttribute>(owningType.GetTypeInfo()));
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
    internal virtual /* for testing */ IEnumerable<TAttribute> GetCustomAttributeForAssembly<TAttribute>(MemberInfo memberInfo)
        where TAttribute : Attribute
        => PlatformServiceProvider.Instance.ReflectionOperations
            .GetCustomAttributes(memberInfo.Module.Assembly, typeof(TAttribute))
            !.OfType<TAttribute>();

    /// <summary>
    /// Gets the custom attributes of the provided type on a memberInfo.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <param name="attributeProvider"> The member to reflect on. </param>
    /// <returns>Attributes defined.</returns>
    internal virtual /* for testing */ IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(MemberInfo attributeProvider)
        where TAttribute : Attribute
        => GetCustomAttributes<TAttribute>(attributeProvider, true)!;

    /// <summary>
    /// Gets the first custom attribute of the provided type on a memberInfo.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type.</typeparam>
    /// <param name="attributeProvider"> The member to reflect on. </param>
    /// <returns>Attribute defined.</returns>
    internal TAttribute? GetCustomAttribute<TAttribute>(MemberInfo attributeProvider)
        where TAttribute : Attribute
    {
        var attribute = GetCustomAttributes<TAttribute>(attributeProvider, true);

        return attribute == null || !attribute.Any()
            ? null
            : attribute.First();
    }

    /// <summary>
    /// KeyValue pairs that are provided by TestOwnerAttribute of the given test method.
    /// </summary>
    /// <param name="ownerAttributeProvider">The member to inspect.</param>
    /// <returns>The owner trait.</returns>
    internal Trait? GetTestOwnerAsTraits(MemberInfo ownerAttributeProvider)
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
    internal Trait? GetTestPriorityAsTraits(int? testPriority) => testPriority == null
            ? null
            : new Trait("Priority", ((int)testPriority).ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="priorityAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal int? GetPriority(MemberInfo priorityAttributeProvider)
    {
        var priorityAttribute = GetCustomAttributes<PriorityAttribute>(priorityAttributeProvider, true);

        return priorityAttribute == null || !priorityAttribute.Any()
            ? null
            : priorityAttribute.First()!.Priority;
    }

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="ignoreAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    internal string? GetIgnoreMessage(MemberInfo ignoreAttributeProvider)
    {
        var ignoreAttribute = GetCustomAttributes<IgnoreAttribute>(ignoreAttributeProvider, true);

        return ignoreAttribute is null || !ignoreAttribute.Any()
            ? null
            : ignoreAttribute.First()!.IgnoreMessage;
    }

    /// <summary>
    /// Gets the class cleanup lifecycle for the class, if set.
    /// </summary>
    /// <param name="classInfo">The class to inspect.</param>
    /// <returns>Returns <see cref="ClassCleanupBehavior"/> if provided, otherwise <c>null</c>.</returns>
    internal ClassCleanupBehavior? GetClassCleanupBehavior(TestClassInfo classInfo)
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
    internal IEnumerable<Trait> GetTestPropertiesAsTraits(MemberInfo testPropertyProvider)
    {
        IEnumerable<TestPropertyAttribute?> testPropertyAttributes = GetCustomAttributes<TestPropertyAttribute>(testPropertyProvider, true)!;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        foreach (TestPropertyAttribute testProperty in testPropertyAttributes!)
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        {
            Trait testPropertyPair = testProperty!.Name == null
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
            if (attributeType.Equals(typeof(TAttributeType)) || attributeType.GetTypeInfo().IsSubclassOf(typeof(TAttributeType)))
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
    private string? GetOwner(MemberInfo ownerAttributeProvider)
    {
        var ownerAttribute = GetCustomAttributes<OwnerAttribute>(ownerAttributeProvider, true);

        return ownerAttribute == null || !ownerAttribute.Any()
            ? null
            : ownerAttribute.First()!.Owner;
    }

    /// <summary>
    /// Get the Attributes (TypeName/TypeObject) for a given member.
    /// </summary>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <param name="inherit">Look at inheritance chain.</param>
    /// <returns>attributes defined.</returns>
    private Dictionary<string, object>? GetAttributes(MemberInfo memberInfo, bool inherit)
    {
        if (AttributeCache.TryGetValue(memberInfo, out Dictionary<string, object>? attributes))
        {
            return attributes;
        }

        object[]? customAttributesArray = null;
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

        if (customAttributesArray.Length == 0)
        {
            AttributeCache.TryAdd(memberInfo, EmptyAttributes!);
            return EmptyAttributes!;
        }
        else
        {
            // Populate the cache
            attributes = new(customAttributesArray.Length);

            foreach (object customAttribute in customAttributesArray)
            {
                Type attributeType = customAttribute.GetType();
                attributes[attributeType.AssemblyQualifiedName!] = customAttribute;
            }

            AttributeCache.TryAdd(memberInfo, attributes);
            return attributes;
        }
    }
}
