// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ClassCleanupManager
{
    private readonly ConcurrentDictionary<string, int> _remainingTestCountsByClass;

    public ClassCleanupManager(IEnumerable<UnitTestElement> testsToRun)
    {
        IEnumerable<UnitTestElement> runnableTests = testsToRun.Where(t => t.Traits is null || !t.Traits.Any(t => t.Name == EngineConstants.FixturesTestTrait));
        _remainingTestCountsByClass =
            new(runnableTests.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()));
    }

    public bool ShouldRunEndOfAssemblyCleanup => _remainingTestCountsByClass.IsEmpty;

    public void MarkTestComplete(TestMethodInfo testMethodInfo, out bool shouldRunEndOfClassCleanup)
    {
        shouldRunEndOfClassCleanup = false;

        lock (_remainingTestCountsByClass)
        {
            if (!_remainingTestCountsByClass.TryGetValue(testMethodInfo.TestClassName, out int remainingCount))
            {
                return;
            }

            remainingCount--;
            _remainingTestCountsByClass[testMethodInfo.TestClassName] = remainingCount;
            if (remainingCount == 0)
            {
                _remainingTestCountsByClass.TryRemove(testMethodInfo.TestClassName, out _);
                if (testMethodInfo.Parent.HasExecutableCleanupMethod)
                {
                    shouldRunEndOfClassCleanup = true;
                }
            }
        }
    }

    internal static void ForceCleanup(TypeCache typeCache, IDictionary<string, object?> sourceLevelParameters, IMessageLogger logger)
    {
        IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
        foreach (TestClassInfo classInfo in classInfoCache)
        {
            TestContext testContext = new TestContextImplementation(null, classInfo.ClassType.FullName, sourceLevelParameters, logger, testRunCancellationToken: null);
            TestFailedException? ex = classInfo.ExecuteClassCleanup(testContext);
            if (ex is not null)
            {
                throw ex;
            }
        }

        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            TestContext testContext = new TestContextImplementation(null, null, sourceLevelParameters, logger, testRunCancellationToken: null);
            TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanup(testContext);
            if (ex is not null)
            {
                throw ex;
            }
        }
    }
}
