// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using FluentAssertions;

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
        RunEventsHandler.PassedTests.Should().HaveCount(15);  // The inherit class tests are called twice.

        int assemblyInitCalledCount = 0;
        int classInitCalledCount = 0;
        int classCleanupCalledCount = 0;
        int assemblyCleanupCalledCount = 0;

        foreach (var testMethod in RunEventsHandler.PassedTests)
        {
            testMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
            var isTestMethodMessageContains = (string s) => testMethod.Messages.Single().Text.Contains(s);
            assemblyInitCalledCount += isTestMethodMessageContains("AssemblyInit was called") ? 1 : 0;
            classCleanupCalledCount += isTestMethodMessageContains("ClassCleanup was called") ? 1 : 0;
            assemblyCleanupCalledCount += isTestMethodMessageContains("AssemblyCleanup was called") ? 1 : 0;
            classInitCalledCount += isTestMethodMessageContains("ClassInitialize was called") ? 1 : 0;
        }

        // Assembly and Class init/cleanup logs don't appear in the tests' results.
        classCleanupCalledCount.Should().Be(0);
        assemblyCleanupCalledCount.Should().Be(0);
        assemblyInitCalledCount.Should().Be(0);
        classInitCalledCount.Should().Be(0);

        int numberOfLines;
        var caseClassCleanupWithCleanupBehaviorEndOfAssembly = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfAssembly"));
        numberOfLines = caseClassCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassCleanupWithCleanupBehaviorEndOfAssembly.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassCleanupWithCleanupBehaviorEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfClass"));
        numberOfLines = caseClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassCleanupWithCleanupBehaviorEndOfClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone"));
        numberOfLines = caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass"));
        numberOfLines = caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassCleanupWithNoProperty = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithNoProperty"));
        numberOfLines = caseClassCleanupWithNoProperty.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassCleanupWithNoProperty.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithNoProperty.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
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
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Should().Contain(
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
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Should().Contain(
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
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassInitializeAndCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 10 : 9)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Should().Contain(
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
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            Derived class TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Should().Contain(
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
            """ : """
            Ctor was called
            Derived class Ctor was called
            TestInitialize was called
            Derived class TestInitialize was called
            TestMethod was called
            Derived class TestCleanup was called
            TestCleanup was called
            Dispose was called
            """);
    }
}
