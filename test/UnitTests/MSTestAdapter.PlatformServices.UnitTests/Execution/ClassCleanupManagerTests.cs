// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class ClassCleanupManagerTests : TestContainer
{
    public void AssemblyCleanupRunsAfterAllTestsFinishEvenIfWeScheduleTheSameTestMultipleTime()
    {
        MethodInfo classCleanupMethodInfo = typeof(FakeTestClass).GetMethod(nameof(FakeTestClass.FakeClassCleanupMethod), BindingFlags.Instance | BindingFlags.NonPublic)!;
        // Full class name must agree between unitTestElement.TestMethod.FullClassName and testMethod.FullClassName;
        string fullClassName = typeof(FakeTestClass).FullName!;
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
        classCleanupManager.MarkTestComplete(testMethod, out bool shouldRunEndOfClassCleanup);

        // The cleanup should not run here yet, we have 1 remaining test to run.
        shouldRunEndOfClassCleanup.Should().BeFalse();
        classCleanupManager.ShouldRunEndOfAssemblyCleanup.Should().BeFalse();

        classCleanupManager.MarkTestComplete(testMethod, out shouldRunEndOfClassCleanup);
        classCleanupManager.MarkClassComplete(fullClassName);
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

    public void MultipleClassesRequireExplicitClassCompletionBeforeAssemblyCleanup()
    {
        var first = new TestMethod("One", "First", "source.dll", null);
        var second = new TestMethod("Two", "Second", "source.dll", null);
        var manager = new ClassCleanupManager([new(first), new(first), new(second)]);

        manager.MarkTestComplete(first, out bool firstDone);
        firstDone.Should().BeFalse();
        manager.MarkTestComplete(first, out firstDone);
        firstDone.Should().BeTrue();
        manager.ShouldRunEndOfAssemblyCleanup.Should().BeFalse();
        manager.MarkClassComplete("First");
        manager.ShouldRunEndOfAssemblyCleanup.Should().BeFalse();
        manager.MarkTestComplete(second, out bool secondDone);
        secondDone.Should().BeTrue();
        manager.ShouldRunEndOfAssemblyCleanup.Should().BeFalse();
        manager.MarkClassComplete("Second");
        manager.ShouldRunEndOfAssemblyCleanup.Should().BeTrue();
    }

    public void InvalidCompletionTransitionsFailWithTheExactInvariantKind()
    {
        var method = new TestMethod("One", "First", "source.dll", null);
        var manager = new ClassCleanupManager([new(method)]);
        Action unknownTest = () => manager.MarkTestComplete(new TestMethod("Other", "Unknown", "source.dll", null), out _);
        Action unknownClass = () => manager.MarkClassComplete("Unknown");
        Action incompleteClass = () => manager.MarkClassComplete("First");
        foreach (Action invalid in new[] { unknownTest, unknownClass, incompleteClass })
        {
            Exception failure = invalid.Should().Throw<Exception>().Which;
            failure.GetType().FullName.Should().Be("System.Diagnostics.UnreachableException");
            failure.Message.Should().Contain("ClassCleanupManager.cs");
        }
    }
}
