// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

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

    public void TestClassInfoClassAttributeGetsAReferenceToTheTestClassAttribute()
        => _testClassInfo.ClassAttribute.Should().Be(_testClassAttribute);

    public void TestClassInfoClassTypeGetsAReferenceToTheActualTypeForTheTestClass()
        => _testClassInfo.ClassType.Should().Be<DummyTestClass>();

    public void TestClassInfoConstructorGetsTheConstructorInfoForTestClass()
        => _testClassInfo.Constructor.Should().BeSameAs(_testClassConstructor);

    public void TestClassInfoTestContextPropertyGetsAReferenceToTheTestContextDefinedInTestClass()
        => _testClassInfo.TestContextProperty.Should().BeSameAs(_testClassType.GetProperty("TestContext"));

    public void TestClassInfoParentGetsAReferenceToTheParentAssemblyForTheTestClass()
        => _testClassInfo.Parent.Should().Be(_testAssemblyInfo);

    public void TestClassInfoClassInitializeMethodSetShouldThrowForMultipleClassInitializeMethods()
    {
        void Action()
        {
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
        }

        new Action(Action).Should().Throw<TypeInspectionException>();
    }

    public void TestClassInfoClassCleanupMethodSetShouldThrowForMultipleClassCleanupMethods()
    {
        void Action()
        {
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
        }

        new Action(Action).Should().Throw<TypeInspectionException>();
    }

    private static TestContextImplementation CreateDummyTestContextImplementation()
        => new(null, null, new Dictionary<string, object?>(), null, null);

    public async Task TestClassInfoClassCleanupMethodShouldNotInvokeWhenNoTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation()); // call cleanup without calling init
        ex.Should().BeNull();
        classCleanupCallCount.Should().Be(0);
    }

    public async Task TestClassInfoClassCleanupMethodShouldInvokeWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation()); // call cleanup without calling init

        ex.Should().BeNull();
        classCleanupCallCount.Should().Be(1);
    }

    public async Task TestClassInfoClassCleanupMethodShouldInvokeBaseClassCleanupMethodWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.BaseClassCleanupMethods.Add(typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")!);

        GetResultOrRunClassInitialize();
        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        ex.Should().BeNull();
        classCleanupCallCount.Should().Be(1);
    }

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassDoesNotHaveCleanupMethod() => _testClassInfo.HasExecutableCleanupMethod.Should().BeFalse();

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueEvenIfClassInitializeThrowsAnException()
    {
        _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
        _testClassInfo.ClassInitializationException = new NotImplementedException();

        _testClassInfo.HasExecutableCleanupMethod.Should().BeTrue();
    }

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueIfClassHasCleanupMethod()
    {
        _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();

        _testClassInfo.HasExecutableCleanupMethod.Should().BeTrue();
    }

    #region Run Class Initialize tests

    public void RunClassInitializeShouldNotInvokeIfClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = null;

        GetResultOrRunClassInitialize(null);

        classInitCallCount.Should().Be(0);
    }

    public void RunClassInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        TestResult result = GetResultOrRunClassInitialize(null);
        var exception = result.TestFailureException as TestFailedException;
        exception.Should().NotBeNull()
            .WithMessage("TestContext cannot be Null.");
        result.Outcome.Should().Be(UTF.UnitTestOutcome.Error);
    }

    public void RunClassInitializeShouldExecuteClassInitialize()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        classInitCallCount.Should().Be(1);
        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();

        GetResultOrRunClassInitialize();
        classInitCallCount.Should().Be(1);
        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();
    }

    public void RunClassInitializeShouldSetClassInitializeExecutedFlag()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();

        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();
    }

    public void RunClassInitializeShouldOnlyRunOnce()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();
        GetResultOrRunClassInitialize();

        classInitCallCount.Should().Be(1, "Class Initialize called only once");
    }

    public void RunClassInitializeShouldRunOnlyOnceIfThereIsNoDerivedClassInitializeAndSetClassInitializeExecutedFlag()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        GetResultOrRunClassInitialize();
        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();

        GetResultOrRunClassInitialize();
        classInitCallCount.Should().Be(1);
    }

    public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        _testClassInfo.ClassInitializationException.Should().NotBeNull();
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeMethod()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod")!;

        GetResultOrRunClassInitialize();

        classInitCallCount.Should().Be(2);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassInitializeMethodIfClassInitializeHasExecuted()
    {
        int classInitCallCount = 0;

        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount += 2;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod")!;

        GetResultOrRunClassInitialize();
        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();

        GetResultOrRunClassInitialize(); // this one shouldn't run
        classInitCallCount.Should().Be(3);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeIfDerivedClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        GetResultOrRunClassInitialize();

        classInitCallCount.Should().Be(1);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassIfBaseClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        GetResultOrRunClassInitialize();

        classInitCallCount.Should().Be(1);
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions()
    {
        DummyBaseTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod")!);

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.InitBaseClassMethod threw exception. System.ArgumentException: Some exception message.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions>", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<ArgumentException>();
        exception.InnerException.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.Contains(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<AssertFailedException>();
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Inconclusive);
        exception.Message.Should().Be("Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.Contains(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<AssertInconclusiveException>();
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Argument exception");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: Argument exception.");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
    "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>", StringComparison.Ordinal).Should().BeTrue();
#endif
    }

    public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod")!;
        _testClassInfo.ClassInitializationException = new TestFailedException(UTF.UnitTestOutcome.Failed, "Cached Test failure");

        var exception = GetResultOrRunClassInitialize().TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Cached Test failure");
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => (tc == _testContext).Should().BeTrue();
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

        exception.Outcome.Should().Be(UTF.UnitTestOutcome.Failed);
        exception.Message.Should().Be("Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.InvalidOperationException: I fail..");
#if DEBUG
        exception.StackTraceInformation!.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()", StringComparison.Ordinal).Should().BeTrue();
#endif
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    private TestResult GetResultOrRunClassInitialize()
        => GetResultOrRunClassInitialize(_testContext);

    private TestResult GetResultOrRunClassInitialize(TestContext? testContext)
        => _testClassInfo.GetResultOrRunClassInitializeAsync(new TestTestContextImpl(testContext!), string.Empty, string.Empty, string.Empty, string.Empty).GetAwaiter().GetResult();
    #endregion

    #region Run Class Cleanup tests

    public async Task RunClassCleanupShouldInvokeIfClassCleanupMethod()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        ex.Should().BeNull();
        classCleanupCallCount.Should().Be(1);
    }

    public async Task RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = null;

        // Act
        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        ex.Should().BeNull();
        classCleanupCallCount.Should().Be(0);
    }

    public async Task RunClassCleanupShouldReturnAssertFailureExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        classCleanupException.Should().NotBeNull();
        classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal).Should().BeTrue();
        classCleanupException.Message.Contains("Error Message: Assert.Fail failed. Test Failure.").Should().BeTrue();
#if DEBUG
        classCleanupException.Message.Should().Contain(
            $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertFailureExceptionDetails)}>",
            $"Value: {classCleanupException.Message}");
#endif
    }

    public async Task RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        classCleanupException.Should().NotBeNull();
        classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal).Should().BeTrue();
        classCleanupException.Message.Contains("Error Message: Assert.Inconclusive failed. Test Inconclusive.").Should().BeTrue();
#if DEBUG
        classCleanupException.Message.Should().Contain(
            $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails)}>",
            $"Value: {classCleanupException.Message}");
#endif
    }

    public async Task RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        classCleanupException.Should().NotBeNull();
        classCleanupException.Message.Should().StartWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        classCleanupException.Message.Should().Contain("Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        classCleanupException.Message.Should().Contain($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    public async Task RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyBaseTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod))!;
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        classCleanupException.Should().NotBeNull();
        classCleanupException.Message.Should().StartWith("Class Cleanup method DummyBaseTestClass.CleanupClassMethod failed.");
        classCleanupException.Message.Should().Contain("Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        classCleanupException.Message.Should().Contain($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    public async Task RunBaseClassCleanupEvenIfThereIsNoDerivedClassCleanup()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod))!;
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        TestFailedException? ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert
        ex.Should().BeNull();
        _testClassInfo.HasExecutableCleanupMethod.Should().BeTrue();
        classCleanupCallCount.Should().Be(0, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 2
        GetResultOrRunClassInitialize(null);
        ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert 2
        ex.Should().BeNull();
        _testClassInfo.HasExecutableCleanupMethod.Should().BeTrue();
        _testClassInfo.IsClassInitializeExecuted.Should().BeTrue();
        classCleanupCallCount.Should().Be(1, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 3
        ex = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        // Assert 3
        ex.Should().BeNull();
        _testClassInfo.HasExecutableCleanupMethod.Should().BeTrue();
        classCleanupCallCount.Should().Be(1, "DummyBaseTestClass.CleanupClassMethod call count");
    }

    public async Task RunClassCleanupShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        // This helper calls inner helper, and the inner helper ctor throws.
        // We want to see the real exception on screen, and not TypeInitializationException
        // which has no info about what failed.
        DummyTestClass.ClassCleanupMethodBody = FailingStaticHelper.DoWork;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod")!;

        GetResultOrRunClassInitialize(null);
        TestFailedException? classCleanupException = await _testClassInfo.ExecuteClassCleanupAsync(CreateDummyTestContextImplementation());

        classCleanupException.Should().NotBeNull();
        classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: System.InvalidOperationException: I fail..", StringComparison.Ordinal).Should().BeTrue();
        classCleanupException.Message.Contains("at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()").Should().BeTrue();
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
