// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class ClassCleanupManagerTests : TestContainer
{
    public void AssemblyCleanupRunsAfterAllTestsFinishEvenIfWeScheduleTheSameTestMultipleTime()
    {
        ReflectHelper reflectHelper = Mock.Of<ReflectHelper>();
        MethodInfo methodInfo = typeof(ClassCleanupManagerTests).GetMethod(nameof(FakeTestMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo classCleanupMethodInfo = typeof(ClassCleanupManagerTests).GetMethod(nameof(FakeClassCleanupMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        // Full class name must agree between unitTestElement.TestMethod.FullClassName and testMethod.FullClassName;
        string fullClassName = methodInfo.DeclaringType.FullName;
        TestMethod testMethod = new(nameof(FakeTestMethod), fullClassName, typeof(ClassCleanupManagerTests).Assembly.FullName, isAsync: false);

        // Setting 2 of the same test to run, we should run assembly cleanup after both these tests
        // finish, not after the first one finishes.
        List<UnitTestElement> testsToRun = new()
        {
            new(testMethod),
            new(testMethod),
        };

        var classCleanupManager = new ClassCleanupManager(testsToRun, ClassCleanupBehavior.EndOfClass, ClassCleanupBehavior.EndOfClass, reflectHelper);

        TestClassInfo testClassInfo = new(typeof(ClassCleanupManagerTests), null, true, null, null, null)
        {
            // This needs to be set, to allow running class cleanup.
            ClassCleanupMethod = classCleanupMethodInfo,
        };
        TestMethodInfo testMethodInfo = new(methodInfo, testClassInfo, null!);
        classCleanupManager.MarkTestComplete(testMethodInfo, testMethod, out bool shouldRunEndOfClassCleanup);

        // The cleanup should not run here yet, we have 1 remaining test to run.
        Assert.IsFalse(shouldRunEndOfClassCleanup);
        Assert.IsFalse(classCleanupManager.ShouldRunEndOfAssemblyCleanup);

        classCleanupManager.MarkTestComplete(testMethodInfo, testMethod, out shouldRunEndOfClassCleanup);
        // The cleanup should run here.
        Assert.IsTrue(shouldRunEndOfClassCleanup);
        Assert.IsTrue(classCleanupManager.ShouldRunEndOfAssemblyCleanup);
    }

    private void FakeTestMethod()
    {
    }

    private void FakeClassCleanupMethod()
    {
    }
}
