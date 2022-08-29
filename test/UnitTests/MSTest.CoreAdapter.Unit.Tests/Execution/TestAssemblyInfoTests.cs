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
        testAssemblyInfo = new TestAssemblyInfo(typeof(TestAssemblyInfoTests).Assembly);
        dummyMethodInfo = typeof(TestAssemblyInfoTests).GetMethods().First();
        testContext = new Mock<UTFExtension.TestContext>().Object;
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyInitializeMethodThrowsForMultipleAssemblyInitializeMethods()
    {
        void action()
        {
            testAssemblyInfo.AssemblyInitializeMethod = dummyMethodInfo;
            testAssemblyInfo.AssemblyInitializeMethod = dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyCleanupMethodThrowsForMultipleAssemblyCleanupMethods()
    {
        void action()
        {
            testAssemblyInfo.AssemblyCleanupMethod = dummyMethodInfo;
            testAssemblyInfo.AssemblyCleanupMethod = dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnFalseIfAssemblyHasNoCleanupMethod()
    {
        Assert.IsFalse(testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueEvenIfAssemblyInitializationThrewAnException()
    {
        testAssemblyInfo.AssemblyCleanupMethod = dummyMethodInfo;
        testAssemblyInfo.AssemblyInitializationException = new NotImplementedException();

        Assert.IsTrue(testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueIfAssemblyCleanupMethodIsAvailable()
    {
        testAssemblyInfo.AssemblyCleanupMethod = dummyMethodInfo;

        Assert.IsTrue(testAssemblyInfo.HasExecutableCleanupMethod);
    }

    #region Run Assembly Initialize tests

    [TestMethod]
    public void RunAssemblyInitializeShouldNotInvokeIfAssemblyInitializeIsNull()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        testAssemblyInfo.AssemblyInitializeMethod = null;

        testAssemblyInfo.RunAssemblyInitialize(null);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        void action() => testAssemblyInfo.RunAssemblyInitialize(null);

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldNotExecuteAssemblyInitializeIfItHasAlreadyExecuted()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        testAssemblyInfo.RunAssemblyInitialize(testContext);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldExecuteAssemblyInitialize()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        testAssemblyInfo.RunAssemblyInitialize(testContext);

        Assert.AreEqual(1, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializeExecutedFlag()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        testAssemblyInfo.RunAssemblyInitialize(testContext);

        Assert.IsTrue(testAssemblyInfo.IsAssemblyInitializeExecuted);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializationExceptionOnException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => testAssemblyInfo.RunAssemblyInitialize(testContext));

        Assert.IsNotNull(testAssemblyInfo.AssemblyInitializationException);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => testAssemblyInfo.RunAssemblyInitialize(testContext)) as TestFailedException;

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
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => testAssemblyInfo.RunAssemblyInitialize(testContext)) as TestFailedException;

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
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => testAssemblyInfo.RunAssemblyInitialize(testContext)) as TestFailedException;

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
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");
        testAssemblyInfo.AssemblyInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = ActionUtility.PerformActionAndReturnException(() => testAssemblyInfo.RunAssemblyInitialize(testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Cached Test failure",
            exception.Message);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { Assert.AreEqual(tc, testContext); };
        testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        testAssemblyInfo.RunAssemblyInitialize(testContext);
    }

    #endregion

    #region Run Assembly Cleanup tests

    [TestMethod]
    public void RunAssemblyCleanupShouldNotInvokeIfAssemblyCleanupIsNull()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        testAssemblyInfo.AssemblyCleanupMethod = null;

        Assert.IsNull(testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(0, assemblycleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldInvokeIfAssemblyCleanupMethod()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");

        Assert.IsNull(testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(1, assemblycleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

        testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

        testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };

        testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            testAssemblyInfo.RunAssemblyCleanup(),
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
