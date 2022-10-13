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
        Verify(RunEventsHandler.PassedTests.Count == 15);  // the inherit class tests are called twice

        int assemblyInitCalledCount = 0;
        int classCleanupCalledCount = 0;
        int assemblyCleanupCalledCount = 0;

        foreach (var testMethod in RunEventsHandler.PassedTests)
        {
            Verify(testMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
            var isTestMethodMessageContains = (string s) => { return testMethod.Messages.Single().Text.Contains(s); };
            assemblyInitCalledCount += isTestMethodMessageContains("AssemblyInit was called") ? 1 : 0;
            classCleanupCalledCount += isTestMethodMessageContains("ClassCleanup was called") ? 1 : 0;
            assemblyCleanupCalledCount += isTestMethodMessageContains("AssemblyCleanup was called") ? 1 : 0;
        }

        Verify(assemblyInitCalledCount == 1);

        // Assembly and Class cleanup don't appear in the logs because they happen after retrieving the result.
        // TODO: https://github.com/microsoft/testfx/issues/1328
        Verify(classCleanupCalledCount == 0);
        Verify(assemblyCleanupCalledCount == 0);

        var caseClassCleanupWithCleanupBehaviorEndOfAssembly = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfAssembly"));
        Verify(caseClassCleanupWithCleanupBehaviorEndOfAssembly.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Contains(
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

        var caseClassCleanupWithCleanupBehaviorEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfClass"));
        Verify(caseClassCleanupWithCleanupBehaviorEndOfClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Contains(
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

        var caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone"));
        Verify(caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Contains(
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

        var caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass"));
        Verify(caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
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

        var caseClassCleanupWithNoProperty = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithNoProperty"));
        Verify(caseClassCleanupWithNoProperty.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassCleanupWithNoProperty.Messages.Single().Text.Contains(
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

        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        Verify(caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
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
        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Contains(
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

        var caseInheritClassWithCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        Verify(caseInheritClassWithCleanupInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Contains(
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
        var caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.TestMethod"));
        Verify(caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Contains(
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

        var caseClassInitializeAndCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone.TestMethod"));
        Verify(caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Contains(
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

        var caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
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

        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Contains(
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
        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.TestMethod"));
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Contains(
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

        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Contains(
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
        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        Verify(caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Contains(
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
