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
        RunEventsHandler.PassedTests.Should().HaveCount(27);  // The inherit class tests are called twice.

        var caseClassCleanup = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanup.TestMethod"));
        caseClassCleanup.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanup.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            AssemblyInit was called
            LifeCycleClassCleanup.ClassInitialize was called
            LifeCycleClassCleanup.ctor was called
            LifeCycleClassCleanup.TestInitialize was called
            LifeCycleClassCleanup.TestMethod was called
            LifeCycleClassCleanup.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanup.DisposeAsync was called\r\nLifeCycleClassCleanup.Dispose was called"
                : "LifeCycleClassCleanup.Dispose was called")}

            """);

        var caseClassCleanupEndOfAssembly = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfAssembly.TestMethod"));
        caseClassCleanupEndOfAssembly.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);

        // We don't see "LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called" because it will be attached to the
        // latest test run.
        caseClassCleanupEndOfAssembly.Messages.Single().Text.Should().Be(
            $"""


            TestContext Messages:
            LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called
            LifeCycleClassCleanupEndOfAssembly.ctor was called
            LifeCycleClassCleanupEndOfAssembly.TestInitialize was called
            LifeCycleClassCleanupEndOfAssembly.TestMethod was called
            LifeCycleClassCleanupEndOfAssembly.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssembly.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssembly.Dispose was called")}
            
            """);

        var caseClassCleanupEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClass.TestMethod"));
        caseClassCleanupEndOfClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupEndOfClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfClass.ctor was called
            LifeCycleClassCleanupEndOfClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClass.TestMethod was called
            LifeCycleClassCleanupEndOfClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClass.Dispose was called")}
            LifeCycleClassCleanupEndOfClass.ClassCleanup was called
            
            """);

        var caseClassInitializeAndCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod"));
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseClassInitializeAndCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeAndCleanupNone.TestMethod"));
        caseClassInitializeAndCleanupNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeAndCleanupNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupNone.ctor was called
            LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}

            """);

        var caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone"));
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);

        var caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass"));
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.DerivedClassTestMethod"));
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);

        // Test the parent test method.
        var caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod"));
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassInitializeAndCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupNone.DerivedClassTestMethod"));
        caseDerivedClassInitializeAndCleanupNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeAndCleanupNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
            """);

        // Test the parent test method.
        var caseDerivedClassInitializeAndCleanupNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod"));
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeAndCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
            """);

        var caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DerivedClassTestMethod"));
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);

        // Test the parent test method.
        var caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod"));
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);

        var caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DerivedClassTestMethod"));
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod"));
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseClassCleanupEndOfAssemblyAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod"));
        caseClassCleanupEndOfAssemblyAndNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupEndOfAssemblyAndNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);

        var caseClassCleanupEndOfClassAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod"));
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """);

        var caseClassCleanupEndOfClassAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClassAndNone.TestMethod"));
        caseClassCleanupEndOfClassAndNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanupEndOfClassAndNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called
            
            """);

        var caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DerivedClassTestMethod"));
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassCleanupEndOfAssemblyAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.DerivedClassTestMethod"));
        caseDerivedClassCleanupEndOfAssemblyAndNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfAssemblyAndNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);
        var caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.DerivedClassTestMethod"));
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassCleanupEndOfClassAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndNone.DerivedClassTestMethod"));
        caseDerivedClassCleanupEndOfClassAndNone.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfClassAndNone.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
            """);

        var caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod"));
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod"));
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);
        var caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod"));
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
            """);

        var caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod"));
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages.Single().Text.Should().Be(
            $"""
            

            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
            """);

        // Test the parent test method.
        // We are seeing all the ClassCleanup EndOfAssembly (or nothing set - as it's the default) being reported
        // here as this is the last test to run.
        var caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod"));
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        var caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText = caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages.Single().Text;
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            $"""
            

                TestContext Messages:
                LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
                LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
                LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
                LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
                LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
                LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
                LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
                {(targetFramework == "net6.0"
                    ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                    : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
                """);
        var numberOfLines = caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Split('\n').Length;
        Verify(numberOfLines == (targetFramework == "net6.0" ? 31 : 30)); // The number of the logs + 3 empty lines + 1 for the logs' header.

        // Locally, netfx calls seems to be respecting the order of the cleanup while it is not stable for netcore.
        // But local order is not the same on various machines. I am not sure whether we should be commiting to a
        // specific order.
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassCleanup.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called");
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethodMessageText.Should().Contain(
            "AssemblyCleanup was called");
    }
}
