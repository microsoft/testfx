// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Linq;
using System.Reflection;

using global::MSTestAdapter.TestUtilities;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using Moq;

using MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

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
            _testContextProperty,
            _testClassAttribute,
            _testAssemblyInfo);

        _testContext = new Mock<UTFExtension.TestContext>().Object;
    }

    [TestInitialize]
    public void TestInitialize()
    {
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

    public void TestClassInfoClassAttributeGetsAReferenceToTheTestClassAttribute()
    {
        Assert.AreEqual(_testClassAttribute, _testClassInfo.ClassAttribute);
    }

    public void TestClassInfoClassTypeGetsAReferenceToTheActualTypeForTheTestClass()
    {
        Assert.AreEqual(typeof(DummyTestClass), _testClassInfo.ClassType);
    }

    public void TestClassInfoConstructorGetsTheConstructorInfoForTestClass()
    {
        Assert.AreEqual(_testClassConstructor, _testClassInfo.Constructor);
    }

    public void TestClassInfoTestContextPropertyGetsAReferenceToTheTestContextDefinedInTestClass()
    {
        Assert.AreEqual(_testContextProperty, _testClassInfo.TestContextProperty);
    }

    public void TestClassInfoParentGetsAReferenceToTheParentAssemblyForTheTestClass()
    {
        Assert.AreEqual(_testAssemblyInfo, _testClassInfo.Parent);
    }

    public void TestClassInfoClassInitializeMethodSetShouldThrowForMultipleClassInitializeMethods()
    {
        void action()
        {
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassInitializeMethod = _testClassType.GetMethods().First();
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    public void TestClassInfoClassCleanupMethodSetShouldThrowForMultipleClassCleanupMethods()
    {
        void action()
        {
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
            _testClassInfo.ClassCleanupMethod = _testClassType.GetMethods().First();
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    public void TestClassInfoClassCleanupMethodShouldNotInvokeWhenNoTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var ret = _testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Verify(ret is null);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    public void TestClassInfoClassCleanupMethodShouldNotInvokeBaseClassCleanupMethodsWhenNoTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                null,
                typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")));
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitBaseClassMethod");

        var ret = _testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Verify(ret is null);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeWhenTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        var ret = _testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Verify(ret is null);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    public void TestClassInfoClassCleanupMethodShouldInvokeBaseClassCleanupMethodWhenTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")));

        _testClassInfo.RunClassInitialize(_testContext);
        var ret = _testClassInfo.RunClassCleanup();

        Verify(ret is null);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassDoesNotHaveCleanupMethod()
    {
        Verify(!_testClassInfo.HasExecutableCleanupMethod);
    }

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
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.ClassInitializeMethod = null;

        _testClassInfo.RunClassInitialize(null);

        Assert.AreEqual(0, classInitCallCount);
    }

    public void RunClassInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };

        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        void action() => _testClassInfo.RunClassInitialize(null);

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
    }

    public void RunClassInitializeShouldNotExecuteClassInitializeIfItHasAlreadyExecuted()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.IsClassInitializeExecuted = true;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(0, classInitCallCount);
    }

    public void RunClassInitializeShouldExecuteClassInitialize()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(1, classInitCallCount);
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
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(1, classInitCallCount, "Class Initialize called only once");
    }

    public void RunClassInitializeShouldRunOnlyOnceIfThereIsNoDerivedClassInitializeAndSetClassInitializeExecutedFlag()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            Tuple.Create(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"), (MethodInfo)null));

        _testClassInfo.RunClassInitialize(_testContext);
        Verify(_testClassInfo.IsClassInitializeExecuted);

        _testClassInfo.RunClassInitialize(_testContext);
        Assert.AreEqual(1, classInitCallCount);
    }

    public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext));

        Verify(_testClassInfo.ClassInitializationException is not null);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeMethod()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(2, classInitCallCount);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassInitializeMethodIfClassInitializeHasExecuted()
    {
        var classInitCallCount = 0;

        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount += 2;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));
        _testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        _testClassInfo.RunClassInitialize(_testContext);
        Verify(_testClassInfo.IsClassInitializeExecuted);

        _testClassInfo.RunClassInitialize(_testContext); // this one shouldn't run
        Assert.AreEqual(3, classInitCallCount);
    }

    public void RunClassInitializeShouldExecuteBaseClassInitializeIfDerivedClassInitializeIsNull()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));

        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(1, classInitCallCount);
    }

    public void RunClassInitializeShouldNotExecuteBaseClassIfBaseClassInitializeIsNull()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(new Tuple<MethodInfo, MethodInfo>(null, null));
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);

        Assert.AreEqual(1, classInitCallCount);
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions()
    {
        DummyBaseTestClass.ClassInitializeMethodBody = tc => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };

        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(new Tuple<MethodInfo, MethodInfo>(
            typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
            null));

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.InitBaseClassMethod threw exception. System.ArgumentException: Some exception message.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
        Assert.IsInstanceOfType(exception.InnerException.InnerException, typeof(InvalidOperationException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertFailedException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertInconclusiveException));
    }

    public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { throw new ArgumentException("Argument exception"); };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: Argument exception.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>");
    }

    public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");
        _testClassInfo.ClassInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = ActionUtility.PerformActionAndReturnException(() => _testClassInfo.RunClassInitialize(_testContext)) as TestFailedException;

        Verify(exception is not null);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Cached Test failure",
            exception.Message);
    }

    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { Assert.AreEqual(tc, _testContext); };
        _testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        _testClassInfo.RunClassInitialize(_testContext);
    }

    #endregion

    #region Run Class Cleanup tests

    public void RunClassCleanupShouldInvokeIfClassCleanupMethod()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        Verify(classCleanup is null);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    public void RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        _testClassInfo.ClassCleanupMethod = null;

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        Verify(classCleanup is null);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    public void RunClassCleanupShouldReturnAssertFailureExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: Assert.Fail failed. Test Failure.");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertFailureExceptionDetails)}>");
    }

    public void RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive");
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: Assert.Inconclusive failed. Test Inconclusive.");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails)}>");
    }

    public void RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };
        _testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    public void RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyBaseTestClass.ClassCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };
        var baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(Tuple.Create((MethodInfo)null, baseClassCleanupMethod));
        _testClassInfo.BaseClassCleanupMethodsStack.Push(baseClassCleanupMethod);

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyBaseTestClass.CleanupClassMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    public void RunBaseClassCleanupEvenIfThereIsNoDerivedClassCleanup()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        var baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        _testClassInfo.ClassCleanupMethod = null;
        _testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(Tuple.Create((MethodInfo)null, baseClassCleanupMethod));
        _testClassInfo.BaseClassCleanupMethodsStack.Push(baseClassCleanupMethod);

        // Act
        var classCleanup = _testClassInfo.RunClassCleanup();

        // Assert
        Verify(_testClassInfo.HasExecutableCleanupMethod);
        Verify(classCleanup is null);
        Assert.AreEqual(1, classcleanupCallCount, "DummyBaseTestClass.CleanupClassMethod call count");
    }

    #endregion

    [DummyTestClass]
    public class DummyGrandParentTestClass
    {
        public static Action<object> ClassInitMethodBody { get; set; }

        public static Action CleanupClassMethodBody { get; set; }

        public UTFExtension.TestContext BaseTestContext { get; set; }

        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitClassMethod(UTFExtension.TestContext testContext)
        {
            ClassInitMethodBody?.Invoke(testContext);
        }

        public static void ClassCleanupMethod()
        {
            CleanupClassMethodBody?.Invoke();
        }
    }

    [DummyTestClass]
    public class DummyBaseTestClass : DummyGrandParentTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.ClassInitialize(UTF.InheritanceBehavior.BeforeEachDerivedClass)]
        public static void InitBaseClassMethod(UTFExtension.TestContext testContext)
        {
            ClassInitializeMethodBody?.Invoke(testContext);
        }

        public static void CleanupClassMethod()
        {
            ClassCleanupMethodBody?.Invoke();
        }
    }

    [DummyTestClass]
    public class DummyDerivedTestClass : DummyBaseTestClass
    {
        public static Action<object> DerivedClassInitializeMethodBody { get; set; }

        public static Action DerivedClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext Context { get; set; }

        public static void InitDerivedClassMethod(UTFExtension.TestContext testContext)
        {
            DerivedClassInitializeMethodBody?.Invoke(testContext);
        }

        public static void CleanupDerivedClassMethod()
        {
            DerivedClassCleanupMethodBody?.Invoke();
        }
    }

    [DummyTestClass]
    public class DummyTestClass
    {
        public static Action<object> ClassInitializeMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        public static void ClassInitializeMethod(UTFExtension.TestContext testContext)
        {
            ClassInitializeMethodBody?.Invoke(testContext);
        }

        public static void ClassCleanupMethod()
        {
            ClassCleanupMethodBody?.Invoke();
        }
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }
}
