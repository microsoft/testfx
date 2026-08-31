// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
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

#if NETCOREAPP
        // Using GetOrAdd to ensure we calculate only once when this is called by different threads in parallel.
        // Using a static lambda to ensure we don't capture.
        return _classInfoCache.GetOrAdd(typeName, CreateTestClassInfo, (this, testMethod));
#else
        // On .NET Framework, we don't have the GetOrAdd overload that prevents capturing lambdas.
        // So, we first try to get the value from the cache.
        if (_classInfoCache.TryGetValue(typeName, out TestClassInfo? cachedClassInfo))
        {
            return cachedClassInfo;
        }

        // If value doesn't already exist in the cache, we fallback to the GetOrAdd that allocates.
        return _classInfoCache.GetOrAdd(typeName, typeName => CreateTestClassInfo(typeName, (this, testMethod)));
#endif
    }

    private static TestClassInfo? CreateTestClassInfo(string typeName, (TypeCache Cache, TestMethod Method) tuple)
    {
        TestMethod testMethod = tuple.Method;
        TypeCache @this = tuple.Cache;

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
        catch (TypeLoadException ex)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result, but don't silently swallow the
            // exception: trace a warning with the type/assembly context and the full exception
            // so the underlying cause (e.g. a missing or mismatched assembly) is discoverable.
            try
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache.LoadType: Failed to load type '{0}' from assembly '{1}'. The corresponding test(s) will be reported as not found. {2}",
                        typeName,
                        assemblyName,
                        ex);
                }
            }
            catch (Exception)
            {
                // This diagnostic must not replace the established not-found result when a logging provider fails.
            }

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

        TestClassAttribute? testClassAttribute = ReflectHelper.Instance.GetSingleAttributeOrDefault<TestClassAttribute>(classType);
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

    private TimeoutInfo? TryGetTimeoutInfo(MethodInfo methodInfo, FixtureKind fixtureKind)
    {
        TimeoutAttribute? timeoutAttribute = _reflectionHelper.GetFirstAttributeOrDefault<TimeoutAttribute>(methodInfo);
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
                if (_reflectionHelper.GetFirstAttributeOrDefault<ClassInitializeAttribute>(methodInfo)?
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
                if (_reflectionHelper.GetFirstAttributeOrDefault<ClassCleanupAttribute>(methodInfo)?
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
        bool hasTestInitialize = _reflectionHelper.IsAttributeDefined<TestInitializeAttribute>(methodInfo);
        bool hasTestCleanup = _reflectionHelper.IsAttributeDefined<TestCleanupAttribute>(methodInfo);

        if (!hasTestCleanup && !hasTestInitialize)
        {
            if (instanceMethods is not null && methodInfo.HasCorrectTestInitializeOrCleanupSignature())
            {
                instanceMethods.Add(methodInfo.Name);
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
                if (instanceMethods is not null && !instanceMethods.Contains(methodInfo.Name))
                {
                    classInfo.BaseTestInitializeMethodsQueue.Add(methodInfo);
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
                    classInfo.BaseTestCleanupMethodsQueue.Add(methodInfo);
                }
            }
        }

        instanceMethods?.Add(methodInfo.Name);
    }

    #endregion
}
