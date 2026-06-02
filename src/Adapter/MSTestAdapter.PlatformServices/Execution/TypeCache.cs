// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Security;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines type cache which reflects upon a type and cache its test artifacts.
/// </summary>
internal sealed partial class TypeCache
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
}
