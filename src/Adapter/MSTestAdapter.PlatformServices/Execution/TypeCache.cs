// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines type cache which reflects upon a type and cache its test artifacts.
/// </summary>
internal sealed class TypeCache : MarshalByRefObject
{
    /// <summary>
    /// Helper for reflection API's.
    /// </summary>
    private readonly IReflectionOperations _reflectionOperations;

    /// <summary>
    /// Assembly info cache.
    /// </summary>
    private readonly ConcurrentDictionary<Assembly, TestAssemblyInfo> _testAssemblyInfoCache = new();

    /// <summary>
    /// ClassInfo cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, TestClassInfo?> _classInfoCache = new();

    private readonly ConcurrentDictionary<string, bool> _discoverInternalsCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    internal TypeCache()
        : this(PlatformServiceProvider.Instance.ReflectionOperations)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    /// <param name="reflectionOperations"> An instance to the <see cref="IReflectionOperations"/> object. </param>
    internal TypeCache(IReflectionOperations reflectionOperations) => _reflectionOperations = reflectionOperations;

    /// <summary>
    /// Gets Class Info cache which has cleanup methods to execute.
    /// </summary>
    public IEnumerable<TestClassInfo> ClassInfoListWithExecutableCleanupMethods
        => _classInfoCache.Values.Where(classInfo => classInfo?.HasExecutableCleanupMethod == true)!;

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
    public ICollection<TestClassInfo?> ClassInfoCache => _classInfoCache.Values;

    /// <summary>
    /// Get the test method info corresponding to the parameter test Element.
    /// </summary>
    /// <returns> The <see cref="TestMethodInfo"/>. </returns>
    public TestMethodInfo? GetTestMethodInfo(TestMethod testMethod, ITestContext testContext)
    {
        Ensure.NotNull(testMethod);
        Ensure.NotNull(testContext);

        // Get the classInfo (This may throw as GetType calls assembly.GetType(..,true);)
        TestClassInfo? testClassInfo = GetClassInfo(testMethod);

        if (testClassInfo == null)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result.
            return null;
        }

        // Get the testMethod
        return ResolveTestMethodInfo(testMethod, testClassInfo, testContext);
    }

    /// <summary>
    /// Get the test method info corresponding to the parameter test Element.
    /// </summary>
    /// <returns> The <see cref="TestMethodInfo"/>. </returns>
    public DiscoveryTestMethodInfo? GetTestMethodInfoForDiscovery(TestMethod testMethod)
    {
        Ensure.NotNull(testMethod);

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
        // Using GetOrAdd to ensure we calculate only once when this is called by different threads in parallel.
        // Using a static lambda to ensure we don't capture.
        return _classInfoCache.GetOrAdd(typeName, static (typeName, tuple) =>
        {
            TestMethod testMethod = tuple.testMethod;
            TypeCache @this = tuple.Item1;

            // Load the class type
            Type? type = LoadType(typeName, testMethod.AssemblyName);

            if (type == null)
            {
                // This means the class containing the test method could not be found.
                // Return null so we return a not found result.
                return null;
            }

            // Get the classInfo
            return @this.CreateClassInfo(type);
        }, (this, testMethod));
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
                Assembly assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyName);

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
    /// <returns> The <see cref="TestClassInfo"/>. </returns>
    private TestClassInfo CreateClassInfo(Type classType)
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
            throw new TypeInspectionException(string.Format(CultureInfo.CurrentCulture, Resource.UTA_NoValidConstructor, classType.FullName));
        }

        ConstructorInfo constructor = selectedConstructor.Value.CtorInfo;
        bool isParameterLessConstructor = selectedConstructor.Value.IsParameterless;

        TestAssemblyInfo assemblyInfo = GetAssemblyInfo(classType.Assembly);

        TestClassAttribute? testClassAttribute = PlatformServiceProvider.Instance.ReflectionOperations.GetSingleAttributeOrDefault<TestClassAttribute>(classType);
        DebugEx.Assert(testClassAttribute is not null, "testClassAttribute is null");
        var classInfo = new TestClassInfo(classType, constructor, isParameterLessConstructor, testClassAttribute, assemblyInfo);

        // List holding the instance of the initialize/cleanup methods
        // to be passed into the tuples' queue  when updating the class info.
        var initAndCleanupMethods = new MethodInfo?[2];

        // List of instance methods present in the type as well its base type
        // which is used to decide whether TestInitialize/TestCleanup methods
        // present in the base type should be used or not. They are not used if
        // the method is overridden in the derived type.
        HashSet<string>? instanceMethods = classType.BaseType == typeof(object) ? null : [];

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

    #endregion

    #region AssemblyInfo creation and cache logic.

    private TimeoutInfo? TryGetTimeoutInfo(MethodInfo methodInfo, FixtureKind fixtureKind)
    {
        TimeoutAttribute? timeoutAttribute = _reflectionOperations.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo);
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
    /// Get the assembly info for the assembly given.
    /// </summary>
    /// <param name="assembly"> The assembly to get its info. </param>
    /// <returns> The <see cref="TestAssemblyInfo"/> instance. </returns>
    private TestAssemblyInfo GetAssemblyInfo(Assembly assembly)
        // Using GetOrAdd to ensure we calculate only once when this is called by different threads in parallel.
        // Using a static lambda to ensure we don't capture.
        => _testAssemblyInfoCache.GetOrAdd(assembly, static (assembly, @this) =>
            {
                var assemblyInfo = new TestAssemblyInfo(assembly);

                Type[] types = AssemblyEnumerator.GetTypes(assembly);

                foreach (Type t in types)
                {
                    try
                    {
                        // Only examine classes which are TestClass or derives from TestClass attribute
                        if (!@this._reflectionOperations.IsAttributeDefined<TestClassAttribute>(t))
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we fail to discover type from an assembly, then do not abort. Pick the next type.
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                                "TypeCache: Exception occurred while checking whether type {0} is a test class or not. {1}",
                                t.FullName,
                                ex);
                        }

                        continue;
                    }

                    // Enumerate through all methods and identify the Assembly Init and cleanup methods.
                    foreach (MethodInfo methodInfo in PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(t))
                    {
                        if (@this.IsAssemblyOrClassInitializeMethod<AssemblyInitializeAttribute>(methodInfo))
                        {
                            assemblyInfo.AssemblyInitializeMethod = methodInfo;
                            assemblyInfo.AssemblyInitializeMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyInitialize);
                        }
                        else if (@this.IsAssemblyOrClassCleanupMethod<AssemblyCleanupAttribute>(methodInfo))
                        {
                            assemblyInfo.AssemblyCleanupMethod = methodInfo;
                            assemblyInfo.AssemblyCleanupMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyCleanup);
                        }

                        bool isGlobalTestInitialize = @this._reflectionOperations.IsAttributeDefined<GlobalTestInitializeAttribute>(methodInfo);
                        bool isGlobalTestCleanup = @this._reflectionOperations.IsAttributeDefined<GlobalTestCleanupAttribute>(methodInfo);

                        if (isGlobalTestInitialize || isGlobalTestCleanup)
                        {
                            // Only try to validate the method if it already has the needed attribute.
                            // This avoids potential type load exceptions when the return type cannot be resolved.
                            // NOTE: Users tend to load assemblies in AssemblyInitialize after finishing the discovery.
                            // We want to avoid loading types early as much as we can.
                            bool isValid = methodInfo is { IsSpecialName: false, IsPublic: true, IsStatic: true, IsGenericMethod: false, DeclaringType.IsGenericType: false, DeclaringType.IsPublic: true } &&
                                methodInfo.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].ParameterType == typeof(TestContext) &&
                                methodInfo.IsValidReturnType(@this._reflectionOperations);

                            if (isValid && isGlobalTestInitialize)
                            {
                                assemblyInfo.GlobalTestInitializations.Add((methodInfo, @this.TryGetTimeoutInfo(methodInfo, FixtureKind.TestInitialize)));
                            }

                            if (isValid && isGlobalTestCleanup)
                            {
                                assemblyInfo.GlobalTestCleanups.Add((methodInfo, @this.TryGetTimeoutInfo(methodInfo, FixtureKind.TestCleanup)));
                            }
                        }
                    }
                }

                return assemblyInfo;
            }, this);

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
        if (!_reflectionOperations.IsAttributeDefined<TInitializeAttribute>(methodInfo))
        {
            return false;
        }

        if (!methodInfo.HasCorrectClassOrAssemblyInitializeSignature(_reflectionOperations))
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
        if (!_reflectionOperations.IsAttributeDefined<TCleanupAttribute>(methodInfo))
        {
            return false;
        }

        if (!methodInfo.HasCorrectClassOrAssemblyCleanupSignature(_reflectionOperations))
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
                if (_reflectionOperations.GetFirstAttributeOrDefault<ClassInitializeAttribute>(methodInfo)?
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
                if (_reflectionOperations.GetFirstAttributeOrDefault<ClassCleanupAttribute>(methodInfo)?
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
        HashSet<string>? instanceMethods)
    {
        bool hasTestInitialize = _reflectionOperations.IsAttributeDefined<TestInitializeAttribute>(methodInfo);
        bool hasTestCleanup = _reflectionOperations.IsAttributeDefined<TestCleanupAttribute>(methodInfo);

        if (!hasTestCleanup && !hasTestInitialize)
        {
            if (instanceMethods is not null && methodInfo.HasCorrectTestInitializeOrCleanupSignature(_reflectionOperations))
            {
                instanceMethods.Add(methodInfo.Name);
            }

            return;
        }

        if (!methodInfo.HasCorrectTestInitializeOrCleanupSignature(_reflectionOperations))
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
                if (instanceMethods is not null && !instanceMethods.Contains(methodInfo.Name))
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
                if (instanceMethods is not null && !instanceMethods.Contains(methodInfo.Name))
                {
                    classInfo.BaseTestCleanupMethodsQueue.Enqueue(methodInfo);
                }
            }
        }

        instanceMethods?.Add(methodInfo.Name);
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
    private TestMethodInfo ResolveTestMethodInfo(TestMethod testMethod, TestClassInfo testClassInfo, ITestContext testContext)
    {
        DebugEx.Assert(testMethod != null, "testMethod is Null");
        DebugEx.Assert(testClassInfo != null, "testClassInfo is Null");

        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);

        return new TestMethodInfo(methodInfo, testClassInfo, testContext);
    }

    private DiscoveryTestMethodInfo ResolveTestMethodInfoForDiscovery(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);
        return new DiscoveryTestMethodInfo(methodInfo, testClassInfo);
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
            static (_, testClassInfo) => testClassInfo.Parent.Assembly.GetCustomAttribute<DiscoverInternalsAttribute>() != null,
            testClassInfo);

        MethodInfo? testMethodInfo = testMethod.HasManagedMethodAndTypeProperties
            ? GetMethodInfoUsingManagedNameHelper(testMethod, testClassInfo, discoverInternals)
            : throw ApplicationStateGuard.Unreachable();

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
        MethodInfo? testMethodInfo = null;
        try
        {
            // testMethod.MethodInfo can be null if 'TestMethod' instance crossed app domain boundaries.
            // This happens on .NET Framework when app domain is enabled, and the MethodInfo is calculated and set during discovery.
            // Then, execution will cause TestMethod to cross to a different app domain, and MethodInfo will be null.
            // In addition, it also happens when deployment items are used and app domain is disabled.
            // We explicitly set it to null in this case because the original MethodInfo calculated during discovery cannot be used because
            // it points to the type loaded from the assembly in bin instead of from deployment directory.
            testMethodInfo = testMethod.MethodInfo ?? ManagedNameHelper.GetMethod(testClassInfo.Parent.Assembly, testMethod.ManagedTypeName!, testMethod.ManagedMethodName!);
        }
        catch (InvalidManagedNameException)
        {
        }

        return testMethodInfo is null
            || !testMethodInfo.HasCorrectTestMethodSignature(true, PlatformServiceProvider.Instance.ReflectionOperations, discoverInternals)
            ? null
            : testMethodInfo;
    }
}
