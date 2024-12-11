// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;

using Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines type cache which reflects upon a type and cache its test artifacts.
/// </summary>
internal sealed class TypeCache : MarshalByRefObject
{
    /// <summary>
    /// Test context property name.
    /// </summary>
    private const string TestContextPropertyName = "TestContext";

    /// <summary>
    /// Predefined test Attribute names.
    /// </summary>
    private static readonly string[] PredefinedNames = ["Priority", "TestCategory", "Owner"];

    /// <summary>
    /// Helper for reflection API's.
    /// </summary>
    private readonly ReflectHelper _reflectionHelper;

    /// <summary>
    /// Assembly info cache.
    /// </summary>
    private readonly ConcurrentDictionary<Assembly, TestAssemblyInfo> _testAssemblyInfoCache = new();

    /// <summary>
    /// ClassInfo cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, TestClassInfo> _classInfoCache = new();

    private readonly ConcurrentDictionary<string, bool> _discoverInternalsCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    internal TypeCache()
        : this(ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    /// <param name="reflectionHelper"> An instance to the <see cref="ReflectHelper"/> object. </param>
    internal TypeCache(ReflectHelper reflectionHelper) => _reflectionHelper = reflectionHelper;

    /// <summary>
    /// Gets Class Info cache which has cleanup methods to execute.
    /// </summary>
    public IEnumerable<TestClassInfo> ClassInfoListWithExecutableCleanupMethods
        => _classInfoCache.Values.Where(classInfo => classInfo.HasExecutableCleanupMethod);

    /// <summary>
    /// Gets Assembly Info cache which has cleanup methods to execute.
    /// </summary>
    public IEnumerable<TestAssemblyInfo> AssemblyInfoListWithExecutableCleanupMethods
        => _testAssemblyInfoCache.Values.Where(assemblyInfo => assemblyInfo.HasExecutableCleanupMethod);

    /// <summary>
    /// Gets the set of cached assembly info values.
    /// </summary>
    public ICollection<TestAssemblyInfo> AssemblyInfoCache => _testAssemblyInfoCache.Values;

    /// <summary>
    /// Gets the set of cached class info values.
    /// </summary>
    public ICollection<TestClassInfo> ClassInfoCache => _classInfoCache.Values;

    /// <summary>
    /// Get the test method info corresponding to the parameter test Element.
    /// </summary>
    /// <returns> The <see cref="TestMethodInfo"/>. </returns>
    public TestMethodInfo? GetTestMethodInfo(TestMethod testMethod, ITestContext testContext, bool captureDebugTraces)
    {
        Guard.NotNull(testMethod);
        Guard.NotNull(testContext);

        // Get the classInfo (This may throw as GetType calls assembly.GetType(..,true);)
        TestClassInfo? testClassInfo = GetClassInfo(testMethod);

        if (testClassInfo == null)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result.
            return null;
        }

        // Get the testMethod
        return ResolveTestMethodInfo(testMethod, testClassInfo, testContext, captureDebugTraces);
    }

    /// <summary>
    /// Get the test method info corresponding to the parameter test Element.
    /// </summary>
    /// <returns> The <see cref="TestMethodInfo"/>. </returns>
    public TestMethodInfo? GetTestMethodInfoForDiscovery(TestMethod testMethod)
    {
        Guard.NotNull(testMethod);

        // Get the classInfo (This may throw as GetType calls assembly.GetType(..,true);)
        TestClassInfo? testClassInfo = GetClassInfo(testMethod);

        if (testClassInfo == null)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result.
            return null;
        }

        // Get the testMethod
        return ResolveTestMethodInfoForDiscovery(testMethod, testClassInfo);
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

    #region ClassInfo creation and cache logic.

    /// <summary>
    /// Gets the classInfo corresponding to the unit test.
    /// </summary>
    /// <param name="testMethod"> The test Method.  </param>
    /// <returns> The <see cref="TestClassInfo"/>. </returns>
    private TestClassInfo? GetClassInfo(TestMethod testMethod)
    {
        DebugEx.Assert(testMethod != null, "test method is null");

        string typeName = testMethod.FullClassName;

        if (!_classInfoCache.TryGetValue(typeName, out TestClassInfo? classInfo))
        {
            // Load the class type
            Type? type = LoadType(typeName, testMethod.AssemblyName);

            // VSTest managed feature is not working properly and ends up providing names that are not fully
            // unescaped causing reflection to fail loading. For the cases we know this is happening, we will
            // try to manually unescape the type name and load the type again.
            if (type == null
                && TryGetUnescapedManagedTypeName(testMethod, out string? unescapedTypeName))
            {
                type = LoadType(unescapedTypeName, testMethod.AssemblyName);
            }

            if (type == null)
            {
                // This means the class containing the test method could not be found.
                // Return null so we return a not found result.
                return null;
            }

            // Get the classInfo
            classInfo = CreateClassInfo(type, testMethod);

            // Use the full type name for the cache.
            classInfo = _classInfoCache.GetOrAdd(typeName, classInfo);
        }

        return classInfo;
    }

    private static bool TryGetUnescapedManagedTypeName(TestMethod testMethod, [NotNullWhen(true)] out string? unescapedTypeName)
    {
        if (testMethod.Hierarchy.Count != 4)
        {
            unescapedTypeName = null;
            return false;
        }

        StringBuilder unescapedTypeNameBuilder = new();
        int i = -1;
        foreach (string? hierarchyPart in testMethod.Hierarchy)
        {
            i++;
            if (i is not 1 and not 2 || hierarchyPart is null)
            {
                continue;
            }

#if NETCOREAPP || WINDOWS_UWP
            if (hierarchyPart.StartsWith('\'') && hierarchyPart.EndsWith('\''))
#else
            if (hierarchyPart.StartsWith("'", StringComparison.Ordinal) && hierarchyPart.EndsWith("'", StringComparison.Ordinal))
#endif
            {
                unescapedTypeNameBuilder.Append(hierarchyPart, 1, hierarchyPart.Length - 2);
            }
            else
            {
                unescapedTypeNameBuilder.Append(hierarchyPart);
            }

            if (i == 1)
            {
                unescapedTypeNameBuilder.Append('.');
            }
        }

        unescapedTypeName = unescapedTypeNameBuilder.ToString();
        return unescapedTypeName != testMethod.FullClassName;
    }

    /// <summary>
    /// Loads the parameter type from the parameter assembly.
    /// </summary>
    /// <param name="typeName"> The type Name. </param>
    /// <param name="assemblyName"> The assembly Name. </param>
    /// <returns> The <see cref="Type"/>. </returns>
    /// <exception cref="TypeInspectionException"> Thrown when there is a type load exception from the assembly. </exception>
    private static Type? LoadType(string typeName, string assemblyName)
    {
        try
        {
            // Attempt to load the assembly using the full type name (includes assembly)
            // This call will load the assembly from the first location it is
            // found in (i.e. GAC, current directory, path)
            // If this fails, we will try to load the type from the assembly
            // location in the Out directory.
            Type? t = PlatformServiceProvider.Instance.ReflectionOperations.GetType(typeName);

            if (t == null)
            {
                Assembly assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyName, isReflectionOnly: false);

                // Attempt to load the type from the test assembly.
                // Allow this call to throw if the type can't be loaded.
                t = PlatformServiceProvider.Instance.ReflectionOperations.GetType(assembly, typeName);
            }

            return t;
        }
        catch (TypeLoadException)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result.
            return null;
        }
        catch (Exception ex)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TypeLoadError, typeName, ex);
            throw new TypeInspectionException(message);
        }
    }

    /// <summary>
    /// Create the class Info.
    /// </summary>
    /// <param name="classType"> The class Type. </param>
    /// <param name="testMethod"> The test Method. </param>
    /// <returns> The <see cref="TestClassInfo"/>. </returns>
    private TestClassInfo CreateClassInfo(Type classType, TestMethod testMethod)
    {
        ConstructorInfo[] constructors = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredConstructors(classType);
        (ConstructorInfo CtorInfo, bool IsParameterless)? selectedConstructor = null;

        foreach (ConstructorInfo ctor in constructors)
        {
            if (!ctor.IsPublic)
            {
                continue;
            }

            ParameterInfo[] parameters = ctor.GetParameters();

            // There are just 2 ctor shapes that we know, so the code is quite simple,
            // but if we add more, add a priority to the search, and short-circuit this search so we only iterate
            // through the collection once, to avoid re-allocating GetParameters multiple times.
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(TestContext))
            {
                selectedConstructor = (ctor, IsParameterless: false);

                // This is the preferred constructor, no point in searching for more.
                break;
            }

            if (parameters.Length == 0)
            {
                // Otherwise take the first parameterless constructor we can find.
                selectedConstructor ??= (ctor, IsParameterless: true);
            }
        }

        if (selectedConstructor is null)
        {
            throw new TypeInspectionException(string.Format(CultureInfo.CurrentCulture, Resource.UTA_NoValidConstructor, testMethod.FullClassName));
        }

        ConstructorInfo constructor = selectedConstructor.Value.CtorInfo;
        bool isParameterLessConstructor = selectedConstructor.Value.IsParameterless;

        PropertyInfo? testContextProperty = ResolveTestContext(classType);

        TestAssemblyInfo assemblyInfo = GetAssemblyInfo(classType);

        TestClassAttribute? testClassAttribute = ReflectHelper.Instance.GetFirstDerivedAttributeOrDefault<TestClassAttribute>(classType, inherit: false);
        DebugEx.Assert(testClassAttribute is not null, "testClassAttribute is null");
        var classInfo = new TestClassInfo(classType, constructor, isParameterLessConstructor, testContextProperty, testClassAttribute, assemblyInfo);

        // List holding the instance of the initialize/cleanup methods
        // to be passed into the tuples' queue  when updating the class info.
        var initAndCleanupMethods = new MethodInfo?[2];

        // List of instance methods present in the type as well its base type
        // which is used to decide whether TestInitialize/TestCleanup methods
        // present in the base type should be used or not. They are not used if
        // the method is overridden in the derived type.
        var instanceMethods = new Dictionary<string, string?>();

        foreach (MethodInfo methodInfo in PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(classType))
        {
            UpdateInfoIfTestInitializeOrCleanupMethod(classInfo, methodInfo, false, instanceMethods);

            UpdateInfoIfClassInitializeOrCleanupMethod(classInfo, methodInfo, false, ref initAndCleanupMethods);
        }

        Type? baseType = classType.BaseType;
        // PERF: Don't inspect object, no test methods or setups can be defined on it.
        while (baseType != null && baseType != typeof(object))
        {
            foreach (MethodInfo methodInfo in PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(baseType))
            {
                if (methodInfo is { IsPublic: true, IsStatic: false })
                {
                    // Update test initialize/cleanup method from base type.
                    UpdateInfoIfTestInitializeOrCleanupMethod(classInfo, methodInfo, true, instanceMethods);
                }

                if (methodInfo is { IsPublic: true, IsStatic: true })
                {
                    UpdateInfoIfClassInitializeOrCleanupMethod(classInfo, methodInfo, true, ref initAndCleanupMethods);
                }
            }

            UpdateInfoWithInitializeAndCleanupMethods(classInfo, ref initAndCleanupMethods);
            baseType = baseType.BaseType;
        }

        return classInfo;
    }

    /// <summary>
    /// Resolves the test context property.
    /// </summary>
    /// <param name="classType"> The class Type. </param>
    /// <returns> The <see cref="PropertyInfo"/> for TestContext property. Null if not defined. </returns>
    private static PropertyInfo? ResolveTestContext(Type classType)
    {
        try
        {
            PropertyInfo? testContextProperty = PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeProperty(classType, TestContextPropertyName);
            if (testContextProperty == null)
            {
                // that's okay may be the property was not defined
                return null;
            }

            // check if testContextProperty is of correct type
            if (!string.Equals(testContextProperty.PropertyType.FullName, typeof(TestContext).FullName, StringComparison.Ordinal))
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextTypeMismatchLoadError, classType.FullName);
                throw new TypeInspectionException(errorMessage);
            }

            return testContextProperty;
        }
        catch (AmbiguousMatchException ex)
        {
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextLoadError, classType.FullName, ex.Message);
            throw new TypeInspectionException(errorMessage);
        }
    }

    #endregion

    #region AssemblyInfo creation and cache logic.

    private TimeoutInfo? TryGetTimeoutInfo(MethodInfo methodInfo, FixtureKind fixtureKind)
    {
        TimeoutAttribute? timeoutAttribute = _reflectionHelper.GetFirstNonDerivedAttributeOrDefault<TimeoutAttribute>(methodInfo, inherit: false);
        if (timeoutAttribute != null)
        {
            if (!timeoutAttribute.HasCorrectTimeout)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, methodInfo.DeclaringType!.FullName, methodInfo.Name);
                throw new TypeInspectionException(message);
            }

            return TimeoutInfo.FromTimeoutAttribute(timeoutAttribute);
        }

        var globalTimeout = TimeoutInfo.FromFixtureSettings(fixtureKind);
        return globalTimeout.Timeout > 0
            ? globalTimeout
            : null;
    }

    /// <summary>
    /// Get the assembly info for the parameter type.
    /// </summary>
    /// <param name="type"> The type. </param>
    /// <returns> The <see cref="TestAssemblyInfo"/> instance. </returns>
    private TestAssemblyInfo GetAssemblyInfo(Type type)
    {
        Assembly assembly = type.Assembly;

        if (_testAssemblyInfoCache.TryGetValue(assembly, out TestAssemblyInfo? assemblyInfo))
        {
            return assemblyInfo;
        }

        assemblyInfo = new TestAssemblyInfo(assembly);

        Type[] types = AssemblyEnumerator.GetTypes(assembly, assembly.FullName!, null);

        foreach (Type t in types)
        {
            try
            {
                // Only examine classes which are TestClass or derives from TestClass attribute
                if (!_reflectionHelper.IsDerivedAttributeDefined<TestClassAttribute>(t, inherit: true))
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                // If we fail to discover type from an assembly, then do not abort. Pick the next type.
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(
                    "TypeCache: Exception occurred while checking whether type {0} is a test class or not. {1}",
                    t.FullName,
                    ex);

                continue;
            }

            // Enumerate through all methods and identify the Assembly Init and cleanup methods.
            foreach (MethodInfo methodInfo in PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(t))
            {
                if (IsAssemblyOrClassInitializeMethod<AssemblyInitializeAttribute>(methodInfo))
                {
                    assemblyInfo.AssemblyInitializeMethod = methodInfo;
                    assemblyInfo.AssemblyInitializeMethodTimeoutMilliseconds = TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyInitialize);
                }
                else if (IsAssemblyOrClassCleanupMethod<AssemblyCleanupAttribute>(methodInfo))
                {
                    assemblyInfo.AssemblyCleanupMethod = methodInfo;
                    assemblyInfo.AssemblyCleanupMethodTimeoutMilliseconds = TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyCleanup);
                }
            }
        }

        assemblyInfo = _testAssemblyInfoCache.GetOrAdd(assembly, assemblyInfo);

        return assemblyInfo;
    }

    /// <summary>
    /// Verify if a given method is an Assembly or Class Initialize method.
    /// </summary>
    /// <typeparam name="TInitializeAttribute">The initialization attribute type. </typeparam>
    /// <param name="methodInfo"> The method info. </param>
    /// <returns> True if its an initialization method. </returns>
    private bool IsAssemblyOrClassInitializeMethod<TInitializeAttribute>(MethodInfo methodInfo)
        where TInitializeAttribute : Attribute
    {
        // TODO: this would be inconsistent with the codebase, but potential perf gain, issue: https://github.com/microsoft/testfx/issues/2999
        // if (!methodInfo.IsStatic)
        // {
        //    return false;
        // }
        if (!_reflectionHelper.IsNonDerivedAttributeDefined<TInitializeAttribute>(methodInfo, false))
        {
            return false;
        }

        if (!methodInfo.HasCorrectClassOrAssemblyInitializeSignature())
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ClassOrAssemblyInitializeMethodHasWrongSignature, methodInfo.DeclaringType!.FullName, methodInfo.Name);
            throw new TypeInspectionException(message);
        }

        return true;
    }

    /// <summary>
    /// Verify if a given method is an Assembly or Class cleanup method.
    /// </summary>
    /// <typeparam name="TCleanupAttribute">The cleanup attribute type.</typeparam>
    /// <param name="methodInfo"> The method info. </param>
    /// <returns> True if its a cleanup method. </returns>
    private bool IsAssemblyOrClassCleanupMethod<TCleanupAttribute>(MethodInfo methodInfo)
        where TCleanupAttribute : Attribute
    {
        // TODO: this would be inconsistent with the codebase, but potential perf gain, issue: https://github.com/microsoft/testfx/issues/2999
        // if (!methodInfo.IsStatic)
        // {
        //    return false;
        // }
        if (!_reflectionHelper.IsNonDerivedAttributeDefined<TCleanupAttribute>(methodInfo, false))
        {
            return false;
        }

        if (!methodInfo.HasCorrectClassOrAssemblyCleanupSignature())
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ClassOrAssemblyCleanupMethodHasWrongSignature, methodInfo.DeclaringType!.FullName, methodInfo.Name);
            throw new TypeInspectionException(message);
        }

        return true;
    }

    #endregion

    /// <summary>
    /// Update the classInfo with given initialize and cleanup methods.
    /// </summary>
    /// <param name="classInfo"> The Class Info. </param>
    /// <param name="initAndCleanupMethods"> An array with the Initialize and Cleanup Methods Info. </param>
    private static void UpdateInfoWithInitializeAndCleanupMethods(
        TestClassInfo classInfo,
        ref MethodInfo?[] initAndCleanupMethods)
    {
        DebugEx.Assert(initAndCleanupMethods.Length == 2, "initAndCleanupMethods.Length == 2");

        MethodInfo? initMethod = initAndCleanupMethods[0];
        MethodInfo? cleanupMethod = initAndCleanupMethods[1];

        if (initMethod is not null)
        {
            classInfo.BaseClassInitMethods.Add(initMethod);
        }

        if (cleanupMethod is not null)
        {
            classInfo.BaseClassCleanupMethods.Add(cleanupMethod);

#pragma warning disable CS0618 // Type or member is obsolete
            classInfo.BaseClassCleanupMethodsStack.Push(cleanupMethod);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (initMethod is not null || cleanupMethod is not null)
        {
#pragma warning disable CS0618 // Type or member is obsolete - kept in case someone is using it
            classInfo.BaseClassInitAndCleanupMethods.Enqueue(
                new Tuple<MethodInfo?, MethodInfo?>(
                    initMethod,
                    initAndCleanupMethods.LastOrDefault()));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        initAndCleanupMethods = new MethodInfo[2];
    }

    /// <summary>
    /// Update the classInfo if the parameter method is a classInitialize/cleanup method.
    /// </summary>
    /// <param name="classInfo"> The Class Info. </param>
    /// <param name="methodInfo"> The Method Info. </param>
    /// <param name="isBase"> Flag to check whether base class needs to be validated. </param>
    /// <param name="initAndCleanupMethods"> An array with Initialize/Cleanup methods. </param>
    private void UpdateInfoIfClassInitializeOrCleanupMethod(
        TestClassInfo classInfo,
        MethodInfo methodInfo,
        bool isBase,
        ref MethodInfo?[] initAndCleanupMethods)
    {
        bool isInitializeMethod = IsAssemblyOrClassInitializeMethod<ClassInitializeAttribute>(methodInfo);
        bool isCleanupMethod = IsAssemblyOrClassCleanupMethod<ClassCleanupAttribute>(methodInfo);

        if (isInitializeMethod)
        {
            if (TryGetTimeoutInfo(methodInfo, FixtureKind.ClassInitialize) is { } timeoutInfo)
            {
                classInfo.ClassInitializeMethodTimeoutMilliseconds.Add(methodInfo, timeoutInfo);
            }

            if (isBase)
            {
                if (_reflectionHelper.GetFirstDerivedAttributeOrDefault<ClassInitializeAttribute>(methodInfo, inherit: true)?
                        .InheritanceBehavior == InheritanceBehavior.BeforeEachDerivedClass)
                {
                    initAndCleanupMethods[0] = methodInfo;
                }
            }
            else
            {
                // update class initialize method
                classInfo.ClassInitializeMethod = methodInfo;
            }
        }

        if (isCleanupMethod)
        {
            if (TryGetTimeoutInfo(methodInfo, FixtureKind.ClassCleanup) is { } timeoutInfo)
            {
                classInfo.ClassCleanupMethodTimeoutMilliseconds.Add(methodInfo, timeoutInfo);
            }

            if (isBase)
            {
                if (_reflectionHelper.GetFirstDerivedAttributeOrDefault<ClassCleanupAttribute>(methodInfo, inherit: true)?
                        .InheritanceBehavior == InheritanceBehavior.BeforeEachDerivedClass)
                {
                    initAndCleanupMethods[1] = methodInfo;
                }
            }
            else
            {
                // update class cleanup method
                classInfo.ClassCleanupMethod = methodInfo;
            }
        }
    }

    /// <summary>
    /// Update the classInfo if the parameter method is a testInitialize/cleanup method.
    /// </summary>
    /// <param name="classInfo"> The class Info. </param>
    /// <param name="methodInfo"> The method Info. </param>
    /// <param name="isBase"> If this needs to validate in base class or not. </param>
    /// <param name="instanceMethods"> The instance Methods. </param>
    private void UpdateInfoIfTestInitializeOrCleanupMethod(
        TestClassInfo classInfo,
        MethodInfo methodInfo,
        bool isBase,
        Dictionary<string, string?> instanceMethods)
    {
        bool hasTestInitialize = _reflectionHelper.IsNonDerivedAttributeDefined<TestInitializeAttribute>(methodInfo, inherit: false);
        bool hasTestCleanup = _reflectionHelper.IsNonDerivedAttributeDefined<TestCleanupAttribute>(methodInfo, inherit: false);

        if (!hasTestCleanup && !hasTestInitialize)
        {
            if (methodInfo.HasCorrectTestInitializeOrCleanupSignature())
            {
                instanceMethods[methodInfo.Name] = null;
            }

            return;
        }

        if (!methodInfo.HasCorrectTestInitializeOrCleanupSignature())
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestInitializeAndCleanupMethodHasWrongSignature, methodInfo.DeclaringType!.FullName, methodInfo.Name);
            throw new TypeInspectionException(message);
        }

        if (hasTestInitialize)
        {
            if (TryGetTimeoutInfo(methodInfo, FixtureKind.TestInitialize) is { } timeoutInfo)
            {
                classInfo.TestInitializeMethodTimeoutMilliseconds.Add(methodInfo, timeoutInfo);
            }

            if (!isBase)
            {
                classInfo.TestInitializeMethod = methodInfo;
            }
            else
            {
                if (!instanceMethods.ContainsKey(methodInfo.Name))
                {
                    classInfo.BaseTestInitializeMethodsQueue.Enqueue(methodInfo);
                }
            }
        }

        if (hasTestCleanup)
        {
            if (TryGetTimeoutInfo(methodInfo, FixtureKind.TestCleanup) is { } timeoutInfo)
            {
                classInfo.TestCleanupMethodTimeoutMilliseconds.Add(methodInfo, timeoutInfo);
            }

            if (!isBase)
            {
                classInfo.TestCleanupMethod = methodInfo;
            }
            else
            {
                if (!instanceMethods.ContainsKey(methodInfo.Name))
                {
                    classInfo.BaseTestCleanupMethodsQueue.Enqueue(methodInfo);
                }
            }
        }

        instanceMethods[methodInfo.Name] = null;
    }

    /// <summary>
    /// Resolve the test method. The function will try to
    /// find a function that has the method name with 0 parameters. If the function
    /// cannot be found, or a function is found that returns non-void, the result is
    /// set to error.
    /// </summary>
    /// <returns>
    /// The TestMethodInfo for the given test method. Null if the test method could not be found.
    /// </returns>
    private TestMethodInfo ResolveTestMethodInfo(TestMethod testMethod, TestClassInfo testClassInfo, ITestContext testContext, bool captureDebugTraces)
    {
        DebugEx.Assert(testMethod != null, "testMethod is Null");
        DebugEx.Assert(testClassInfo != null, "testClassInfo is Null");

        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);

        TimeoutInfo timeout = GetTestTimeout(methodInfo, testMethod);
        ExpectedExceptionBaseAttribute? expectedExceptionAttribute = _reflectionHelper.ResolveExpectedExceptionHelper(methodInfo, testMethod);
        var testMethodOptions = new TestMethodOptions(timeout, expectedExceptionAttribute, testContext, captureDebugTraces, GetTestMethodAttribute(methodInfo, testClassInfo));
        var testMethodInfo = new TestMethodInfo(methodInfo, testClassInfo, testMethodOptions);

        SetCustomProperties(testMethodInfo, testContext);

        return testMethodInfo;
    }

    private TestMethodInfo ResolveTestMethodInfoForDiscovery(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);

        // Let's build a fake options type as it won't be used.
        return new TestMethodInfo(methodInfo, testClassInfo, new(TimeoutInfo.FromTimeout(-1), null, null, false, null));
    }

    /// <summary>
    /// Provides the Test Method Extension Attribute of the TestClass.
    /// </summary>
    /// <param name="methodInfo"> The method info. </param>
    /// <param name="testClassInfo"> The test class info. </param>
    /// <returns>Test Method Attribute.</returns>
    private TestMethodAttribute? GetTestMethodAttribute(MethodInfo methodInfo, TestClassInfo testClassInfo)
    {
        // Get the derived TestMethod attribute from reflection
        TestMethodAttribute? testMethodAttribute = _reflectionHelper.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(methodInfo, inherit: false);

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        testMethodAttribute = testClassInfo.ClassAttribute.GetTestMethodAttribute(testMethodAttribute!) ?? testMethodAttribute;

        return testMethodAttribute;
    }

    /// <summary>
    /// Resolves a method by using the method name.
    /// </summary>
    /// <param name="testMethod"> The test Method. </param>
    /// <param name="testClassInfo"> The test Class Info. </param>
    /// <returns> The <see cref="MethodInfo"/>. </returns>
    private MethodInfo GetMethodInfoForTestMethod(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        bool discoverInternals = _discoverInternalsCache.GetOrAdd(
            testMethod.AssemblyName,
            _ => testClassInfo.Parent.Assembly.GetCustomAttribute<DiscoverInternalsAttribute>() != null);

        MethodInfo? testMethodInfo = testMethod.HasManagedMethodAndTypeProperties
            ? GetMethodInfoUsingManagedNameHelper(testMethod, testClassInfo, discoverInternals)
            : GetMethodInfoUsingRuntimeMethods(testMethod, testClassInfo, discoverInternals);

        // if correct method is not found, throw appropriate
        // exception about what is wrong.
        if (testMethodInfo == null)
        {
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_MethodDoesNotExists, testMethod.FullClassName, testMethod.Name);
            throw new TypeInspectionException(errorMessage);
        }

        return testMethodInfo;
    }

    private static MethodInfo? GetMethodInfoUsingManagedNameHelper(TestMethod testMethod, TestClassInfo testClassInfo, bool discoverInternals)
    {
        MethodBase? methodBase = null;
        try
        {
            methodBase = ManagedNameHelper.GetMethod(testClassInfo.Parent.Assembly, testMethod.ManagedTypeName!, testMethod.ManagedMethodName!);
        }
        catch (InvalidManagedNameException)
        {
        }

        MethodInfo? testMethodInfo = null;
        if (methodBase is MethodInfo mi)
        {
            testMethodInfo = mi;
        }
        else if (methodBase != null)
        {
            Type[] parameters = methodBase.GetParameters().Select(i => i.ParameterType).ToArray();
            testMethodInfo = PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeMethod(methodBase.DeclaringType!, methodBase.Name, parameters);
        }

        return testMethodInfo is null
            || !testMethodInfo.HasCorrectTestMethodSignature(true, discoverInternals)
            ? null
            : testMethodInfo;
    }

    private static MethodInfo? GetMethodInfoUsingRuntimeMethods(TestMethod testMethod, TestClassInfo testClassInfo, bool discoverInternals)
    {
        IEnumerable<MethodInfo> methods = PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeMethods(testClassInfo.ClassType)
            .Where(method => method.Name == testMethod.Name &&
                             method.HasCorrectTestMethodSignature(true, discoverInternals));

        if (testMethod.DeclaringClassFullName == null)
        {
            // Either the declaring class is the same as the test class, or
            // the declaring class information wasn't passed in the test case.
            // Prioritize the former while maintaining previous behavior for the latter.
            string? className = testClassInfo.ClassType.FullName;
            return methods
                .OrderByDescending(method => method.DeclaringType!.FullName == className)
                .FirstOrDefault();
        }

        // Only find methods that match the given declaring name.
        return methods.FirstOrDefault(method => method.DeclaringType!.FullName == testMethod.DeclaringClassFullName);
    }

    /// <summary>
    /// Gets the test timeout for the parameter test method.
    /// </summary>
    /// <param name="methodInfo"> The method Info. </param>
    /// <param name="testMethod"> The test Method. </param>
    /// <returns> The timeout value if defined in milliseconds. 0 if not defined. </returns>
    private TimeoutInfo GetTestTimeout(MethodInfo methodInfo, TestMethod testMethod)
    {
        DebugEx.Assert(methodInfo != null, "TestMethod should be non-null");
        TimeoutAttribute? timeoutAttribute = _reflectionHelper.GetFirstNonDerivedAttributeOrDefault<TimeoutAttribute>(methodInfo, inherit: false);

        if (timeoutAttribute != null)
        {
            if (!timeoutAttribute.HasCorrectTimeout)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, testMethod.FullClassName, testMethod.Name);
                throw new TypeInspectionException(message);
            }

            return TimeoutInfo.FromTimeoutAttribute(timeoutAttribute);
        }

        return TimeoutInfo.FromTestTimeoutSettings();
    }

    /// <summary>
    /// Set custom properties.
    /// </summary>
    /// <param name="testMethodInfo"> The test Method Info. </param>
    /// <param name="testContext"> The test Context. </param>
    private void SetCustomProperties(TestMethodInfo testMethodInfo, ITestContext testContext)
    {
        DebugEx.Assert(testMethodInfo != null, "testMethodInfo is Null");
        DebugEx.Assert(testMethodInfo.TestMethod != null, "testMethodInfo.TestMethod is Null");

        IEnumerable<TestPropertyAttribute> attributes = _reflectionHelper.GetDerivedAttributes<TestPropertyAttribute>(testMethodInfo.TestMethod, inherit: true);
        DebugEx.Assert(attributes != null, "attributes is null");

        if (testMethodInfo.TestMethod.DeclaringType is { } testClass)
        {
            attributes = attributes.Concat(_reflectionHelper.GetDerivedAttributes<TestPropertyAttribute>(testClass, inherit: true));
        }

        foreach (TestPropertyAttribute attribute in attributes)
        {
            if (!ValidateAndAssignTestProperty(testMethodInfo, testContext, attribute.Name, attribute.Value))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Validates If a Custom test property is valid and then adds it to the TestContext property list.
    /// </summary>
    /// <param name="testMethodInfo"> The test method info. </param>
    /// <param name="testContext"> The test context. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="propertyValue"> The property value. </param>
    /// <returns> True if its a valid Test Property. </returns>
    private static bool ValidateAndAssignTestProperty(
        TestMethodInfo testMethodInfo,
        ITestContext testContext,
        string propertyName,
        string propertyValue)
    {
        if (PredefinedNames.Any(predefinedProp => predefinedProp == propertyName))
        {
            testMethodInfo.NotRunnableReason = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ErrorPredefinedTestProperty,
                testMethodInfo.TestMethod.DeclaringType!.FullName,
                testMethodInfo.TestMethod.Name,
                propertyName);

            return false;
        }

        if (StringEx.IsNullOrEmpty(propertyName))
        {
            testMethodInfo.NotRunnableReason = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ErrorTestPropertyNullOrEmpty,
                testMethodInfo.TestMethod.DeclaringType!.FullName,
                testMethodInfo.TestMethod.Name);

            return false;
        }

        if (testContext.TryGetPropertyValue(propertyName, out object? existingValue))
        {
            // Do not add to the test context because it would conflict with an already existing value.
            // We were at one point reporting a warning here. However with extensibility centered around TestProperty where
            // users can have multiple WorkItemAttributes(say) we cannot throw a warning here. Users would have multiple of these attributes
            // so that it shows up in reporting rather than seeing them in TestContext properties.
        }
        else
        {
            testContext.AddProperty(propertyName, propertyValue);
        }

        return true;
    }
}
