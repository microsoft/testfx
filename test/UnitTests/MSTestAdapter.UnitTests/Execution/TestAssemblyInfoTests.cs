// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestAssemblyInfoTests : TestContainer
{
    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly MethodInfo _dummyMethodInfo;

    private readonly UTFExtension.TestContext _testContext;

    public TestAssemblyInfoTests()
    {
        _testAssemblyInfo = new TestAssemblyInfo(typeof(TestAssemblyInfoTests).Assembly);
        _dummyMethodInfo = typeof(TestAssemblyInfoTests).GetMethods().First();

        var testContext = new Mock<UTFExtension.TestContext>();
        testContext.SetupGet(x => x.CancellationTokenSource).Returns(new CancellationTokenSource());
        _testContext = testContext.Object;
    }

    public void TestAssemblyInfoAssemblyInitializeMethodThrowsForMultipleAssemblyInitializeMethods()
    {
        void Action()
        {
            _testAssemblyInfo.AssemblyInitializeMethod = _dummyMethodInfo;
            _testAssemblyInfo.AssemblyInitializeMethod = _dummyMethodInfo;
        }

        var ex = VerifyThrows(Action);
        Verify(ex.GetType() == typeof(TypeInspectionException));
    }

    public void TestAssemblyInfoAssemblyCleanupMethodThrowsForMultipleAssemblyCleanupMethods()
    {
        void Action()
        {
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        }

        var ex = VerifyThrows(Action);
        Verify(ex.GetType() == typeof(TypeInspectionException));
    }

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnFalseIfAssemblyHasNoCleanupMethod()
    {
        Verify(!_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueEvenIfAssemblyInitializationThrewAnException()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        _testAssemblyInfo.AssemblyInitializationException = new NotImplementedException();

        Verify(_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueIfAssemblyCleanupMethodIsAvailable()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;

        Verify(_testAssemblyInfo.HasExecutableCleanupMethod);
    }

    #region Run Assembly Initialize tests

    public void RunAssemblyInitializeShouldNotInvokeIfAssemblyInitializeIsNull()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        _testAssemblyInfo.AssemblyInitializeMethod = null;

        _testAssemblyInfo.RunAssemblyInitialize(null);

        Verify(assemblyInitCallCount == 0);
    }

    public void RunAssemblyInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        void Action() => _testAssemblyInfo.RunAssemblyInitialize(null);

        var ex = VerifyThrows(Action);
        Verify(ex.GetType() == typeof(NullReferenceException));
    }

    public void RunAssemblyInitializeShouldNotExecuteAssemblyInitializeIfItHasAlreadyExecuted()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;

        _testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Verify(assemblyInitCallCount == 0);
    }

    public void RunAssemblyInitializeShouldExecuteAssemblyInitialize()
    {
        var assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => assemblyInitCallCount++;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Verify(assemblyInitCallCount == 1);
    }

    public void RunAssemblyInitializeShouldSetAssemblyInitializeExecutedFlag()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        Verify(_testAssemblyInfo.IsAssemblyInitializeExecuted);
    }

    public void RunAssemblyInitializeShouldSetAssemblyInitializationExceptionOnException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext));

        Verify(_testAssemblyInfo.AssemblyInitializationException is not null);
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure. Aborting test execution.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(
            exception.Message
            == "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive. Aborting test execution.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => { throw new ArgumentException("Some actualErrorMessage message", new InvalidOperationException("Inner actualErrorMessage message")); };
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. System.ArgumentException: Some actualErrorMessage message. Aborting test execution.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));
    }

    public void RunAssemblyInitializeShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc =>
        {
            // This helper calls inner helper, and the inner helper ctor throws.
            // We want to see the real exception on screen, and not TypeInitializationException
            // which has no info about what failed.
            FailingStaticHelper.DoWork();
        };
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. System.InvalidOperationException: I fail.. Aborting test execution.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.FailingStaticHelper..cctor()", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(InvalidOperationException));
    }

    public void RunAssemblyInitializeShouldThrowForAlreadyExecutedTestAssemblyInitWithException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { };
        _testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");
        _testAssemblyInfo.AssemblyInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = VerifyThrows(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.Message == "Cached Test failure");
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.AssemblyInitializeMethodBody = (tc) => { Verify(tc == _testContext); };
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod");

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);
    }

    #endregion

    #region Run Assembly Cleanup tests

    public void RunAssemblyCleanupShouldNotInvokeIfAssemblyCleanupIsNull()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = null;

        Verify(_testAssemblyInfo.RunAssemblyCleanup() is null);
        Verify(assemblycleanupCallCount == 0);
    }

    public void RunAssemblyCleanupShouldInvokeIfAssemblyCleanupMethod()
    {
        var assemblycleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblycleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");

        Verify(_testAssemblyInfo.RunAssemblyCleanup() is null);
        Verify(assemblycleanupCallCount == 1);
    }

    public void RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        Verify(
            _testAssemblyInfo.RunAssemblyCleanup().StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails>", StringComparison.Ordinal));
    }

    public void RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        Verify(
            _testAssemblyInfo.RunAssemblyCleanup().StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive.. StackTrace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails>", StringComparison.Ordinal));
    }

    public void RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");
        Verify(
            _testAssemblyInfo.RunAssemblyCleanup().StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: System.ArgumentException: Argument Exception. StackTrace:     at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions>", StringComparison.Ordinal));
    }

    public void RunAssemblyCleanupShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () =>
        {
            // This helper calls inner helper, and the inner helper ctor throws.
            // We want to see the real exception on screen, and not TypeInitializationException
            // which has no info about what failed.
            FailingStaticHelper.DoWork();
        };
        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod");

        var actualErrorMessage = _testAssemblyInfo.RunAssemblyCleanup();

        Verify(actualErrorMessage.StartsWith("Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: System.InvalidOperationException: I fail.. StackTrace:", StringComparison.Ordinal));
        Verify(actualErrorMessage.Contains("at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.FailingStaticHelper..cctor()"));
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

    private static class FailingStaticHelper
    {
        static FailingStaticHelper()
        {
            throw new InvalidOperationException("I fail.");
        }

        public static void DoWork()
        {
        }
    }

    private static class FailingInnerStaticHelper
    {
        static FailingInnerStaticHelper()
        {
            throw new InvalidOperationException("I fail.");
        }

        public static void Initialize()
        {
        }
    }
}
