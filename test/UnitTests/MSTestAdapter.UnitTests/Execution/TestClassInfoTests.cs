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

public class TestClassInfoTests : TestContainer
{
    private readonly Type _testClassType;

    private readonly ConstructorInfo _testClassConstructor;

    private readonly PropertyInfo _testContextProperty;

    private readonly UTF.TestClassAttribute _testClassAttribute;

    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly UTFExtension.TestContext _testContext;

    private readonly TestClassInfo _testClassInfo;

    public TestClassInfoTests()
    {
        _testClassType = typeof(DummyTestClass);
        _testClassConstructor = _testClassType.GetConstructors().First();
        _testContextProperty = _testClassType.GetProperties().First();
        _testClassAttribute = (UTF.TestClassAttribute)_testClassType.GetCustomAttributes().First();
        _testAssemblyInfo = new TestAssemblyInfo(_testClassType.Assembly);

        _testClassInfo = new TestClassInfo(
            _testClassType,
            _testClassConstructor,
            true,
            _testContextProperty,
            _testClassAttribute,
            _testAssemblyInfo);

        var testContext = new Mock<UTFExtension.TestContext>();
        testContext.SetupGet(x => x.CancellationTokenSource).Returns(new CancellationTokenSource());
        _testContext = testContext.Object;

        // Prevent leaking init/cleanup methods between classes
        DummyGrandParentTestClass.ClassInitMethodBody = null;
        DummyGrandParentTestClass.CleanupClassMethodBody = null;
        DummyBaseTestClass.ClassInitializeMethodBody = null;
        DummyBaseTestClass.ClassCleanupMethodBody = null;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = null;
        DummyDerivedTestClass.DerivedClassCleanupMethodBody = null;
        DummyTestClass.ClassInitializeMethodBody = null;
        DummyTestClass.ClassCleanupMethodBody = null;
    }

    public void TestClassInfoClassAttributeGetsAReferenceToTheTestClassAttribute() => Verify(_testClassAttribute == _testClassInfo.ClassAttribute);

    public void TestClassInfoClassTypeGetsAReferenceToTheActualTypeForTheTestClass() => Verify(typeof(DummyTestClass) == _testClassInfo.ClassType);

    public void TestClassInfoConstructorGetsTheConstructorInfoForTestClass() => Verify(_testClassConstructor == _testClassInfo.Constructor);

    public void TestClassInfoTestContextPropertyGetsAReferenceToTheTestContextDefinedInTestClass() => Verify(_testContextProperty == _testClassInfo.TestContextProperty);

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

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.ExecuteClassCleanup(); // call cleanup without calling init
        Verify(classCleanupCallCount == 0);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        _testClassInfo.ExecuteClassCleanup(); // call cleanup without calling init

        Verify(classCleanupCallCount == 1);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeBaseClassCleanupMethodWhenTestClassInitializedIsCalled()
    {
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));
        _testClassInfo.BaseClassCleanupMethods.Add(typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod"));

        _testClassInfo.RunClassInitialize(_testContext);
        _testClassInfo.ExecuteClassCleanup();

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

        _testClassInfo.RunClassInitialize(null);

        Verify(classInitCallCount == 0);
    }

    public void RunClassInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        VerifyThrows<NullReferenceException>(() => _testClassInfo.RunClassInitialize(null));
    }

    public void RunClassInitializeShouldNotExecuteClassInitializeIfItHasAlreadyExecuted()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.IsClassInitializeExecuted = true;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 0);
    }

    public void RunClassInitializeShouldExecuteClassInitialize()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldSetClassInitializeExecutedFlag()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(_testClassInfo.IsClassInitializeExecuted);
    }

    public void RunClassInitializeShouldOnlyRunOnce()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 1, "Class Initialize called only once");
    }

    public void RunClassInitializeShouldRunOnlyOnceIfThereIsNoDerivedClassInitializeAndSetClassInitializeExecutedFlag()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));

        _testClassInfo.RunClassInitialize(_testContext);
        Verify(_testClassInfo.IsClassInitializeExecuted);

        _testClassInfo.RunClassInitialize(_testContext);
        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        VerifyThrows(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(_testClassInfo.ClassInitializationException is not null);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeMethod()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 2);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassInitializeMethodIfClassInitializeHasExecuted()
    {
        int classInitCallCount = 0;

        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount += 2;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        Verify(_testClassInfo.IsClassInitializeExecuted);

        _testClassInfo.RunClassInitialize(_testContext); // this one shouldn't run
        Verify(classInitCallCount == 3);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeIfDerivedClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassIfBaseClassInitializeIsNull()
    {
        int classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Verify(classInitCallCount == 1);
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions()
    {
        DummyBaseTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));

        _testClassInfo.BaseClassInitMethods.Add(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"));

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.InitBaseClassMethod threw exception. System.ArgumentException: Some exception message.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => throw new ArgumentException("Argument exception");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: Argument exception.");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>", StringComparison.Ordinal));
    }

    public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");
        _testClassInfo.ClassInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.Message == "Cached Test failure");
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => Verify(tc == _testContext);
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
    }

    public void RunClassInitializeShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc =>
            // This helper calls inner helper, and the inner helper ctor throws.
            // We want to see the real exception on screen, and not TypeInitializationException
            // which has no info about what failed.
            FailingStaticHelper.DoWork();
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        TestFailedException exception = VerifyThrows<TestFailedException>(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(
            exception.Message
            == "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.InvalidOperationException: I fail..");
        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()", StringComparison.Ordinal));
        Verify(exception.InnerException.GetType() == typeof(InvalidOperationException));
    }

    #endregion

    #region Run Class Cleanup tests

    public void RunClassCleanupShouldInvokeIfClassCleanupMethod()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        _testClassInfo.RunClassInitialize(null);
        _testClassInfo.ExecuteClassCleanup();

        // Assert
        Verify(classCleanupCallCount == 1);
    }

    public void RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = null;

        // Act
        _testClassInfo.ExecuteClassCleanup();

        // Assert
        Verify(classCleanupCallCount == 0);
    }

    public void RunClassCleanupShouldReturnAssertFailureExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        _testClassInfo.RunClassInitialize(null);
        Exception classCleanupException = VerifyThrows(_testClassInfo.ExecuteClassCleanup);

        // Assert
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: Assert.Fail failed. Test Failure."));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertFailureExceptionDetails)}>"));
    }

    public void RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        _testClassInfo.RunClassInitialize(null);
        Exception classCleanupException = VerifyThrows(_testClassInfo.ExecuteClassCleanup);

        // Assert
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: Assert.Inconclusive failed. Test Inconclusive."));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails)}>"));
    }

    public void RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        _testClassInfo.RunClassInitialize(null);
        Exception classCleanupException = VerifyThrows(_testClassInfo.ExecuteClassCleanup);

        // Assert
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: System.ArgumentException: Argument Exception. Stack Trace:"));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>"));
    }

    public void RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyBaseTestClass.ClassCleanupMethodBody = () => throw new ArgumentException("Argument Exception");
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        _testClassInfo.RunClassInitialize(null);
        Exception classCleanupException = VerifyThrows(_testClassInfo.ExecuteClassCleanup);

        // Assert
        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyBaseTestClass.CleanupClassMethod failed.", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("Error Message: System.ArgumentException: Argument Exception. Stack Trace:"));
        Verify(classCleanupException.Message.Contains($"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>"));
    }

    public void RunBaseClassCleanupEvenIfThereIsNoDerivedClassCleanup()
    {
        // Arrange
        int classCleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classCleanupCallCount++;
        MethodInfo baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassCleanupMethods.Add(baseClassCleanupMethod);

        // Act
        _testClassInfo.ExecuteClassCleanup();

        // Assert
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(classCleanupCallCount == 0, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 2
        _testClassInfo.RunClassInitialize(null);
        _testClassInfo.ExecuteClassCleanup();

        // Assert 2
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(_testClassInfo.IsClassInitializeExecuted);
        Verify(classCleanupCallCount == 1, "DummyBaseTestClass.CleanupClassMethod call count");

        // Act 3
        _testClassInfo.ExecuteClassCleanup();

        // Assert 3
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(classCleanupCallCount == 1, "DummyBaseTestClass.CleanupClassMethod call count");
    }

    public void RunClassCleanupShouldThrowTheInnerMostExceptionWhenThereAreMultipleNestedTypeInitializationExceptions()
    {
        // This helper calls inner helper, and the inner helper ctor throws.
        // We want to see the real exception on screen, and not TypeInitializationException
        // which has no info about what failed.
        DummyTestClass.ClassCleanupMethodBody = FailingStaticHelper.DoWork;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");

        _testClassInfo.RunClassInitialize(null);
        Exception classCleanupException = VerifyThrows(_testClassInfo.ExecuteClassCleanup);

        Verify(classCleanupException.Message.StartsWith("Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: System.InvalidOperationException: I fail..", StringComparison.Ordinal));
        Verify(classCleanupException.Message.Contains("at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.FailingStaticHelper..cctor()"));
    }

    #endregion

    [DummyTestClass]
    public class DummyGrandParentTestClass
    {
        public static Action<object> ClassInitMethodBody { get; set; }

        public static Action CleanupClassMethodBody { get; set; }

        public UTFExtension.TestContext BaseTestContext { get; set; }

        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitClassMethod(UTFExtension.TestContext testContext) => ClassInitMethodBody?.Invoke(testContext);

        public static void ClassCleanupMethod() => CleanupClassMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyBaseTestClass : DummyGrandParentTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitBaseClassMethod(UTFExtension.TestContext testContext) => ClassInitializeMethodBody?.Invoke(testContext);

        public static void CleanupClassMethod() => ClassCleanupMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyDerivedTestClass : DummyBaseTestClass
    {
        public static Action<object> DerivedClassInitializeMethodBody { get; set; }

        public static Action DerivedClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext Context { get; set; }

        public static void InitDerivedClassMethod(UTFExtension.TestContext testContext) => DerivedClassInitializeMethodBody?.Invoke(testContext);

        public static void CleanupDerivedClassMethod() => DerivedClassCleanupMethodBody?.Invoke();
    }

    [DummyTestClass]
    public class DummyTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        public static void ClassInitializeMethod(UTFExtension.TestContext testContext) => ClassInitializeMethodBody?.Invoke(testContext);

        public static void ClassCleanupMethod() => ClassCleanupMethodBody?.Invoke();
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute;

    private static class FailingStaticHelper
    {
        static FailingStaticHelper() => throw new InvalidOperationException("I fail.");

        public static void DoWork()
        {
        }
    }
}
