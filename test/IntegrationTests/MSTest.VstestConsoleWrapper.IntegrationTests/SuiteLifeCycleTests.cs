// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
public class SuiteLifeCycleTests : CLITestBase
{
    private const string TestAssetName = "SuiteLifeCycleTestProject";
    private static readonly string[] WindowsLineReturn = ["\r\n"];

    [TestMethod]
    [DataRow("net8.0")]
    [DataRow("net462")]
    public void ValidateTestRunLifecycle(string targetFramework)
    {
        InvokeVsTestForExecution(
            [TestAssetName],
            testCaseFilter: "FullyQualifiedName~SuiteLifeCycleTestProject",
            targetFramework: targetFramework);
        Assert.HasCount(19, RunEventsHandler.PassedTests);  // The inherit class tests are called twice.

        TestResult caseClassCleanupEndOfClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseClassCleanupEndOfClass.Outcome);
        Assert.HasCount(3, caseClassCleanupEndOfClass.Messages);
        Assert.AreEqual(
            $"""
            Console: AssemblyInit was called
            Console: LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClass.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClass.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClass.ClassCleanup was called

            """,
            caseClassCleanupEndOfClass.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("AssemblyInit was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClass.ClassCleanup was called")}

            """,
            caseClassCleanupEndOfClass.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            AssemblyInit was called
            LifeCycleClassCleanupEndOfClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfClass.ctor was called
            LifeCycleClassCleanupEndOfClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClass.TestMethod was called
            LifeCycleClassCleanupEndOfClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClass.Dispose was called")}
            LifeCycleClassCleanupEndOfClass.ClassCleanup was called

            """,
            caseClassCleanupEndOfClass.Messages[2].Text);

        TestResult caseClassInitializeAndCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseClassInitializeAndCleanupBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}

            """,
            caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassInitializeAndCleanupBeforeEachDerivedClass.Messages[2].Text);

        TestResult caseClassInitializeAndCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeAndCleanupNone.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseClassInitializeAndCleanupNone.Outcome);
        Assert.HasCount(3, caseClassInitializeAndCleanupNone.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            Console: LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called

            """,
            caseClassInitializeAndCleanupNone.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called")}

            """,
            caseClassInitializeAndCleanupNone.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeAndCleanupNone.ctor was called
            LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called

            """,
            caseClassInitializeAndCleanupNone.Messages[2].Text);

        TestResult caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone"));
        Assert.AreEqual(TestOutcome.Passed, caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Outcome);
        Assert.HasCount(3, caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called

            """,
            caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called")}

            """,
            caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called

            """,
            caseClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[2].Text);

        TestResult caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass"));
        Assert.AreEqual(TestOutcome.Passed, caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called")}

            """,
            caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[2].Text);

        TestResult caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClass.Messages[2].Text);

        // Test the parent test method.
        TestResult caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called")}

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called")}
            LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseDerivedClassInitializeAndCleanupBeforeEachDerivedClassParentTestMethod.Messages[2].Text);

        TestResult caseDerivedClassInitializeAndCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupNone.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeAndCleanupNone.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeAndCleanupNone.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}

            """,
            caseDerivedClassInitializeAndCleanupNone.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}

            """,
            caseDerivedClassInitializeAndCleanupNone.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}

            """,
            caseDerivedClassInitializeAndCleanupNone.Messages[2].Text);

        // Test the parent test method.
        TestResult caseDerivedClassInitializeAndCleanupNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called

            """,
            caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeAndCleanupNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called")}

            """,
            caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeAndCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called
            LifeCycleClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called
            LifeCycleClassInitializeAndCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called
            LifeCycleClassInitializeAndCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeAndCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeAndCleanupNone.Dispose was called")}
            LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called

            """,
            caseDerivedClassInitializeAndCleanupNoneParentTestMethod.Messages[2].Text);

        TestResult caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Messages[2].Text);

        // Test the parent test method.
        TestResult caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called")}

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called\r\nLifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called"
                : "LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called")}
            LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called

            """,
            caseDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNoneParentTestMethod.Messages[2].Text);

        TestResult caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}

            """,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Messages[2].Text);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult caseClassCleanupEndOfClassAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called")}

            """,
            caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[2].Text);

        TestResult caseClassCleanupEndOfClassAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleClassCleanupEndOfClassAndNone.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseClassCleanupEndOfClassAndNone.Outcome);
        Assert.HasCount(3, caseClassCleanupEndOfClassAndNone.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            Console: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called

            """,
            caseClassCleanupEndOfClassAndNone.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called")}

            """,
            caseClassCleanupEndOfClassAndNone.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called

            """,
            caseClassCleanupEndOfClassAndNone.Messages[2].Text);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Outcome);
        Assert.HasCount(3, caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.Messages[2].Text);

        TestResult caseDerivedClassCleanupEndOfClassAndNone = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndNone.DerivedClassTestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassCleanupEndOfClassAndNone.Outcome);
        Assert.HasCount(3, caseDerivedClassCleanupEndOfClassAndNone.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndNone.Messages[0].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}

            """,
            caseDerivedClassCleanupEndOfClassAndNone.Messages[1].Text);
        Assert.AreEqual(
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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndNone.Messages[2].Text);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called")}
            LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called

            """,
            caseDerivedClassCleanupEndOfClassAndBeforeEachDerivedClassParentTestMethod.Messages[2].Text);

        TestResult caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages);
        Assert.AreEqual(
            $"""
            Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nConsole: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called

            """,
            caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[0].Text);
        Assert.AreEqual(
            $"""


            Debug Trace:
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called")}
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called"))}
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called")}

            """,
            caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[1].Text);
        Assert.AreEqual(
            $"""


            TestContext Messages:
            LifeCycleClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called
            LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called
            LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called
            LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called
            LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called\r\nLifeCycleClassCleanupEndOfClassAndNone.Dispose was called"
                : "LifeCycleClassCleanupEndOfClassAndNone.Dispose was called")}
            LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called

            """,
            caseDerivedClassCleanupEndOfClassAndNoneParentTestMethod.Messages[2].Text);

        // Test the parent test method.
        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.Contains("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod"));
        Assert.AreEqual(TestOutcome.Passed, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Outcome);
        Assert.HasCount(3, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages);

        // Locally, netfx calls seems to be respecting the order of the cleanup while it is not stable for netcore.
        // But local order is not the same on various machines. I am not sure whether we should be committing to a
        // specific order.
        string expectedStart =
            $"""
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called
            {(targetFramework == "net8.0"
                ? "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nConsole: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """;
        Assert.StartsWith(expectedStart, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[0].Text);

        string[] expectedRemainingMessages =
            """
            Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            Console: AssemblyCleanup was called

            """
            .Split(WindowsLineReturn, StringSplitOptions.None);
        CollectionAssert.AreEquivalent(
            expectedRemainingMessages,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
                .Messages[0].Text!
                .Substring(expectedStart.Length)
                .Split(WindowsLineReturn, StringSplitOptions.None));

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
            {(targetFramework == "net8.0"
                ? GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called")
                    + "\r\n"
                    + GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")
                : GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"))}

            """;
        Assert.StartsWith(expectedStart, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[1].Text);

        expectedRemainingMessages =
            $"""
            {GenerateTraceDebugPrefixedMessage("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called")}
            {GenerateTraceDebugPrefixedMessage("AssemblyCleanup was called")}

            """
            .Split(["\r\n"], StringSplitOptions.None);
        CollectionAssert.AreEquivalent(
            expectedRemainingMessages,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
                .Messages[1].Text!
                .Substring(expectedStart.Length)
                .Split(["\r\n"], StringSplitOptions.None));

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
            {(targetFramework == "net8.0"
                ? "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called\r\nLifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called"
                : "LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called")}

            """;
        Assert.StartsWith(expectedStart, caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod.Messages[2].Text);

        expectedRemainingMessages =
            """
            LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called
            AssemblyCleanup was called

            """
            .Split(["\r\n"], StringSplitOptions.None);
        CollectionAssert.AreEquivalent(
            expectedRemainingMessages,
            caseDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClassParentTestMethod
                .Messages[2].Text!
                .Substring(expectedStart.Length)
                .Split(["\r\n"], StringSplitOptions.None));
    }

    [TestMethod]
    public void ValidateInheritanceBehavior()
    {
        InvokeVsTestForExecution(
            [TestAssetName],
            testCaseFilter: "FullyQualifiedName~LifecycleInheritance",
            targetFramework: "net462");

        Assert.HasCount(3, RunEventsHandler.PassedTests);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult testMethod1 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfClass.TestMethod", StringComparison.Ordinal));
        Assert.AreEqual(
            """
            Console: AssemblyInit was called
            TestClassBaseEndOfClass: ClassInitialize
            TestClassDerived_EndOfClass: TestMethod
            TestClassBaseEndOfClass: ClassCleanup

            """, testMethod1.Messages[0].Text);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult testMethod2 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerivedEndOfClass_EndOfClassEndOfClass.TestMethod", StringComparison.Ordinal));
        Assert.AreEqual(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize
            TestClassDerivedEndOfClass_EndOfClassEndOfClass: TestMethod
            TestClassDerivedEndOfClass_EndOfClassEndOfClass: ClassCleanup
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup

            """, testMethod2.Messages[0].Text);

        Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult testMethod3 = RunEventsHandler.PassedTests.Single(x => x.TestCase.FullyQualifiedName.EndsWith("TestClassDerived_EndOfClassEndOfClass.TestMethod", StringComparison.Ordinal));
        Assert.AreEqual(
            """
            TestClassBaseEndOfClass: ClassInitialize
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassInitialize
            TestClassDerived_EndOfClassEndOfClass: TestMethod
            TestClassIntermediateEndOfClassBaseEndOfClass: ClassCleanup
            TestClassBaseEndOfClass: ClassCleanup
            Console: AssemblyCleanup was called
            
            """, testMethod3.Messages[0].Text);
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
