// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ClassCleanupManager
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly Dictionary<string, int> _remainingTestCountsByClass;

    public ClassCleanupManager(IEnumerable<UnitTestElement> testsToRun)
    {
        _remainingTestCountsByClass =
            testsToRun.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());
    }

    public bool ShouldRunEndOfAssemblyCleanup
    {
        get
        {
            lock (_lock)
            {
                return _remainingTestCountsByClass.Count == 0;
            }
        }
    }

    public void MarkTestComplete(TestMethod testMethod, out bool isLastTestInClass)
    {
        lock (_lock)
        {
#if NET8_0_OR_GREATER
            ref int remainingCount = ref CollectionsMarshal.GetValueRefOrNullRef(
                _remainingTestCountsByClass, testMethod.FullClassName);
            if (Unsafe.IsNullRef(ref remainingCount))
            {
                throw ApplicationStateGuard.Unreachable();
            }

            remainingCount--;
            isLastTestInClass = remainingCount == 0;
#else
            if (!_remainingTestCountsByClass.TryGetValue(testMethod.FullClassName, out int remainingCount))
            {
                throw ApplicationStateGuard.Unreachable();
            }

            remainingCount--;
            _remainingTestCountsByClass[testMethod.FullClassName] = remainingCount;
            isLastTestInClass = remainingCount == 0;
#endif
        }
    }

    public void MarkClassComplete(string fullClassName)
    {
        lock (_lock)
        {
            if (!_remainingTestCountsByClass.TryGetValue(fullClassName, out int remainingTests) ||
                remainingTests != 0)
            {
                // We failed to find the class, or we are incorrectly marking the class as complete while there are remaining tests.
                // This should never happen.
                throw ApplicationStateGuard.Unreachable();
            }

            _remainingTestCountsByClass.Remove(fullClassName);
        }
    }

    internal static void ForceCleanup(TypeCache typeCache, IDictionary<string, object?> sourceLevelParameters, IMessageLogger logger)
    {
        IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
        foreach (TestClassInfo classInfo in classInfoCache)
        {
            TestContext testContext = new TestContextImplementation(null, classInfo.ClassType.FullName, sourceLevelParameters, logger, testRunCancellationToken: null);
            TestFailedException? ex = classInfo.ExecuteClassCleanupAsync(testContext).GetAwaiter().GetResult();
            if (ex is not null)
            {
                throw ex;
            }
        }

        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            TestContext testContext = new TestContextImplementation(null, null, sourceLevelParameters, logger, testRunCancellationToken: null);
            TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanupAsync(testContext).GetAwaiter().GetResult();
            if (ex is not null)
            {
                throw ex;
            }
        }
    }
}
