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

        int numberOfLines;
        var caseClassCleanupWithCleanupBehaviorEndOfAssembly = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfAssembly"));
        numberOfLines = caseClassCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 12 : 11)); // The number of the logs + 3 empty lines + 1 for the logs' header.
        caseClassCleanupWithCleanupBehaviorEndOfAssembly.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithCleanupBehaviorEndOfAssembly.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            AssemblyInit was called
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
            AssemblyInit was called
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassCleanupWithCleanupBehaviorEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithCleanupBehaviorEndOfClass"));
        numberOfLines = caseClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 12 : 11)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassCleanupWithCleanupBehaviorEndOfClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithCleanupBehaviorEndOfClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            ClassCleanup.EndOfClass was called
            """
            :
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            ClassCleanup.EndOfClass was called
            """);

        var caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone"));
        numberOfLines = caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 11 : 10)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """
            :
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass"));
        numberOfLines = caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 11 : 10)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeWithInheritanceBehaviorNoneAndClassCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorNone was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            ClassInitialize.InheritanceBehaviorNone was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseClassCleanupWithNoProperty = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassCleanupWithNoProperty"));
        numberOfLines = caseClassCleanupWithNoProperty.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 11 : 10)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassCleanupWithNoProperty.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupWithNoProperty.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            ClassInitialize was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 15 : 14)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassInitialize was called
            Base ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """
            :
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassInitialize was called
            Base ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            
            """);

        // Test the parent test method.
        var caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Base ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            Base ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            """);

        var caseInheritClassWithCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 14 : 13)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            
            """);

        // Test the parent test method.
        var caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 25 : 24)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            Base ClassCleanup.InheritanceBehaviorNone was called
            Current ClassCleanup was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            ClassCleanup.WithNoProperty was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassCleanup was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassCleanup was called
            Base ClassCleanup.InheritanceBehaviorNone was called
            Current ClassCleanup was called
            ClassCleanup.EndOfAssembly was called
            AssemblyCleanup was called
            """ : """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            Base ClassCleanup.InheritanceBehaviorNone was called
            Current ClassCleanup was called
            Current ClassCleanup was called
            ClassCleanup.WithNoProperty was called
            Current ClassCleanup was called
            Base ClassCleanup.InheritanceBehaviorNone was called
            Current ClassCleanup was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            ClassCleanup.EndOfAssembly was called
            Base ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called
            AssemblyCleanup was called
            """);

        var caseClassInitializeAndCleanupWithInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 11 : 10)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupWithInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorNone was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            ClassInitialize.InheritanceBehaviorNone was called
            Ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was calle
            """);

        var caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 11 : 10)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            DisposeAsync was called
            Dispose was called
            """ : """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            ctor was called
            TestInitialize was called
            TestMethod was called
            TestCleanup was called
            Dispose was called
            """);

        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 15 : 14)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone.TestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone_ParentTestMethod.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            """);

        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.DerivedClassTestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 14 : 13)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            Current ClassInitialize was called
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Current TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            """);

        // Test the parent test method.
        var caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass.TestMethod"));
        numberOfLines = caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 13 : 12)); // The number of the logs + 3 empety lines + 1 for the logs' header.
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseInheritClassWithClassInitializeInheritanceBehaviorNoneAndClassCleanupInheritanceBehaviorBeforeEachDerivedClass_ParentTestMethod.Messages.Single().Text.Should().Contain(
            targetFramework == "net6.0" ?
            """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base DisposeAsync was called
            Base Dispose was called
            """ : """
            Base Ctor was called
            Current Ctor was called
            Base TestInitialize was called
            Current TestInitialize was called
            Base TestMethod was called
            Current TestCleanup was called
            Base TestCleanup was called
            Base Dispose was called
            """);
    }
}
