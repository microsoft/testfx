// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;
#if NET && !WINDOWS_UWP
using System.Runtime.Loader;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines type cache which reflects upon a type and cache its test artifacts.
/// </summary>
internal sealed class TypeCache
#if NETFRAMEWORK
    : MarshalByRefObject
#endif
{
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
    private readonly ConcurrentDictionary<string, TestClassInfo?> _classInfoCache = new();

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
    public TestMethodInfo? GetTestMethodInfo(TestMethod testMethod)
    {
        // Get the classInfo (This may throw as GetType calls assembly.GetType(..,true);)
        TestClassInfo? testClassInfo = GetClassInfo(testMethod);

        if (testClassInfo == null)
        {
            // This means the class containing the test method could not be found.
            // Return null so we return a not found result.
            return null;
        }

        // Get the testMethod
        return ResolveTestMethodInfo(testMethod, testClassInfo);
    }

    /// <summary>
    /// Get the test method info corresponding to the parameter test Element.
    /// </summary>
    /// <returns> The <see cref="TestMethodInfo"/>. </returns>
    public DiscoveryTestMethodInfo? GetTestMethodInfoForDiscovery(TestMethod testMethod)
    {
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

#if NETFRAMEWORK
    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
    public override object? InitializeLifetimeService() => null;
#endif

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

    #endregion

    #region AssemblyInfo creation and cache logic.

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
    /// Get the assembly info for the assembly given.
    /// </summary>
    /// <param name="assembly"> The assembly to get its info. </param>
    /// <returns> The <see cref="TestAssemblyInfo"/> instance. </returns>
    private TestAssemblyInfo GetAssemblyInfo(Assembly assembly)
    {
#if NETCOREAPP
        // Using GetOrAdd to ensure we calculate only once when this is called by different threads in parallel.
        // Using a static lambda to ensure we don't capture.
        return _testAssemblyInfoCache.GetOrAdd(assembly, CreateTestAssemblyInfo, this);
#else
        if (_testAssemblyInfoCache.TryGetValue(assembly, out TestAssemblyInfo cachedTestAssemblyInfo))
        {
            return cachedTestAssemblyInfo;
        }

        // Not cached already. Fallback to GetOrAdd call that captures "this" and allocates.
        return _testAssemblyInfoCache.GetOrAdd(assembly, assembly => CreateTestAssemblyInfo(assembly, this));
#endif
    }

    private static TestAssemblyInfo CreateTestAssemblyInfo(Assembly assembly, TypeCache @this)
    {
        var assemblyInfo = new TestAssemblyInfo(assembly);

        Type[] types = AssemblyEnumerator.GetTypes(assembly);

        foreach (Type t in types)
        {
            try
            {
                // Only examine classes which are TestClass or derives from TestClass attribute
                if (!@this._reflectionHelper.IsAttributeDefined<TestClassAttribute>(t))
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

                bool isGlobalTestInitialize = @this._reflectionHelper.IsAttributeDefined<GlobalTestInitializeAttribute>(methodInfo);
                bool isGlobalTestCleanup = @this._reflectionHelper.IsAttributeDefined<GlobalTestCleanupAttribute>(methodInfo);

                if (isGlobalTestInitialize || isGlobalTestCleanup)
                {
                    // Only try to validate the method if it already has the needed attribute.
                    // This avoids potential type load exceptions when the return type cannot be resolved.
                    // NOTE: Users tend to load assemblies in AssemblyInitialize after finishing the discovery.
                    // We want to avoid loading types early as much as we can.
                    bool isValid = methodInfo is { IsSpecialName: false, IsPublic: true, IsStatic: true, IsGenericMethod: false, DeclaringType.IsGenericType: false, DeclaringType.IsPublic: true } &&
                        methodInfo.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].ParameterType == typeof(TestContext) &&
                        methodInfo.IsValidReturnType();

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

        // After looking at the current assembly, honor any [AssemblyFixtureProvider] markers contributed
        // by referenced libraries. Local declarations on the test assembly always win silently — the
        // provider pass only fills slots that the in-assembly pass left empty. See
        // https://github.com/microsoft/testfx/issues/757.
        DiscoverFixturesFromProviders(assembly, assemblyInfo, @this);

        return assemblyInfo;
    }

    private static void DiscoverFixturesFromProviders(Assembly currentAssembly, TestAssemblyInfo assemblyInfo, TypeCache @this)
    {
        // Snapshot which slots were filled by the in-assembly pass. Local declarations are
        // authoritative — never let a provider overwrite or even consider those slots, so the
        // provider pass stays silent when the test assembly already declared a fixture method.
        bool localProvidedInit = assemblyInfo.AssemblyInitializeMethod is not null;
        bool localProvidedCleanup = assemblyInfo.AssemblyCleanupMethod is not null;

        if (localProvidedInit && localProvidedCleanup)
        {
            return;
        }

        foreach (Assembly candidate in EnumerateCandidateAssemblies(currentAssembly))
        {
            // Cheap presence check via metadata: CustomAttributeData does not invoke the attribute
            // constructor and so cannot trip on a typeof(...) argument whose target assembly fails
            // to resolve. We only need to know whether the marker is on the assembly; if it is, an
            // instantiation failure below becomes a real diagnostic instead of a silent drop.
            bool hasMarker;
            try
            {
                hasMarker = HasAssemblyFixtureProviderMarker(candidate);
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Exception occurred while probing AssemblyFixtureProviderAttribute metadata from assembly {0}. {1}",
                        SafeGetAssemblyName(candidate),
                        ex);
                }

                continue;
            }

            if (!hasMarker)
            {
                continue;
            }

            object[] markers;
            try
            {
                markers = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(candidate, typeof(AssemblyFixtureProviderAttribute));
            }
            catch (Exception ex)
            {
                // The marker is present (CustomAttributeData saw it) but the attribute cannot be
                // instantiated. This usually means the type referenced by typeof(...) cannot be
                // loaded. [AssemblyFixtureProvider] is explicit opt-in: silently dropping the
                // marker here would let assembly init/cleanup quietly disappear. Surface as a
                // standard MSTest diagnostic so the failure is visible to the user.
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_AssemblyFixtureProviderLoadFailed,
                    SafeGetAssemblyName(candidate) ?? "<unknown>",
                    ex.Message);
                throw new TypeInspectionException(message, ex);
            }

            if (markers is null || markers.Length == 0)
            {
                continue;
            }

            foreach (object marker in markers)
            {
                if (marker is not AssemblyFixtureProviderAttribute providerAttribute)
                {
                    continue;
                }

                Type? fixtureType = providerAttribute.FixtureType;
                if (fixtureType is null)
                {
                    continue;
                }

                if (fixtureType.ContainsGenericParameters)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_AssemblyFixtureProviderTypeIsGeneric, fixtureType.FullName);
                    throw new TypeInspectionException(message);
                }

                CollectFixtureMethodsFromProviderType(fixtureType, assemblyInfo, @this, localProvidedInit, localProvidedCleanup);
            }
        }
    }

    private static void CollectFixtureMethodsFromProviderType(
        Type fixtureType,
        TestAssemblyInfo assemblyInfo,
        TypeCache @this,
        bool localProvidedInit,
        bool localProvidedCleanup)
    {
        MethodInfo[] methods;
        try
        {
            methods = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(fixtureType);
        }
        catch (Exception ex)
        {
            // [AssemblyFixtureProvider] is explicit opt-in: silently dropping the provider type
            // would let a missing dependency or metadata problem make assembly init/cleanup quietly
            // disappear. Surface as a standard MSTest diagnostic instead.
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_AssemblyFixtureProviderTypeReflectionFailed,
                fixtureType.FullName,
                ex.Message);
            throw new TypeInspectionException(message);
        }

        foreach (MethodInfo methodInfo in methods)
        {
            // Slots filled by the in-assembly pass (local declarations) are immutable — silently
            // skip provider methods that target them. Slots left empty are filled here; the
            // setters on TestAssemblyInfo throw UTA_ErrorMultiAssemblyInit / UTA_ErrorMultiAssemblyClean
            // if two providers contribute methods for the same slot, surfacing the conflict as a
            // standard MSTest diagnostic.
            if (!localProvidedInit && @this.IsAssemblyOrClassInitializeMethod<AssemblyInitializeAttribute>(methodInfo))
            {
                assemblyInfo.AssemblyInitializeMethod = methodInfo;
                assemblyInfo.AssemblyInitializeMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyInitialize);
            }
            else if (!localProvidedCleanup && @this.IsAssemblyOrClassCleanupMethod<AssemblyCleanupAttribute>(methodInfo))
            {
                assemblyInfo.AssemblyCleanupMethod = methodInfo;
                assemblyInfo.AssemblyCleanupMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyCleanup);
            }
        }
    }

    private static IEnumerable<Assembly> EnumerateCandidateAssemblies(Assembly currentAssembly)
    {
        // BFS over the consumer assembly's reference graph. This both bounds the work to assemblies
        // the consumer actually depends on (vs scanning the entire AppDomain) and lets us discover
        // markers on libraries that haven't been touched by the test code yet — passively referencing
        // them in a project file is enough.
        //
        // Dedup uses AssemblyName.FullName (name + version + culture + public-key-token) so multi-version
        // / multi-token references with the same simple name are not collapsed.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>();

        visited.Add(currentAssembly.FullName ?? string.Empty);

        // The consumer assembly itself is yielded first so users may also place the marker on the
        // test project (handy escape hatch when the library author cannot ship the attribute themselves).
        queue.Enqueue(currentAssembly);

        while (queue.Count > 0)
        {
            Assembly current = queue.Dequeue();
            yield return current;

            AssemblyName[] references;
            try
            {
                references = current.GetReferencedAssemblies();
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Exception occurred while enumerating referenced assemblies of {0} for AssemblyFixtureProvider discovery. {1}",
                        SafeGetAssemblyName(current),
                        ex);
                }

                continue;
            }

            foreach (AssemblyName referenceName in references)
            {
                string? name = referenceName.Name;
                if (name is null || IsFrameworkAssemblyName(name))
                {
                    continue;
                }

                if (!visited.Add(referenceName.FullName))
                {
                    continue;
                }

                Assembly? referenced = TryLoadReferencedAssembly(current, referenceName);
                if (referenced is null || referenced.IsDynamic)
                {
                    continue;
                }

                queue.Enqueue(referenced);
            }
        }
    }

    private static Assembly? TryLoadReferencedAssembly(Assembly referrer, AssemblyName referenceName)
    {
        try
        {
#if NET && !WINDOWS_UWP
            // Resolve through the same AssemblyLoadContext as the referrer so plugin-style hosts
            // (which place the test assembly in a non-default ALC) don't end up loading a second
            // copy of the provider library into the default ALC, which would cause assembly
            // fixtures to mutate static state on the wrong assembly instance.
            AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(referrer) ?? AssemblyLoadContext.Default;
            return loadContext.LoadFromAssemblyName(referenceName);
#else
            _ = referrer;
            return Assembly.Load(referenceName);
#endif
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "TypeCache: Could not load referenced assembly {0} for AssemblyFixtureProvider discovery. {1}",
                    referenceName.FullName,
                    ex);
            }

            return null;
        }
    }

    private static bool IsFrameworkAssemblyName(string name)
        => name.StartsWith("System.", StringComparison.Ordinal)
            || name.Equals("System", StringComparison.Ordinal)
            || name.Equals("mscorlib", StringComparison.Ordinal)
            || name.Equals("netstandard", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.VisualStudio.TestTools", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.TestPlatform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Testing.", StringComparison.Ordinal)
            || name.Equals("Microsoft.Testing.Platform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.NET.", StringComparison.Ordinal)
            || name.Equals("Microsoft.CSharp", StringComparison.Ordinal)
            || name.StartsWith("MSTest.", StringComparison.Ordinal)
            || name.StartsWith("MSTestAdapter.", StringComparison.Ordinal);

    private static bool HasAssemblyFixtureProviderMarker(Assembly assembly)
    {
        // Compare on the attribute type's FullName so we don't trigger attribute construction
        // (and therefore don't depend on the typeof(...) argument being resolvable). This is
        // exactly the "is the marker present at all?" probe.
        string markerFullName = typeof(AssemblyFixtureProviderAttribute).FullName!;
        foreach (CustomAttributeData data in assembly.GetCustomAttributesData())
        {
            if (string.Equals(data.AttributeType.FullName, markerFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? SafeGetAssemblyName(Assembly assembly)
    {
        try
        {
            return assembly.GetName().Name;
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException or SecurityException or NotSupportedException)
        {
            return null;
        }
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
        if (!_reflectionHelper.IsAttributeDefined<TInitializeAttribute>(methodInfo))
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
        if (!_reflectionHelper.IsAttributeDefined<TCleanupAttribute>(methodInfo))
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
    private static TestMethodInfo ResolveTestMethodInfo(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        DebugEx.Assert(testMethod != null, "testMethod is Null");
        DebugEx.Assert(testClassInfo != null, "testClassInfo is Null");

        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);

        return new TestMethodInfo(methodInfo, testClassInfo);
    }

    private static DiscoveryTestMethodInfo ResolveTestMethodInfoForDiscovery(TestMethod testMethod, TestClassInfo testClassInfo)
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
    private static MethodInfo GetMethodInfoForTestMethod(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo? testMethodInfo = testMethod.HasManagedMethodAndTypeProperties
            ? GetMethodInfoUsingManagedNameHelper(testMethod, testClassInfo)
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

    private static MethodInfo? GetMethodInfoUsingManagedNameHelper(TestMethod testMethod, TestClassInfo testClassInfo)
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
            || !testMethodInfo.HasCorrectTestMethodSignature(true, testClassInfo.Parent.DiscoversInternals)
            ? null
            : testMethodInfo;
    }
}
