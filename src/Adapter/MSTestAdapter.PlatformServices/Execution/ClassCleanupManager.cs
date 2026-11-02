// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using AdapterApplicationStateGuard = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.ApplicationStateGuard;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ClassCleanupManager
{
    private readonly ConcurrentDictionary<string, int> _remainingTestCountsByClass;

    public ClassCleanupManager(IEnumerable<UnitTestElement> testsToRun)
    {
        _remainingTestCountsByClass =
            new(testsToRun.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()));
    }

    public bool ShouldRunEndOfAssemblyCleanup => _remainingTestCountsByClass.IsEmpty;

    public void MarkTestComplete(TestMethod testMethod, out bool isLastTestInClass)
    {
        lock (_remainingTestCountsByClass)
        {
            if (!_remainingTestCountsByClass.TryGetValue(testMethod.FullClassName, out int remainingCount))
            {
                throw AdapterApplicationStateGuard.Unreachable();
            }

            remainingCount--;
            _remainingTestCountsByClass[testMethod.FullClassName] = remainingCount;
            isLastTestInClass = remainingCount == 0;
        }
    }

    public void MarkClassComplete(string fullClassName)
    {
        lock (_remainingTestCountsByClass)
        {
            if (!_remainingTestCountsByClass.TryRemove(fullClassName, out int remainingTests) ||
                remainingTests != 0)
            {
                // We failed to remove the class, or we are incorrectly marking the class as complete while there are remaining tests.
                // This should never happen.
                throw AdapterApplicationStateGuard.Unreachable();
            }
        }
    }

    internal static void ForceCleanup(TypeCache typeCache, IDictionary<string, object?> sourceLevelParameters, IAdapterMessageLogger logger)
    {
        IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
        foreach (TestClassInfo classInfo in classInfoCache)
        {
            var testContext = new TestContextImplementation(null, classInfo.ClassType.FullName, sourceLevelParameters, logger, testRunCancellationToken: null);

            // Flow properties set during AssemblyInitialize and ClassInitialize so the
            // ClassCleanup method observes them when invoked via the fallback path.
            testContext.MergeProperties(classInfo.Parent.PostAssemblyInitProperties);
            testContext.MergeProperties(classInfo.PostClassInitProperties);

            TestFailedException? ex = classInfo.ExecuteClassCleanupAsync(testContext).GetAwaiter().GetResult();
            if (ex is not null)
            {
                throw ex;
            }
        }

        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            var testContext = new TestContextImplementation(null, null, sourceLevelParameters, logger, testRunCancellationToken: null);

            // Flow properties set during AssemblyInitialize so the AssemblyCleanup method observes
            // them when invoked via the fallback path.
            testContext.MergeProperties(assemblyInfo.PostAssemblyInitProperties);

            TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanupAsync(testContext).GetAwaiter().GetResult();
            if (ex is not null)
            {
                throw ex;
            }
        }
    }
}
