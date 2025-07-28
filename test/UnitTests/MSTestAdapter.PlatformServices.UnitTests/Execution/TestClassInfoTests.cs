// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TestClassInfoTests : TestContainer
{
    private sealed class TestTestContextImpl : ITestContext
    {
        public TestTestContextImpl(TestContext testContext)
            => Context = testContext;

        public TestContext Context { get; }

        public void AddProperty(string propertyName, string propertyValue) => throw new NotImplementedException();

        public void ClearDiagnosticMessages()
        {
        }

        public void DisplayMessage(MessageLevel messageLevel, string message) => throw new NotImplementedException();

        public string GetDiagnosticMessages() => string.Empty;

        public IList<string> GetResultFiles() => throw new NotImplementedException();

        public void SetDataConnection(object? dbConnection) => throw new NotImplementedException();

        public void SetDataRow(object? dataRow) => throw new NotImplementedException();

        public void SetDisplayName(string? displayName) => throw new NotImplementedException();

        public void SetException(Exception? exception) => throw new NotImplementedException();

        public void SetOutcome(UTF.UnitTestOutcome outcome) => throw new NotImplementedException();

        public void SetTestData(object?[]? data) => throw new NotImplementedException();

        public bool TryGetPropertyValue(string propertyName, out object propertyValue) => throw new NotImplementedException();
    }

    private readonly Type _testClassType;

    private readonly ConstructorInfo _testClassConstructor;

    private readonly TestClassAttribute _testClassAttribute;

    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly TestContext _testContext;

    private readonly TestClassInfo _testClassInfo;

    public TestClassInfoTests()
    {
        _testClassType = typeof(DummyTestClass);
        _testClassConstructor = _testClassType.GetConstructors().First();
        _testClassAttribute = _testClassType.GetCustomAttributes().OfType<TestClassAttribute>().First();
        _testAssemblyInfo = new TestAssemblyInfo(_testClassType.Assembly);

        _testClassInfo = new TestClassInfo(
            _testClassType,
            _testClassConstructor,
            true,
            _testClassAttribute,
            _testAssemblyInfo);

        var testContext = new Mock<TestContext>();
        testContext.SetupGet(x => x.CancellationTokenSource).Returns(new CancellationTokenSource());
        _testContext = testContext.Object;

        // Prevent leaking init/cleanup methods between classes
        DummyGrandParentTestClass.ClassInitMethodBody = null!;
        DummyGrandParentTestClass.CleanupClassMethodBody = null!;
        DummyBaseTestClass.ClassInitializeMethodBody = null!;
        DummyBaseTestClass.ClassCleanupMethodBody = null!;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = null!;
        DummyDerivedTestClass.DerivedClassCleanupMethodBody = null!;
        DummyTestClass.ClassInitializeMethodBody = null!;
        DummyTestClass.ClassCleanupMethodBody = null!;
    }

    public void TestClassInfoClassAttributeGetsAReferenceToTheTestClassAttribute() => Verify(_testClassAttribute == _testClassInfo.ClassAttribute);

    public void TestClassInfoClassTypeGetsAReferenceToTheActualTypeForTheTestClass() => Verify(typeof(DummyTestClass) == _testClassInfo.ClassType);

    public void TestClassInfoConstructorGetsTheConstructorInfoForTestClass() => Verify(_testClassConstructor == _testClassInfo.Constructor);

    public void TestClassInfoTestContextPropertyGetsAReferenceToTheTestContextDefinedInTestClass() => Verify(_testClassInfo.TestContextProperty == _testClassType.GetProperty("TestContext"));

    public void TestClassInfoParentGetsAReferenceToTheParentAssemblyForTheTestClass() => Verify(_testAssemblyInfo == _testClassInfo.Parent);

    public void TestClassInfoClassInitializeMethodSetShouldThrowForMultipleClassInitializeMethods()
    {
        void Action()
        {
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
        }

        VerifyThrows<TypeInspectionException>(Action);
    }

    public void TestClassInfoClassCleanupMethodSetShouldThrowForMultipleClassCleanupMethods()
    {
        void Action()
        {
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
        }

        VerifyThrows<TypeInspectionException>(Action);
    }

    public void TestClassInfoClassCleanupMethodShouldNotInvokeWhenNoTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>())); // call cleanup without calling init
        Verify(ex is null);
        Verify(classCleanupCallCount == 0);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>())); // call cleanup without calling init

        Verify(ex is null);
        Verify(classCleanupCallCount == 1);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeBaseClassCleanupMethodWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.BaseClassCleanupMethods.Add(typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")!);

        GetResultOrRunClassInitialize();
        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        Verify(ex is null);
        Verify(classCleanupCallCount == 1);
    }

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassDoesNotHaveCleanupMethod() => Verify(!_testClassInfo.HasExecutableCleanupMethod);

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueEvenIfClassInitializeThrowsAnException()
    {
        _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
        _testClassInfo.ClassInitializationException = new NotImplementedException();

        Verify(_testClassInfo.HasExecutableCleanupMethod);
    }

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueIfClassHasCleanupMethod()
    {
        _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();

        Verify(_testClassInfo.HasExecutableCleanupMethod);
    }

    #region Run Class Initialize tests

    public void RunClassInitializeShouldNotInvokeIfClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = null;

        GetResultOrRunClassInitialize(null);

        Verify(classInitCallCount == 0);
    }

    public void RunClassInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        TestResult result = GetResultOrRunClassInitialize(null);
        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Error);
        Verify(exception.Message == "TestContext cannot be Null.");
    }

    public void RunClassInitializeShouldExecuteClassInitialize()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        Verify(classInitCallCount == 1);
        Verify(_testClassInfo.IsClassInitializeExecuted);

        GetResultOrRunClassInitialize();
        Verify(classInitCallCount == 1);
        Verify(_testClassInfo.IsClassInitializeExecuted);
    }

    public void RunClassInitializeShouldSetClassInitializeExecutedFlag()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();

        Verify(_testClassInfo.IsClassInitializeExecuted);
    }

    public void RunClassInitializeShouldOnlyRunOnce()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        GetResultOrRunClassInitialize();

        Verify(classInitCallCount == 1, "Class Initialize called only once");
    }

    public void RunClassInitializeShouldRunOnlyOnceIfThereIsNoDerivedClassInitializeAndSetClassInitializeExecutedFlag()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        GetResultOrRunClassInitialize();
        Verify(_testClassInfo.IsClassInitializeExecuted);

        GetResultOrRunClassInitialize();
        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(_testClassInfo.ClassInitializationException is not null);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeMethod()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod")!;

        GetResultOrRunClassInitialize();

        Verify(classInitCallCount == 2);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassInitializeMethodIfClassInitializeHasExecuted()
    {
        int classInitCallCount = 0;

        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount += 2;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod")!;

        GetResultOrRunClassInitialize();
        Verify(_testClassInfo.IsClassInitializeExecuted);

        GetResultOrRunClassInitialize(); // this one shouldn't run
        Verify(classInitCallCount == 3);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeIfDerivedClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        GetResultOrRunClassInitialize();

        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassIfBaseClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();

        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions()
    {
        DummyBaseTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.InitBaseClassMethod threw exception. System.ArgumentException: Some exception message.");
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions>", StringComparison.Ordinal));
        Verify(exception.InnerException!.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException!.GetType() == typeof(InvalidOperationException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure.");
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>", StringComparison.Ordinal));
#endif
        Verify(exception.InnerException!.GetType() == typeof(AssertFailedException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(exception.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive.");
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>", StringComparison.Ordinal));
#endif
        Verify(exception.InnerException!.GetType() == typeof(AssertInconclusiveException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Argument exception");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: Argument exception.");
#if DEBUG
        Verify(exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>", StringComparison.Ordinal));
#endif
    }

    public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;
        _testClassInfo.ClassInitializationException = new TestFailedException(UTF.UnitTestOutcome.Failed, "Cached Test failure");

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception.Message == "Cached Test failure");
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => Verify(tc == _testContext);
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
    }

    public void RunClassInitializeShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc =>
            // This helper calls inner helper, and the inner helper ctor throws.
            // We want to see the real exception on screen, and not TypeInitializationException
            // which has no info about what failed.
            FailingStaticHelper.DoWork();
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        Verify(exception.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.InvalidOperationException: I fail..");
#if DEBUG
        Verify(
            exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()", StringComparison.Ordinal));
#endif
        Verify(exception.InnerException!.GetType() == typeof(InvalidOperationException));
    }

    private TestResult GetResultOrRunClassInitialize()
        => GetResultOrRunClassInitialize(_testContext);

    private TestResult GetResultOrRunClassInitialize(TestContext? testContext)
        => _testClassInfo.GetResultOrRunClassInitialize(new TestTestContextImpl(testContext!), string.Empty, string.Empty, string.Empty, string.Empty);
    #endregion

    #region Run Class Cleanup tests

    public void RunClassCleanupShouldInvokeIfClassCleanupMethod()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(ex is null);
        Verify(classCleanupCallCount == 1);
    }

    public void RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = null;

        // Act
        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(ex is null);
        Verify(classCleanupCallCount == 0);
    }

    public void RunClassCleanupShouldReturnAssertFailureExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(classCleanupException is not null);
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: Assert.Fail failed. Test Failure."));
#if DEBUG
        Verify(
    classCleanupException.Message.Contains(
    $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertFailureExceptionDetails)}>"),
    $"Value: {classCleanupException.Message}");
#endif
    }

    public void RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(classCleanupException is not null);
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: Assert.Inconclusive failed. Test Inconclusive."));
#if DEBUG
        Verify(
            classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails)}>"),
            $"Value: {classCleanupException.Message}");
#endif
    }

    public void RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(classCleanupException is not null);
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: System.ArgumentException: Argument Exception. Stack Trace:"));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>"));
    }

    public void RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyBaseTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod))!;
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(classCleanupException is not null);
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyBaseTestClass.CleanupClassMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: System.ArgumentException: Argument Exception. Stack Trace:"));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>"));
    }

    public void RunBaseClassCleanupEvenIfThereIsNoDerivedClassCleanup()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod))!;
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        TestFailedException? ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert
        Verify(ex is null);
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(classCleanupCallCount == 0, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 2
        GetResultOrRunClassInitialize(null);
        ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert 2
        Verify(ex is null);
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(_testClassInfo.IsClassInitializeExecuted);
        Verify(classCleanupCallCount == 1, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 3
        ex = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        // Assert 3
        Verify(ex is null);
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(classCleanupCallCount == 1, "DummyBaseTestClass.CleanupClassMethod call count");
    }

    public void RunClassCleanupShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        // This helper calls inner helper, and the inner helper ctor throws.
        // We want to see the real exception on screen, and not TypeInitializationException
        // which has no info about what failed.
        DummyTestClass.ClassCleanupMethodBody = FailingStaticHelper.DoWork;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;

        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = _testClassInfo.ExecuteClassCleanup(new TestContextImplementation(null, new StringWriter(), new Dictionary<string, object?>()));

        Verify(classCleanupException is not null);
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: System.InvalidOperationException: I fail..", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()"));
    }

    #endregion

    [DummyTestClass]
    public class DummyGrandParentTestClass
    {
        public static Action<object> ClassInitMethodBody { get; set; } = null!;

        public static Action CleanupClassMethodBody { get; set; } = null!;

        public TestContext BaseTestContext { get; set; } = null!;

        [ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitClassMethod(TestContext testContext) => ClassInitMethodBody?.Invoke(testContext);

        public static void ClassCleanupMethod() => CleanupClassMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyBaseTestClass : DummyGrandParentTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; } = null!;

        public static Action ClassCleanupMethodBody { get; set; } = null!;

        public TestContext TestContext { get; set; } = null!;

        [ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitBaseClassMethod(TestContext testContext) => ClassInitializeMethodBody?.Invoke(testContext);

        public static void CleanupClassMethod() => ClassCleanupMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyDerivedTestClass : DummyBaseTestClass
    {
        public static Action<object> DerivedClassInitializeMethodBody { get; set; } = null!;

        public static Action DerivedClassCleanupMethodBody { get; set; } = null!;

        public TestContext Context { get; set; } = null!;

        public static void InitDerivedClassMethod(TestContext testContext) => DerivedClassInitializeMethodBody?.Invoke(testContext);

        public static void CleanupDerivedClassMethod() => DerivedClassCleanupMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; } = null!;

        public static Action ClassCleanupMethodBody { get; set; } = null!;

        public TestContext TestContext { get; set; } = null!;

        public static void ClassInitializeMethod(TestContext testContext) => ClassInitializeMethodBody?.Invoke(testContext);

        public static void ClassCleanupMethod() => ClassCleanupMethodBody?.Invoke();
    }

    private class DummyTestClassAttribute : TestClassAttribute;

    private static class FailingStaticHelper
    {
        static FailingStaticHelper() => throw new InvalidOperationException("I fail.");

        public static void DoWork()
        {
        }
    }
}
