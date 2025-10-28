// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestAssemblyInfoTests : TestContainer
{
    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly MethodInfo _dummyMethodInfo;

    private readonly TestContext _testContext;

    public TestAssemblyInfoTests()
    {
        _testAssemblyInfo = new TestAssemblyInfo(typeof(TestAssemblyInfoTests).Assembly);
        _dummyMethodInfo = typeof(TestAssemblyInfoTests).GetMethods().First();

        var testContext = new Mock<TestContext>();
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

        new Action(Action).Should().Throw<TypeInspectionException>();
    }

    public void TestAssemblyInfoAssemblyCleanupMethodThrowsForMultipleAssemblyCleanupMethods()
    {
        void Action()
        {
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
            _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        }

        new Action(Action).Should().Throw<TypeInspectionException>();
    }

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnFalseIfAssemblyHasNoCleanupMethod() => _testAssemblyInfo.HasExecutableCleanupMethod.Should().BeFalse();

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueEvenIfAssemblyInitializationThrewAnException()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;
        _testAssemblyInfo.AssemblyInitializationException = new NotImplementedException();

        _testAssemblyInfo.HasExecutableCleanupMethod.Should().BeTrue();
    }

    public void TestAssemblyHasExecutableCleanupMethodShouldReturnTrueIfAssemblyCleanupMethodIsAvailable()
    {
        _testAssemblyInfo.AssemblyCleanupMethod = _dummyMethodInfo;

        _testAssemblyInfo.HasExecutableCleanupMethod.Should().BeTrue();
    }

    #region Run Assembly Initialize tests

    public void RunAssemblyInitializeShouldNotInvokeIfAssemblyInitializeIsNull()
    {
        int assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = _ => assemblyInitCallCount++;

        _testAssemblyInfo.AssemblyInitializeMethod = null;

        _testAssemblyInfo.RunAssemblyInitialize(null!);

        assemblyInitCallCount.Should().Be(0);
    }

    public void RunAssemblyInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.AssemblyInitializeMethodBody = _ => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        new Action(() => _testAssemblyInfo.RunAssemblyInitialize(null!)).Should().Throw<NullReferenceException>();
    }

    public void RunAssemblyInitializeShouldNotExecuteAssemblyInitializeIfItHasAlreadyExecuted()
    {
        int assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = _ => assemblyInitCallCount++;

        _testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        assemblyInitCallCount.Should().Be(0);
    }

    public void RunAssemblyInitializeShouldExecuteAssemblyInitialize()
    {
        int assemblyInitCallCount = 0;
        DummyTestClass.AssemblyInitializeMethodBody = _ => assemblyInitCallCount++;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        assemblyInitCallCount.Should().Be(1);
    }

    public void RunAssemblyInitializeShouldSetAssemblyInitializeExecutedFlag()
    {
        DummyTestClass.AssemblyInitializeMethodBody = _ => { };

        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        _testAssemblyInfo.IsAssemblyInitializeExecuted.Should().BeTrue();
    }

    public void RunAssemblyInitializeShouldSetAssemblyInitializationExceptionOnException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = _ => UTF.Assert.Inconclusive("Test Inconclusive");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        Action action = () => _testAssemblyInfo.RunAssemblyInitialize(_testContext);

        action.Should().Throw<Exception>();
        _testAssemblyInfo.AssemblyInitializationException.Should().NotBeNull();
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        TestFailedException exception = new Action(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)).Should().Throw<TestFailedException>().Which;
        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure. Aborting test execution.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.Should().Contain(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionOnAssertionFailure>");
#endif
        exception.InnerException.Should().BeOfType<AssertFailedException>();
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        TestFailedException exception = new Action(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)).Should().Throw<TestFailedException>().Which;
        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Inconclusive);
        exception.Message.Should().Be("Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive. Aborting test execution.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.Should().Contain(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>");
#endif
        exception.InnerException.Should().BeOfType<AssertInconclusiveException>();
    }

    public void RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => throw new ArgumentException("Some actualErrorMessage message", new InvalidOperationException("Inner actualErrorMessage message"));
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        TestFailedException exception = new Action(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)).Should().Throw<TestFailedException>().Which;

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. System.ArgumentException: Some actualErrorMessage message. Aborting test execution.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.<>c.<RunAssemblyInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<ArgumentException>();
        exception.InnerException.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    public void RunAssemblyInitializeShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc =>
            // This helper calls inner helper, and the inner helper ctor throws.
            // We want to see the real exception on screen, and not TypeInitializationException
            // which has no info about what failed.
            FailingStaticHelper.DoWork();
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        TestFailedException exception = new Action(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)).Should().Throw<TestFailedException>().Which;

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Assembly Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests+DummyTestClass.AssemblyInitializeMethod threw exception. System.InvalidOperationException: I fail.. Aborting test execution.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.FailingStaticHelper..cctor()", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    public void RunAssemblyInitializeShouldThrowForAlreadyExecutedTestAssemblyInitWithException()
    {
        DummyTestClass.AssemblyInitializeMethodBody = _ => { };
        _testAssemblyInfo.IsAssemblyInitializeExecuted = true;
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;
        _testAssemblyInfo.AssemblyInitializationException = new TestFailedException(UTF.UnitTestOutcome.Failed, "Cached Test failure");

        TestFailedException exception = new Action(() => _testAssemblyInfo.RunAssemblyInitialize(_testContext)).Should().Throw<TestFailedException>().Which;
        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Cached Test failure");
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.AssemblyInitializeMethodBody = tc => (tc == _testContext).Should().BeTrue();
        _testAssemblyInfo.AssemblyInitializeMethod = typeof(DummyTestClass).GetMethod("AssemblyInitializeMethod")!;

        _testAssemblyInfo.RunAssemblyInitialize(_testContext);
    }

    #endregion

    #region Run Assembly Cleanup tests

    public void RunAssemblyCleanupShouldNotInvokeIfAssemblyCleanupIsNull()
    {
        int assemblyCleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblyCleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = null;

        _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext()).Should().BeNull();
        assemblyCleanupCallCount.Should().Be(0);
    }

    public void RunAssemblyCleanupShouldInvokeIfAssemblyCleanupMethod()
    {
        int assemblyCleanupCallCount = 0;
        DummyTestClass.AssemblyCleanupMethodBody = () => assemblyCleanupCallCount++;

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod")!;

        _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext()).Should().BeNull();
        assemblyCleanupCallCount.Should().Be(1);
    }

    public void RunAssemblyCleanupShouldReturnAssertFailureExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod")!;
        string? actualErrorMessage = _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext())?.Message;
        actualErrorMessage!.StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure..", StringComparison.Ordinal).Should().BeTrue($"Value: {actualErrorMessage}");
    }

    public void RunAssemblyCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod")!;
        string? actualErrorMessage = _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext())?.Message;
        actualErrorMessage!.StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive..", StringComparison.Ordinal).Should().BeTrue($"Value: {actualErrorMessage}");
    }

    public void RunAssemblyCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        DummyTestClass.AssemblyCleanupMethodBody = () => throw new ArgumentException("Argument Exception");

        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod")!;
        string? actualErrorMessage = _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext())?.Message;
        actualErrorMessage!.StartsWith(
            "Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: System.ArgumentException: Argument Exception.", StringComparison.Ordinal).Should().BeTrue($"Value: {actualErrorMessage}");
    }

    public void RunAssemblyCleanupShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        // This helper calls inner helper, and the inner helper ctor throws.
        // We want to see the real exception on screen, and not TypeInitializationException
        // which has no info about what failed.
        DummyTestClass.AssemblyCleanupMethodBody = FailingStaticHelper.DoWork;
        _testAssemblyInfo.AssemblyCleanupMethod = typeof(DummyTestClass).GetMethod("AssemblyCleanupMethod")!;

        string actualErrorMessage = _testAssemblyInfo.ExecuteAssemblyCleanup(GetTestContext())!.Message;

        actualErrorMessage.StartsWith("Assembly Cleanup method DummyTestClass.AssemblyCleanupMethod failed. Error Message: System.InvalidOperationException: I fail.. StackTrace:", StringComparison.Ordinal).Should().BeTrue();
        actualErrorMessage.Contains("at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestAssemblyInfoTests.FailingStaticHelper..cctor()").Should().BeTrue();
    }

    private static TestContextImplementation GetTestContext()
        => new(null, null, new Dictionary<string, object?>(), null, null);

    #endregion

    [TestClass]
    public class DummyTestClass
    {
        public static Action<object> AssemblyInitializeMethodBody { get; set; } = null!;

        public static Action AssemblyCleanupMethodBody { get; set; } = null!;

        public TestContext TestContext { get; set; } = null!;

        public static void AssemblyInitializeMethod(TestContext testContext) => AssemblyInitializeMethodBody.Invoke(testContext);

        public static void AssemblyCleanupMethod() => AssemblyCleanupMethodBody.Invoke();
    }

    private static class FailingStaticHelper
    {
        static FailingStaticHelper() => throw new InvalidOperationException("I fail.");

        public static void DoWork()
        {
        }
    }

    private static class FailingInnerStaticHelper
    {
        static FailingInnerStaticHelper() => throw new InvalidOperationException("I fail.");

        public static void Initialize()
        {
        }
    }
}
