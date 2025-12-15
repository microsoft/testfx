// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class ClassCleanupManagerTests : TestContainer
{
    public void AssemblyCleanupRunsAfterAllTestsFinishEvenIfWeScheduleTheSameTestMultipleTime()
    {
        ReflectHelper reflectHelper = Mock.Of<ReflectHelper>();
        MethodInfo methodInfo = typeof(FakeTestClass).GetMethod(nameof(FakeTestClass.FakeTestMethod), BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo classCleanupMethodInfo = typeof(FakeTestClass).GetMethod(nameof(FakeTestClass.FakeClassCleanupMethod), BindingFlags.Instance | BindingFlags.NonPublic)!;
        // Full class name must agree between unitTestElement.TestMethod.FullClassName and testMethod.FullClassName;
        string fullClassName = methodInfo.DeclaringType!.FullName!;
        TestMethod testMethod = new(nameof(FakeTestClass.FakeTestMethod), fullClassName, typeof(FakeTestClass).Assembly.FullName!, displayName: null);

        // Setting 2 of the same test to run, we should run assembly cleanup after both these tests
        // finish, not after the first one finishes.
        List<UnitTestElement> testsToRun =
        [
            new(testMethod),
            new(testMethod)
        ];

        var classCleanupManager = new ClassCleanupManager(testsToRun);

        TestClassInfo testClassInfo = new(typeof(FakeTestClass), null!, true, new TestClassAttribute(), null!)
        {
            // This needs to be set, to allow running class cleanup.
            ClassCleanupMethod = classCleanupMethodInfo,
        };
        TestMethodInfo testMethodInfo = new(methodInfo, testClassInfo, null!);
        classCleanupManager.MarkTestComplete(testMethodInfo, out bool shouldRunEndOfClassCleanup);

        // The cleanup should not run here yet, we have 1 remaining test to run.
        shouldRunEndOfClassCleanup.Should().BeFalse();
        classCleanupManager.ShouldRunEndOfAssemblyCleanup.Should().BeFalse();

        classCleanupManager.MarkTestComplete(testMethodInfo, out shouldRunEndOfClassCleanup);
        // The cleanup should run here.
        shouldRunEndOfClassCleanup.Should().BeTrue();
        classCleanupManager.ShouldRunEndOfAssemblyCleanup.Should().BeTrue();
    }

    [TestClass]
    private class FakeTestClass
    {
        [TestMethod]
        internal void FakeTestMethod()
        {
        }

        [ClassCleanup]
        internal void FakeClassCleanupMethod()
        {
        }
    }
}
