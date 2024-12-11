// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ClassCleanupManager
{
    private readonly ClassCleanupBehavior? _lifecycleFromMsTest;
    private readonly ClassCleanupBehavior _lifecycleFromAssembly;
    private readonly ReflectHelper _reflectHelper;
    private readonly ConcurrentDictionary<string, List<string>> _remainingTestsByClass;

    public ClassCleanupManager(
        IEnumerable<UnitTestElement> testsToRun,
        ClassCleanupBehavior? lifecycleFromMsTest,
        ClassCleanupBehavior lifecycleFromAssembly,
        ReflectHelper reflectHelper)
    {
        IEnumerable<UnitTestElement> runnableTests = testsToRun.Where(t => t.Traits is null || !t.Traits.Any(t => t.Name == Constants.FixturesTestTrait));
        _remainingTestsByClass =
            new(runnableTests.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => new List<string>(g.Select(t => t.TestMethod.UniqueName))));
        _lifecycleFromMsTest = lifecycleFromMsTest;
        _lifecycleFromAssembly = lifecycleFromAssembly;
        _reflectHelper = reflectHelper;
    }

    public bool ShouldRunEndOfAssemblyCleanup { get; private set; }

    public void MarkTestComplete(TestMethodInfo testMethodInfo, TestMethod testMethod, out bool shouldRunEndOfClassCleanup)
    {
        shouldRunEndOfClassCleanup = false;
        if (!_remainingTestsByClass.TryGetValue(testMethodInfo.TestClassName, out List<string>? testsByClass))
        {
            return;
        }

        lock (testsByClass)
        {
            testsByClass.Remove(testMethod.UniqueName);
            if (testsByClass.Count == 0)
            {
                _remainingTestsByClass.TryRemove(testMethodInfo.TestClassName, out _);
                if (testMethodInfo.Parent.HasExecutableCleanupMethod)
                {
                    ClassCleanupBehavior cleanupLifecycle = _reflectHelper.GetClassCleanupBehavior(testMethodInfo.Parent)
                        ?? _lifecycleFromMsTest
                        ?? _lifecycleFromAssembly;

                    shouldRunEndOfClassCleanup = cleanupLifecycle == ClassCleanupBehavior.EndOfClass;
                }
            }

            ShouldRunEndOfAssemblyCleanup = _remainingTestsByClass.IsEmpty;
        }
    }

    internal static void ForceCleanup(TypeCache typeCache)
    {
        IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
        foreach (TestClassInfo classInfo in classInfoCache)
        {
            classInfo.ExecuteClassCleanup();
        }

        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            assemblyInfo.ExecuteAssemblyCleanup();
        }
    }
}
