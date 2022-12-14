// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
            Trace: AssemblyInit was called
            Debug: AssemblyInit was called
            Trace: LifeCycleClassCleanup.ClassInitialize was called
            Debug: LifeCycleClassCleanup.ClassInitialize was called
            Trace: LifeCycleClassCleanup.ctor was called
            Debug: LifeCycleClassCleanup.ctor was called
            Trace: LifeCycleClassCleanup.TestInitialize was called
            Debug: LifeCycleClassCleanup.TestInitialize was called
            Trace: LifeCycleClassCleanup.TestMethod was called
            Debug: LifeCycleClassCleanup.TestMethod was called
            Trace: LifeCycleClassCleanup.TestCleanup was called
            Debug: LifeCycleClassCleanup.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanup.DisposeAsync was called\r\nDebug: LifeCycleClassCleanup.DisposeAsync was called\r\nTrace: LifeCycleClassCleanup.Dispose was called\r\nDebug: LifeCycleClassCleanup.Dispose was called"
                : "Trace: LifeCycleClassCleanup.Dispose was called\r\nDebug: LifeCycleClassCleanup.Dispose was called")}

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
            Trace: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssembly.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssembly.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssembly.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfAssembly.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssembly.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssembly.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssembly.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssembly.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClass.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfClass.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfClass.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClass.Dispose was called")}
            Trace: LifeCycleClassCleanupEndOfClass.ClassCleanup was called
            Debug: LifeCycleClassCleanupEndOfClass.ClassCleanup was called
            
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
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}

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
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Trace: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeAndCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
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
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called\r\nDebug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            
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
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

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
            Trace: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            Trace: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called
            
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
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
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
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            
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
            Trace: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Trace: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            Debug: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nTrace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Trace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called\r\nDebug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            
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
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[0].Text.Should().Be(
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

            """);
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[1].Text.Should().Be(
            $"""


            Debug Trace:
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net6.0"
                ? "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nTrace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called\r\nDebug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """);

        var expectedStart =
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
        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[2].Text.Should().StartWith(expectedStart);
        var expectedRemainingMessages = new List<string>
        {
            "LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called",
            "LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called",
            "LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called",
            "LifeCycleClassCleanup.ClassCleanup was called",
            "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called",
            "LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called",
            "LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called",
            "LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called",
            "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called",
            "LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called",
            "AssemblyCleanup was called",
            string.Empty,
        };

        caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
            .Messages[2].Text
            .Substring(expectedStart.Length)
            .Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Should().BeEquivalentTo(expectedRemainingMessages);
    }
}
