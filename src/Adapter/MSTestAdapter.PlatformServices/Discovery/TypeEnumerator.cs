// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Enumerates through the type looking for Valid Test Methods to execute.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class TypeEnumerator
{
    private readonly Type _type;
    private readonly string _assemblyFilePath;
    private readonly TypeValidator _typeValidator;
    private readonly TestMethodValidator _testMethodValidator;
    private readonly ReflectHelper _reflectHelper;
    private List<ResourceLockInfo>? _classResourceLocks;
    private bool _classResourceLocksComputed;
    private List<TestDependencyInfo>? _classDependencies;
    private bool _classDependenciesComputed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeEnumerator"/> class.
    /// </summary>
    /// <param name="type"> The reflected type. </param>
    /// <param name="assemblyFilePath"> The name of the assembly being reflected. </param>
    /// <param name="reflectHelper"> An instance to reflection helper for type information. </param>
    /// <param name="typeValidator"> The validator for test classes. </param>
    /// <param name="testMethodValidator"> The validator for test methods. </param>
    internal TypeEnumerator(Type type, string assemblyFilePath, ReflectHelper reflectHelper, TypeValidator typeValidator, TestMethodValidator testMethodValidator)
    {
        _type = type;
        _assemblyFilePath = assemblyFilePath;
        _reflectHelper = reflectHelper;
        _typeValidator = typeValidator;
        _testMethodValidator = testMethodValidator;
    }

    /// <summary>
    /// Walk through all methods in the type, and find out the test methods.
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> list of test cases.</returns>
    internal virtual List<UnitTestElement>? Enumerate(List<string> warnings)
        => EnumerateCore(warnings, useGeneratedDescriptors: false);

    internal virtual List<UnitTestElement>? Enumerate(List<string> warnings, bool useGeneratedDescriptors)
        => EnumerateCore(warnings, useGeneratedDescriptors);

    private List<UnitTestElement>? EnumerateCore(List<string> warnings, bool useGeneratedDescriptors)
    {
        if (!_typeValidator.IsValidTestClass(_type, warnings))
        {
            return null;
        }

        // Track class-level attributes for telemetry (read Current per call so a session reset
        // between TypeEnumerator construction and use cannot cause writes to land on an
        // orphaned collector).
#if !WINDOWS_UWP && !WIN_UI
        if (Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestTelemetryDataCollector.Current is { } telemetryDataCollector)
        {
            Attribute[] classAttributes = ReflectHelper.GetCustomAttributesCached(_type);
            telemetryDataCollector.TrackDiscoveredClass(classAttributes);
        }
#endif

        // If test class is valid, then get the tests
        return useGeneratedDescriptors
            && PlatformServiceProvider.Instance.ReflectionOperations.TryGetTestMethodDescriptors(
                _type,
                out MethodInfo[]? descriptorMethods,
                out bool areAllTestMethodsSupported)
                ? GetTests(warnings, descriptorMethods, areAllTestMethodsSupported)
                : GetTests(warnings);
    }

    /// <summary>
    /// Gets a list of valid tests in a type.
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> List of Valid Tests. </returns>
    internal List<UnitTestElement> GetTests(List<string> warnings)
        => GetTests(warnings, [], areAllTestMethodsSupported: false);

    private List<UnitTestElement> GetTests(List<string> warnings, MethodInfo[] descriptorMethods, bool areAllTestMethodsSupported)
    {
        bool foundDuplicateTests = false;
        var foundTests = new HashSet<string>();
        var tests = new List<UnitTestElement>(descriptorMethods.Length);
        HashSet<MethodInfo>? descriptorMethodSet = areAllTestMethodsSupported || descriptorMethods.Length == 0
            ? null
            : [.. descriptorMethods];

        // Instead of asking reflect helper to query the type for every method we have, we ask once for the type.
        bool classDisablesParallelization = _reflectHelper.IsAttributeDefined<DoNotParallelizeAttribute>(_type);

        // Test class is already valid. Verify methods.
        // PERF: GetRuntimeMethods is used here to get all methods, including non-public, and static methods.
        // if we rely on analyzers to identify all invalid methods on build, we can change this to fit the current settings.
        foreach (MethodInfo method in descriptorMethods)
        {
            foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.ToString() ?? method.Name);
            tests.Add(GetTestFromMethod(method, classDisablesParallelization, warnings, isFromGeneratedDescriptor: true));
        }

        if (!areAllTestMethodsSupported)
        {
            foreach (MethodInfo method in PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeMethods(_type))
            {
                if (descriptorMethodSet?.Contains(method) ?? false)
                {
                    continue;
                }

                if (_testMethodValidator.IsValidTestMethod(method, _type, warnings))
                {
                    // ToString() outputs method name and its signature. This is necessary for overloaded methods to be recognized as distinct tests.
                    foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.ToString() ?? method.Name);
                    UnitTestElement testMethod = GetTestFromMethod(method, classDisablesParallelization, warnings);

                    tests.Add(testMethod);
                }
            }
        }

        if (!foundDuplicateTests)
        {
            return tests;
        }

        // Remove duplicate test methods by taking the first one of each name
        // that is declared closest to the test class in the hierarchy.
        var inheritanceDepths = new Dictionary<string, int>();
        Type? currentType = _type;
        int currentDepth = 0;

        while (currentType != null)
        {
            inheritanceDepths[currentType.FullName!] = currentDepth;
            ++currentDepth;
            currentType = currentType.BaseType;
        }

        return [.. tests.GroupBy(
            t => t.TestMethod.Name,
            (_, elements) =>
                // Note: null suppression for MethodInfo here is safe.
                // The property is marked as null because it's not serializable and will be null when crossing appdomain.
                // But in this context, we are accessing MethodInfo in the same appdomain that created it, so we are not crossing appdomain boundaries.
                // The GetTestFromMethod call above guarantees that it's non-null.
                // The null suppression for DeclaringType is also safe, there is no reason for a test method to have null declaring type.
                // FullName is also guaranteed to be non-null.
                elements.OrderBy(t => inheritanceDepths[t.TestMethod.MethodInfo!.DeclaringType!.FullName!]).First())];
    }

    /// <summary>
    /// Gets a UnitTestElement from a MethodInfo object filling it up with appropriate values.
    /// </summary>
    /// <param name="method">The reflected method.</param>
    /// <param name="classDisablesParallelization">Whether the test class disables parallelization.</param>
    /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
    /// <returns> Returns a UnitTestElement.</returns>
    internal UnitTestElement GetTestFromMethod(MethodInfo method, bool classDisablesParallelization, ICollection<string> warnings)
        => GetTestFromMethod(method, classDisablesParallelization, warnings, isFromGeneratedDescriptor: false);

    /// <summary>
    /// Gets a UnitTestElement from a MethodInfo object filling it up with appropriate values.
    /// </summary>
    /// <param name="method">The reflected method.</param>
    /// <param name="classDisablesParallelization">Whether the test class disables parallelization.</param>
    /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
    /// <param name="isFromGeneratedDescriptor">Whether native MTP discovery selected this method from generated metadata.</param>
    /// <returns> Returns a UnitTestElement.</returns>
    internal UnitTestElement GetTestFromMethod(MethodInfo method, bool classDisablesParallelization, ICollection<string> warnings, bool isFromGeneratedDescriptor)
    {
        // null if the current instance represents a generic type parameter.
        DebugEx.Assert(_type.AssemblyQualifiedName != null, "AssemblyQualifiedName for method is null.");

        // Note: We pass _type.FullName (the closed-generic CLR name) as FullClassName because TypeCache.LoadType
        // calls assembly.GetType(FullClassName) which requires the closed-generic form to instantiate the type.
        // The managed type name (open-generic form, used for VSTest ManagedType property) is derived from
        // FullClassName by TestMethod.ManagedTypeName via stripping the generic argument list.
        ManagedNameHelper.GetManagedNameAndHierarchy(method, out _, out string managedMethod, out string?[] hierarchyValues);
        ParameterInfo[] parameters = method.GetParameters();
        var testMethod = new TestMethod(managedMethod, hierarchyValues, method.Name, _type.FullName!, _assemblyFilePath, null,
            parameters.Length == 0 ? string.Empty : string.Join(",", Array.ConvertAll(parameters, static p => p.ParameterType.ToString())))
        {
            MethodInfo = method,
        };

        // For every test method in a class, we are asking reflect helper multiple times for the same
        // information (like test categories, traits, deployment items) which is not optimal.
        IReflectionOperations reflectionOperations = PlatformServiceProvider.Instance.ReflectionOperations;
        var testElement = new UnitTestElement(testMethod)
        {
            IsFromGeneratedDescriptor = isFromGeneratedDescriptor,
            TestCategory = reflectionOperations.GetTestCategories(method, _type),
            DoNotParallelize = classDisablesParallelization || _reflectHelper.IsAttributeDefined<DoNotParallelizeAttribute>(method),
            ResourceLocks = MergeResourceLocks(GetClassResourceLocks(), ReadResourceLocks(method)),
            Dependencies = MergeDependencies(GetClassDependencies(), ReadDependencies(method), _type.FullName!, method.Name),
#if !WINDOWS_UWP && !WIN_UI
            DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(method, _type, warnings),
#endif
            Traits = [.. reflectionOperations.GetTestPropertiesAsTraits(method)],
        };

        Attribute[] attributes = reflectionOperations.GetCustomAttributesCached(method);
#if !WINDOWS_UWP && !WIN_UI
        Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestTelemetryDataCollector.Current?.TrackDiscoveredMethod(attributes);
#endif
        TestMethodAttribute? testMethodAttribute = null;
        List<string>? workItemIds = null;

        // Backward looping for backcompat. This used to be calls to _reflectHelper.GetFirstAttributeOrDefault
        // So, to make sure the first attribute always wins, we loop from end to start.
        // WorkItemAttribute is also collected here to avoid a second pass with OfType + Any + Select.
        for (int i = attributes.Length - 1; i >= 0; i--)
        {
            if (attributes[i] is TestMethodAttribute tma)
            {
                testMethodAttribute = tma;
            }
            else if (attributes[i] is PriorityAttribute priorityAttribute)
            {
                testElement.Priority = priorityAttribute.Priority;
            }
            else if (attributes[i] is WorkItemAttribute workItem)
            {
                (workItemIds ??= []).Add(workItem.Id.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (workItemIds is not null)
        {
            workItemIds.Reverse();
            testElement.WorkItemIds = [.. workItemIds];
        }

        // In production, we always have a TestMethod attribute because GetTestFromMethod is called under IsValidTestMethod
        // In unit tests, we may not have the test to have TestMethodAttribute.
        // Unit tests could be adjusted to properly have the attribute so the assert can be uncommented.
        // DebugEx.Assert(testMethodAttribute is not null, "Expected to find a 'TestMethod' attribute.");

        // get DisplayName from TestMethodAttribute (or any inherited attribute)
        testMethod.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;
        testElement.DeclaringFilePath = testMethodAttribute?.DeclaringFilePath;
        testElement.DeclaringLineNumber = testMethodAttribute?.DeclaringLineNumber;
        testElement.UnfoldingStrategy = testMethodAttribute?.UnfoldingStrategy ?? TestDataSourceUnfoldingStrategy.Auto;

        return testElement;
    }

    /// <summary>
    /// Reads (and caches for this type) the <c>[ResourceLock]</c> attributes declared on the test class.
    /// </summary>
    private List<ResourceLockInfo>? GetClassResourceLocks()
    {
        if (!_classResourceLocksComputed)
        {
            _classResourceLocks = ReadResourceLocks(_type);
            _classResourceLocksComputed = true;
        }

        return _classResourceLocks;
    }

    /// <summary>
    /// Reads the <c>[ResourceLock]</c> attributes declared directly on <paramref name="attributeProvider"/>
    /// (a class or a method), in declaration order. Returns <see langword="null"/> when none are present.
    /// </summary>
    private List<ResourceLockInfo>? ReadResourceLocks(ICustomAttributeProvider attributeProvider)
    {
        List<ResourceLockInfo>? locks = null;
        foreach (ResourceLockAttribute attribute in _reflectHelper.GetAttributes<ResourceLockAttribute>(attributeProvider))
        {
            (locks ??= []).Add(new ResourceLockInfo(attribute.Resource, attribute.Mode));
        }

        return locks;
    }

    /// <summary>
    /// Reads (and caches for this type) the <c>[DependsOn]</c> attributes declared on the test class.
    /// </summary>
    private List<TestDependencyInfo>? GetClassDependencies()
    {
        if (!_classDependenciesComputed)
        {
            _classDependencies = ReadDependencies(_type);
            _classDependenciesComputed = true;
        }

        return _classDependencies;
    }

    /// <summary>
    /// Reads the <c>[DependsOn]</c> attributes declared directly on <paramref name="attributeProvider"/>
    /// (a class or a method), in declaration order. Returns <see langword="null"/> when none are present.
    /// </summary>
    private List<TestDependencyInfo>? ReadDependencies(ICustomAttributeProvider attributeProvider)
    {
        List<TestDependencyInfo>? dependencies = null;
        foreach (DependsOnAttribute attribute in _reflectHelper.GetAttributes<DependsOnAttribute>(attributeProvider))
        {
            // A type reference is resolved to its CLR full name here, at discovery, because the graph is
            // rebuilt at execution time - possibly in another app domain - where the Type is no longer
            // available. FullName matches UnitTestElement.TestMethod.FullClassName, which TypeEnumerator
            // also derives from Type.FullName.
            (dependencies ??= []).Add(new TestDependencyInfo(
                attribute.TestClass?.FullName,
                attribute.TestMethodName,
                attribute.ProceedOnFailure));
        }

        return dependencies;
    }

    /// <summary>
    /// Merges the class-level and method-level dependencies into a single distinct set, preserving
    /// declaration order (class first). When the same prerequisite is declared more than once, the
    /// <c>ProceedOnFailure</c> flags are merged conservatively - the edge proceeds past a failed
    /// prerequisite only when every declaration of it says so - matching the rule applied across distinct
    /// prerequisites in <c>TestDependencyGraph.ResolveEdges</c>. Returns <see langword="null"/> when neither
    /// declares any dependency.
    /// </summary>
    private static TestDependencyInfo[]? MergeDependencies(List<TestDependencyInfo>? classDependencies, List<TestDependencyInfo>? methodDependencies, string declaringClassFullName, string methodName)
    {
        if (classDependencies is null && methodDependencies is null)
        {
            return null;
        }

        var result = new List<TestDependencyInfo>();
        var indexByTarget = new Dictionary<string, int>(StringComparer.Ordinal);

        // A class-level [DependsOn(nameof(Setup))] is expanded onto every method of the class, including
        // Setup itself. The user wrote "every *other* test waits for Setup", never "Setup waits for
        // itself", so that generated self-edge is dropped here. A self reference written directly on the
        // method is kept, because there the user really did name the test they were annotating, and it
        // surfaces as a cycle.
        AddAll(classDependencies, result, indexByTarget, declaringClassFullName, methodName);
        AddAll(methodDependencies, result, indexByTarget, declaringClassFullName, methodName: null);

        return result.Count == 0 ? null : [.. result];

        static void AddAll(List<TestDependencyInfo>? source, List<TestDependencyInfo> target, Dictionary<string, int> indexByTarget, string declaringClassFullName, string? methodName)
        {
            if (source is null)
            {
                return;
            }

            foreach (TestDependencyInfo dependency in source)
            {
                if (methodName is not null
                    && string.Equals(dependency.TargetMethodName, methodName, StringComparison.Ordinal)
                    && (dependency.TargetClassFullName is null || string.Equals(dependency.TargetClassFullName, declaringClassFullName, StringComparison.Ordinal)))
                {
                    continue;
                }

                string key = dependency.DescribeTarget();
                if (indexByTarget.TryGetValue(key, out int existingIndex))
                {
                    // Conservative merge: one declaration asking for the ordinary skip is enough to hold the
                    // dependent back, which is the same rule ResolveEdges applies across distinct
                    // prerequisites. Merging the other way would let a class-level ProceedOnFailure silently
                    // override a method-level default and run a test whose precondition demonstrably did not
                    // hold; over-skipping only costs coverage that was already compromised.
                    if (!dependency.ProceedOnFailure && target[existingIndex].ProceedOnFailure)
                    {
                        target[existingIndex] = dependency;
                    }
                }
                else
                {
                    indexByTarget[key] = target.Count;
                    target.Add(dependency);
                }
            }
        }
    }

    /// <summary>
    /// Merges the class-level and method-level resource locks into a single distinct set (ordinal, case-sensitive),
    /// keeping the strongest mode per key (<see cref="ResourceAccessMode.ReadWrite"/> wins over
    /// <see cref="ResourceAccessMode.Read"/>) and sorting ordinally by resource so acquisition order is stable.
    /// Returns <see langword="null"/> when neither declares any lock.
    /// </summary>
    private static ResourceLockInfo[]? MergeResourceLocks(List<ResourceLockInfo>? classLocks, List<ResourceLockInfo>? methodLocks)
    {
        if (classLocks is null && methodLocks is null)
        {
            return null;
        }

        var map = new Dictionary<string, ResourceAccessMode>(StringComparer.Ordinal);
        AddAll(classLocks, map);
        AddAll(methodLocks, map);

        var result = new List<ResourceLockInfo>(map.Count);
        foreach (KeyValuePair<string, ResourceAccessMode> entry in map)
        {
            result.Add(new ResourceLockInfo(entry.Key, entry.Value));
        }

        result.Sort(static (left, right) => string.CompareOrdinal(left.Resource, right.Resource));
        return [.. result];

        static void AddAll(List<ResourceLockInfo>? source, Dictionary<string, ResourceAccessMode> target)
        {
            if (source is null)
            {
                return;
            }

            foreach (ResourceLockInfo resourceLock in source)
            {
                if (target.TryGetValue(resourceLock.Resource, out ResourceAccessMode existingMode))
                {
                    if (resourceLock.Mode == ResourceAccessMode.ReadWrite)
                    {
                        target[resourceLock.Resource] = ResourceAccessMode.ReadWrite;
                    }
                }
                else
                {
                    target[resourceLock.Resource] = resourceLock.Mode;
                }
            }
        }
    }
}
