// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ClassCleanupManager
{
    private readonly ClassCleanupBehavior _lifecycleFromMsTestOrAssembly;
    private readonly ReflectHelper _reflectHelper;
    private readonly ConcurrentDictionary<string, int> _remainingTestCountsByClass;

    public ClassCleanupManager(
        IEnumerable<UnitTestElement> testsToRun,
        ClassCleanupBehavior lifecycleFromMsTestOrAssembly,
        ReflectHelper reflectHelper)
    {
        IEnumerable<UnitTestElement> runnableTests = testsToRun.Where(t => t.Traits is null || !t.Traits.Any(t => t.Name == EngineConstants.FixturesTestTrait));
        _remainingTestCountsByClass =
            new(runnableTests.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()));
        _lifecycleFromMsTestOrAssembly = lifecycleFromMsTestOrAssembly;
        _reflectHelper = reflectHelper;
    }

    public bool ShouldRunEndOfAssemblyCleanup { get; private set; }

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
                    ClassCleanupBehavior cleanupLifecycle = GetClassCleanupBehavior(testMethodInfo.Parent);

                    shouldRunEndOfClassCleanup = cleanupLifecycle == ClassCleanupBehavior.EndOfClass;
                }
            }

            ShouldRunEndOfAssemblyCleanup = _remainingTestCountsByClass.IsEmpty;
        }
    }

    /// <summary>
    /// Gets the class cleanup lifecycle for the class, if set.
    /// </summary>
    /// <param name="classInfo">The class to inspect.</param>
    /// <returns>Returns <see cref="ClassCleanupBehavior"/> if provided, otherwise <c>null</c>.</returns>
    private ClassCleanupBehavior GetClassCleanupBehavior(TestClassInfo classInfo)
    {
        // TODO: not discovery related but seems expensive and unnecessary, because we do inheritance lookup, and to put the method into the stack we've already did this lookup before?
        DebugEx.Assert(classInfo.HasExecutableCleanupMethod, "'GetClassCleanupBehavior' should only be called if 'HasExecutableCleanupMethod' is true");

        bool hasEndOfAssembly = false;
        if (classInfo.ClassCleanupMethod is not null)
        {
            ClassCleanupBehavior? cleanupBehavior = _reflectHelper.GetFirstAttributeOrDefault<ClassCleanupAttribute>(classInfo.ClassCleanupMethod, inherit: true)?.CleanupBehavior;
            if (cleanupBehavior == ClassCleanupBehavior.EndOfClass)
            {
                return ClassCleanupBehavior.EndOfClass;
            }
            else if (cleanupBehavior == ClassCleanupBehavior.EndOfAssembly)
            {
                hasEndOfAssembly = true;
            }
        }

        foreach (MethodInfo baseClassCleanupMethod in classInfo.BaseClassCleanupMethods)
        {
            ClassCleanupBehavior? cleanupBehavior = _reflectHelper.GetFirstAttributeOrDefault<ClassCleanupAttribute>(baseClassCleanupMethod, inherit: true)?.CleanupBehavior;
            if (cleanupBehavior == ClassCleanupBehavior.EndOfClass)
            {
                return ClassCleanupBehavior.EndOfClass;
            }
            else if (cleanupBehavior == ClassCleanupBehavior.EndOfAssembly)
            {
                hasEndOfAssembly = true;
            }
        }

        return hasEndOfAssembly ? ClassCleanupBehavior.EndOfAssembly : _lifecycleFromMsTestOrAssembly;
    }

    internal static void ForceCleanup(TypeCache typeCache, IDictionary<string, object?> sourceLevelParameters, IMessageLogger logger)
    {
        using var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "context");
        TestContext testContext = new TestContextImplementation(null, writer, sourceLevelParameters, logger, testRunCancellationToken: null);
        IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
        LogMessageListener? listener = null;
        foreach (TestClassInfo classInfo in classInfoCache)
        {
            TestFailedException? ex = classInfo.ExecuteClassCleanup(testContext, out listener);
            if (ex is not null)
            {
                throw ex;
            }
        }

        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanup(testContext, ref listener);
            if (ex is not null)
            {
                throw ex;
            }
        }
    }
}
