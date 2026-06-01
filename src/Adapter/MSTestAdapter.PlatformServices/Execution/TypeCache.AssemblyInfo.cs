// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    #region AssemblyInfo creation and cache logic.

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
}
