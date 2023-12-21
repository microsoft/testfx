// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class SuiteLifeCycleTests : CLITestBase
{
    private const string TestAssetName = "SuiteLifeCycleTestProject";
    private static readonly string[] WindowsLineReturn = ["\r\n"];

    public void ValidateTestRunLifecycle_net6()
    {
        ValidateTestRunLifecycle("net6.0");
    }

    public void ValidateTestRunLifecycle_net462()
    {
        ValidateTestRunLifecycle("net462");
    }

    public void ValidateInheritanceBehavior()
    {
        InvokeVsTestForExecution(
            [TestAssetName],
            testCaseFilter: "FullyQualifiedName~LifecycleInheritance",
            targetFramework: "net462");

        RunEventsHandler.PassedTests.Should().HaveCount(10);

        var testMethod1 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfClass.TestMethod", StringComparison.Ordinal));
        testMethod1.Messages[0].Text.Should().Be(
            """
            Console: AssemblyInit was called
            TestClassBaseEndOfClass: ClassInitialize
            TestClassDerived_EndOfClass: TestMethod
            TestClassBaseEndOfClass: ClassCleanup
            
            """);

        var testMethod2 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfAssembly.TestMethod", StringComparison.Ordinal));
        testMethod2.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfAssembly: ClassInitialize
            TestClassDerived_EndOfAssembly: TestMethod
            
            """);

        var testMethod3 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerivedEndOfClass_EndOfClassEndOfClass.TestMethod", StringComparison.Ordinal));
        testMethod3.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize
            TestClassDerivedEndOfClass_EndOfClassEndOfClass: TestMethod
            TestClassDerivedEndOfClass_EndOfClassEndOfClass: ClassCleanup
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup
            
            """);

        var testMethod4 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfClassEndOfClass.TestMethod", StringComparison.Ordinal));
        testMethod4.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize
            TestClassDerived_EndOfClassEndOfClass: TestMethod
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup
            
            """);

        var testMethod5 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerivedEndOfClass_EndOfClassEndOfAssembly.TestMethod", StringComparison.Ordinal));
        testMethod5.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfAssembly: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassInitialize
            TestClassDerivedEndOfClass_EndOfClassEndOfAssembly: TestMethod
            TestClassDerivedEndOfClass_EndOfClassEndOfAssembly: ClassCleanup
            TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassCleanup
            TestClassBaseEndOfAssembly: ClassCleanup
            
            """);

        var testMethod6 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfClassEndOfAssembly.TestMethod", StringComparison.Ordinal));
        testMethod6.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfAssembly: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassInitialize
            TestClassDerived_EndOfClassEndOfAssembly: TestMethod
            TestClassIntermediateEndOfClassBaseEndOfAssembly: ClassCleanup
            TestClassBaseEndOfAssembly: ClassCleanup
            
            """);

        var testMethod7 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass.TestMethod", StringComparison.Ordinal));
        testMethod7.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassInitialize
            TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass: TestMethod
            TestClassDerivedEndOfClass_EndOfAssemblyEndOfClass: ClassCleanup
            TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup
            
            """);

        var testMethod8 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfAssemblyEndOfClass.TestMethod", StringComparison.Ordinal));
        testMethod8.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassInitialize
            TestClassDerived_EndOfAssemblyEndOfClass: TestMethod
            TestClassIntermediateEndOfAssemblyBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup
            
            """);

        var testMethod9 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly.TestMethod", StringComparison.Ordinal));
        testMethod9.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfAssembly: ClassInitialize
            TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassInitialize
            TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly: TestMethod
            TestClassDerivedEndOfClass_EndOfAssemblyEndOfAssembly: ClassCleanup
            TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassCleanup
            TestClassBaseEndOfAssembly: ClassCleanup
            
            """);

        var testMethod10 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfAssemblyEndOfAssembly.TestMethod", StringComparison.Ordinal));
        testMethod10.Messages[0].Text.Should().Be(
            """
            TestClassBaseEndOfAssembly: ClassInitialize
            TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassInitialize
            TestClassDerived_EndOfAssemblyEndOfAssembly: TestMethod
            TestClassIntermediateEndOfAssemblyBaseEndOfAssembly: ClassCleanup
            TestClassBaseEndOfAssembly: ClassCleanup
            TestClassBaseEndOfAssembly: ClassCleanup
            Console: AssemblyCleanup was called
            
            """);
    }

    private void ValidateTestRunLifecycle(string targetFramework)
    {
        InvokeVsTestForExecution(
            [TestAssetName],
            testCaseFilter: "FullyQualifiedName~SuiteLifeCycleTestProject",
            targetFramework: targetFramework);
        RunEventsHandler.PassedTests.Should().HaveCount(27);  // The inherit class tests are called twice.

        var caseClassCleanup = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanup.TestMethod"));
        caseClassCleanup.Outcome.Should().Be(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed);
        caseClassCleanup.Messages.Should().HaveCount(3);
        caseClassCleanup.Messages[0].Text.Should().Be(
            $"""
            Console: AssemblyInit was called
            Console: LifeCycleClassCleanup.ClassInitialize was called
            Console: LifeCycleClassCleanup.ctor was called
            Console: LifeCycleClassCleanup.TestInitialize was called
            Console: LifeCycleClassCleanup.TestMethod was called
            Console: LifeCycleClassCleanup.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanup.DisposeAsync was called\r\nConsole: LifeCycleClassCleanup.Dispose was called"
                : "Console: LifeCycleClassCleanup.Dispose was called")}

            """);
        caseClassCleanup.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("AssemblyInit was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.Dispose was called"))}

            """);
        caseClassCleanup.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfAssembly.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfAssembly.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfAssembly.ctor was called
            Console: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfAssembly.TestMethod was called
            Console: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssembly.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssembly.Dispose was called")}
            
            """);
        caseClassCleanupEndOfAssembly.Messages[1].Text.Should().Be(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.Dispose was called"))}
            
            """);
        caseClassCleanupEndOfAssembly.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfClass.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClass.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClass.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClass.ClassCleanup was called
            
            """);
        caseClassCleanupEndOfClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ClassCleanup was called")}
            
            """);
        caseClassCleanupEndOfClass.Messages[2].Text.Should().Be(
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
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""            
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseClassInitializeAndCleanupNone.Messages.Should().HaveCount(3);
        caseClassInitializeAndCleanupNone.Messages[0].Text.Should().Be(
            $"""            
            Console: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}

            """);
        caseClassInitializeAndCleanupNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}

            """);
        caseClassInitializeAndCleanupNone.Messages[2].Text.Should().Be(
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
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages.Should().HaveCount(3);
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}
            
            """);
        caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[2].Text.Should().Be(
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
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""            
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""            
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeAndCleanupNone.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeAndCleanupNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
            """);
        caseDerivedClassInitializeAndCleanupNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeAndCleanupNone.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
            """);
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
            """);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfAssemblyAndNone.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfAssemblyAndNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);
        caseClassCleanupEndOfAssemblyAndNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"))}
            
            """);
        caseClassCleanupEndOfAssemblyAndNone.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """);
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called")}

            """);
        caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseClassCleanupEndOfClassAndNone.Messages.Should().HaveCount(3);
        caseClassCleanupEndOfClassAndNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called
            
            """);
        caseClassCleanupEndOfClassAndNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called")}
            
            """);
        caseClassCleanupEndOfClassAndNone.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfAssemblyAndNone.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfAssemblyAndNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndNone.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfClassAndNone.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfClassAndNone.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfClassAndNone.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfClassAndNone.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClassParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfAssemblyAndNoneParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages.Should().HaveCount(3);
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[0].Text.Should().Be(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
            """);
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[1].Text.Should().Be(
            $"""
            

            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}
            
            """);
        caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[2].Text.Should().Be(
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
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages.Should().HaveCount(3);

        // Locally, netfx calls seems to be respecting the order of the cleanup while it is not stable for netcore.
        // But local order is not the same on various machines. I am not sure whether we should be committing to a
        // specific order.
        var expectedStart =
            $"""
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """;
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[0].Text
            .Should().StartWith(expectedStart);

        var expectedRemainingMessages =
            """
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassCleanup.ClassCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called
            Console: LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called
            Console: LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called
            Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called
            Console: AssemblyCleanup was called
            
            """
            .Split(WindowsLineReturn, StringSplitOptions.None);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[0].Text
            .Substring(expectedStart.Length)
            .Split(WindowsLineReturn, StringSplitOptions.None)
            .Should().BeEquivalentTo(expectedRemainingMessages);

        expectedStart =
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net6.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}

            """;
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[1].Text
            .Should().StartWith(expectedStart);

        expectedRemainingMessages =
            $"""
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanup.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("AssemblyCleanup was called")}

            """
            .Split(new[] { "\r\n" }, StringSplitOptions.None);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[1].Text
            .Substring(expectedStart.Length)
            .Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Should().BeEquivalentTo(expectedRemainingMessages);

        expectedStart =
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
            
            """;
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[2].Text
            .Should().StartWith(expectedStart);

        expectedRemainingMessages =
            """
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called
            LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called
            LifeCycleClassCleanup.ClassCleanup was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called
            LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called
            LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called
            AssemblyCleanup was called

            """
            .Split(new[] { "\r\n" }, StringSplitOptions.None);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[2].Text
            .Substring(expectedStart.Length)
            .Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Should().BeEquivalentTo(expectedRemainingMessages);
    }

    private static string GenerateTraceDebugPrefixedMessage(string message)
    {
        string prefixedMessage = $"Trace: {message}";

#if DEBUG
        prefixedMessage = $"{prefixedMessage}\r\nDebug: {message}";
#endif

        return prefixedMessage;
    }
}
