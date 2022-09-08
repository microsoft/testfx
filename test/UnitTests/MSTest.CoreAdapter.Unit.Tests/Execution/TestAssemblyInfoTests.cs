// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV2CoreExtension;
#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

using System;
using System.Linq;
using System.Reflection;

using global::MSTestAdapter.TestUtilities;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using Moq;

using MSTest.TestAdapter.ObjectModel;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestAssemblyInfoTests
{
    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly MethodInfo _dummyMethodInfo;

    private readonly UTFExtension.TestContext _testContext;

    public TestAssemblyInfoTests()
    {
        _testAssemblyInfo = new TestAssemblyInfo(typeof(TestAssemblyInfoTests).Assembly);
        _dummyMethodInfo = typeof(TestAssemblyInfoTests).GetMethods().First();
        _testContext = new Mock<UTFExtension.TestContext>().Object;
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyInitializeMethodThrowsForMultipleAssemblyInitializeMethods()
    {
        void action()
        {
            _testAssemblyInfo.AssemblyInitializeMethod = _dummyMethodInfo;
            _testAssemblyInfo.AssemblyInitializeMethod = _dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyInfoAssemblyCleanupMethodThrowsForMultipleAssemblyCleanupMethods()
    {
        void action()
        {
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnFalseIfAssemblyHasNoCleanupMethod()
    {
        Assert.IsFalse(_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueEvenIfAssemblyInitializationThrewAnException()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        _testAssemblyInfo.AssemblyInitializationException = new NotImplementedException();

        Assert.IsTrue(_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueIfAssemblyCleanupMethodIsAvailable()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;

        Assert.IsTrue(_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    #region Run Assembly Initialize tests

    [TestMethod]
    public void RunAssemblyInitializeShouldNotInvokeIfAssemblyInitializeIsNull()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        _testAssemblyInfo.AssemblyInitializeMethod = null;

        _testAssemblyInfo.RunAssemblyInitialize(null);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        void action() => _testAssemblyInfo.RunAssemblyInitialize(null);

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldNotExecuteAssemblyInitializeIfItHasAlreadyExecuted()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        _testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Assert.AreEqual(0, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldExecuteAssemblyInitialize()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Assert.AreEqual(1, assemblyInitCallCount);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializeExecutedFlag()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Assert.IsTrue(_testAssemblyInfo.IsAssemblyInitializeExecuted);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldSetAssemblyInitializationExceptionOnException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext));

        Assert.IsNotNull(_testAssemblyInfo.AssemblyInitializationException);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

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
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

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
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

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
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");
        _testAssemblyInfo.AssemblyInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Cached Test failure",
            exception.Message);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { Assert.AreEqual(tc, _testContext); };
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);
    }

    #endregion

    #region Run Assembly Cleanup tests

    [TestMethod]
    public void RunAssemblyCleanupShouldNotInvokeIfAssemblyCleanupIsNull()
    {
        var assemblyCleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblyCleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = null;

        Assert.IsNull(_testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(0, assemblyCleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldInvokeIfAssemblyCleanupMethod()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");

        Assert.IsNull(_testAssemblyInfo.RunAssemblyCleanup());
        Assert.AreEqual(1, assemblycleanupCallCount);
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            _testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            _testAssemblyInfo.RunAssemblyCleanup(),
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails>");
    }

    [TestMethod]
    public void RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        StringAssert.StartsWith(
            _testAssemblyInfo.RunAssemblyCleanup(),
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
