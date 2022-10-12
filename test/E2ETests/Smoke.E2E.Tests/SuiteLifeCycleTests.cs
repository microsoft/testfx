// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class SuiteLifeCycleTests : CLITestBase
{
    private const string Assembly = "SuiteLifeCycleTestProject.dll";

    public void ValidateTestRunLifecycle_net6()
    {
        ValidateTestRunLifecycle("net6.0");
    }

    public void ValidateTestRunLifecycle_net462()
    {
        ValidateTestRunLifecycle("net462");
    }

    private void ValidateTestRunLifecycle(string targetFramework)
    {
        InvokeVsTestForExecution(new[] { targetFramework + "\\" + Assembly }, targetFramework: targetFramework);
        Verify(RunEventsHandler.PassedTests.Count == 15);  // the inherite class tests called twice

        int noOfassemblyInitCalled = 0;
        bool isClassCleanupCalled = false;
        bool isAssemblyCleanupCalled = false;

        foreach (var testMethod in RunEventsHandler.PassedTests)
        {
            Verify(testMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);

            noOfassemblyInitCalled += testMethod.Messages.Single().Text.Contains("AssemblyInit was called") ? 1 : 0;
            isClassCleanupCalled |= testMethod.Messages.Single().Text.Contains("ClassCleanup was called");
            isAssemblyCleanupCalled |= testMethod.Messages.Single().Text.Contains("AssemblyCleanup was called");
        }

        Verify(noOfassemblyInitCalled == 1);

        // Assembly and Class cleanup doesn't appear in the logs because it's happing after retrieving the result.
        Verify(!isClassCleanupCalled);
        Verify(!isAssemblyCleanupCalled);

        var case_CalssCleanupWithCleanupBehaviorEndOfAssembly = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfAssembly"));
        Verify(case_CalssCleanupWithCleanupBehaviorEndOfAssembly.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_CalssCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassCleanupWithCleanupBehaviorEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfClass"));
        Verify(case_ClassCleanupWithCleanupBehaviorEndOfClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone"));
        Verify(case_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass"));
        Verify(case_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassCleanupWithNoProperty = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithNoProperty"));
        Verify(case_ClassCleanupWithNoProperty.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassCleanupWithNoProperty.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        Verify(case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        // Test the parent test method.
        var case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        var case_InheritClassWithCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        Verify(case_InheritClassWithCleanupInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        // Test the parent test method.
        var case_InheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.TestMethod"));
        Verify(case_InheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassInitializeAndCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone.TestMethod"));
        Verify(case_ClassInitializeAndCleanupWithInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(case_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """));

        var case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        // Test the parent test method.
        var case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.TestMethod"));
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        var case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Derived ClassInitialize was called
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));

        // Test the parent test method.
        var case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(case_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Contains(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """));
    }
}
