// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

using System;
using System.Linq;
using System.Reflection;

using global::MSTestAdapter.TestUtilities;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using Moq;

using MSTest.TestAdapter.ObjectModel;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestAssemblyInfoTests
{
    private readonly TestAssemblyInfo testAssemblyInfo;

    private readonly MethodInfo dummyMethodInfo;

    private readonly UTFExtension.TestContext testContext;

    public TestAssemblyInfoTests()
    {
        this.testAssemblyInfo = new TestAssemblyInfo(typeof(TestAssemblyInfoTests).Assembly);
        this.dummyMethodInfo = typeof(TestAssemblyInfoTests).GetMethods().First();
        this.testContext = new Mock<UTFExtension.TestContext>().Object;
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyInitializeMethodThrowsForMultipleAssemblyInitializeMethods()
    {
        void action()
        {
            this.testAssemblyInfo.AssemblyInitializeMethod = this.dummyMethodInfo;
            this.testAssemblyInfo.AssemblyInitializeMethod = this.dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyCleanupMethodThrowsForMultipleAssemblyCleanupMethods()
    {
        void action()
        {
            this.testAssemblyInfo.AssemblyCleanupMethod = this.dummyMethodInfo;
            this.testAssemblyInfo.AssemblyCleanupMethod = this.dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnFalseIfAssemblyHasNoCleanupMethod()
    {
        Assert.IsFalse(this.testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueEvenIfAssemblyInitializationThrewAnException()
    {
        this.testAssemblyInfo.AssemblyCleanupMethod = this.dummyMethodInfo;
        this.testAssemblyInfo.AssemblyInitializationException = new NotImplementedException();

        Assert.IsTrue(this.testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueIfAssemblyCleanupMethodIsAvailable()
    {
        this.testAssemblyInfo.AssemblyCleanupMethod = this.dummyMethodInfo;

        Assert.IsTrue(this.testAssemblyInfo.HasExecutableCleanupMethod);
    }

    #region Run Assembly Initialize tests

    [TestMethod]
    public void RunAssemblyInitializeShouldNotInvokeIfAssemblyInitializeIsNull()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        this.testAssemblyInfo.AssemblyInitializeMethod = null;

        this.testAssemblyInfo.RunAssemblyInitialize(null);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        void action() => this.testAssemblyInfo.RunAssemblyInitialize(null);

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldNotExecuteAssemblyInitializeIfItHasAlreadyExecuted()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        this.testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        this.testAssemblyInfo.RunAssemblyInitialize(this.testContext);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldExecuteAssemblyInitialize()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        this.testAssemblyInfo.RunAssemblyInitialize(this.testContext);

        Assert.AreEqual(1, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializeExecutedFlag()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        this.testAssemblyInfo.RunAssemblyInitialize(this.testContext);

        Assert.IsTrue(this.testAssemblyInfo.IsAssemblyInitializeExecuted);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializationExceptionOnException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testAssemblyInfo.RunAssemblyInitialize(this.testContext));

        Assert.IsNotNull(this.testAssemblyInfo.AssemblyInitializationException);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testAssemblyInfo.RunAssemblyInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure. Aborting test execution.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertFailedException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testAssemblyInfo.RunAssemblyInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
        Assert.AreEqual(
            "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive. Aborting test execution.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertInconclusiveException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testAssemblyInfo.RunAssemblyInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. System.ArgumentException: Some exception message. Aborting test execution.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
        Assert.IsInstanceOfType(exception.InnerException.InnerException, typeof(InvalidOperationException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowForAlreadyExecutedTestAssemblyInitWithException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");
        this.testAssemblyInfo.AssemblyInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testAssemblyInfo.RunAssemblyInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Cached Test failure",
            exception.Message);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { Assert.AreEqual(tc, this.testContext); };
        this.testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        this.testAssemblyInfo.RunAssemblyInitialize(this.testContext);
    }

    #endregion

    #region Run Assembly Cleanup tests

    [TestMethod]
    public void RunAssemblyCleanupShouldNotInvokeIfAssemblyCleanupIsNull()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        this.testAssemblyInfo.AssemblyCleanupMethod = null;

        Assert.IsNull(this.testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(0, assemblycleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldInvokeIfAssemblyCleanupMethod()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        this.testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");

        Assert.IsNull(this.testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(1, assemblycleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

        this.testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            this.testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

        this.testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            this.testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };

        this.testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            this.testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: System.ArgumentException: Argument Exception. StackTrace:     at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions>");
    }

    #endregion

    [UTF.TestClass]
    public class DummyTestClass
    {
        public static Action<object> AssemblyInitializeMethodBody { get; set; }

        public static Action AssemblyCleanupMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        public static void AssemblyInitializeMethod(UTFExtension.TestContext testContext)
        {
            AssemblyInitializeMethodBody.Invoke(testContext);
        }

        public static void AssemblyCleanupMethod()
        {
            AssemblyCleanupMethodBody.Invoke();
        }
    }
}
