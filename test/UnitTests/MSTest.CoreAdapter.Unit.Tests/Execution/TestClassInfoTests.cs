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
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClassInfoTests
{
    private readonly Type testClassType;

    private readonly ConstructorInfo testClassConstructor;

    private readonly PropertyInfo testContextProperty;

    private readonly UTF.TestClassAttribute testClassAttribute;

    private readonly TestAssemblyInfo testAssemblyInfo;

    private readonly UTFExtension.TestContext testContext;

    private readonly TestClassInfo testClassInfo;

    public TestClassInfoTests()
    {
        this.testClassType = typeof(DummyTestClass);
        this.testClassConstructor = this.testClassType.GetConstructors().First();
        this.testContextProperty = this.testClassType.GetProperties().First();
        this.testClassAttribute = (UTF.TestClassAttribute)this.testClassType.GetCustomAttributes().First();
        this.testAssemblyInfo = new TestAssemblyInfo(this.testClassType.Assembly);

        this.testClassInfo = new TestClassInfo(
            this.testClassType,
            this.testClassConstructor,
            this.testContextProperty,
            this.testClassAttribute,
            this.testAssemblyInfo);

        this.testContext = new Mock<UTFExtension.TestContext>().Object;
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

    [TestMethod]
    public void TestClassInfoClassAttributeGetsAReferenceToTheTestClassAttribute()
    {
        Assert.AreEqual(this.testClassAttribute, this.testClassInfo.ClassAttribute);
    }

    [TestMethod]
    public void TestClassInfoClassTypeGetsAReferenceToTheActualTypeForTheTestClass()
    {
        Assert.AreEqual(typeof(DummyTestClass), this.testClassInfo.ClassType);
    }

    [TestMethod]
    public void TestClassInfoConstructorGetsTheConstructorInfoForTestClass()
    {
        Assert.AreEqual(this.testClassConstructor, this.testClassInfo.Constructor);
    }

    [TestMethod]
    public void TestClassInfoTestContextPropertyGetsAReferenceToTheTestContextDefinedInTestClass()
    {
        Assert.AreEqual(this.testContextProperty, this.testClassInfo.TestContextProperty);
    }

    [TestMethod]
    public void TestClassInfoParentGetsAReferenceToTheParentAssemblyForTheTestClass()
    {
        Assert.AreEqual(this.testAssemblyInfo, this.testClassInfo.Parent);
    }

    [TestMethod]
    public void TestClassInfoClassInitializeMethodSetShouldThrowForMultipleClassInitializeMethods()
    {
        void action()
        {
            this.testClassInfo.ClassInitializeMethod = this.testClassType.GetMethods().First();
            this.testClassInfo.ClassInitializeMethod = this.testClassType.GetMethods().First();
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestClassInfoClassCleanupMethodSetShouldThrowForMultipleClassCleanupMethods()
    {
        void action()
        {
            this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
            this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
        }

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
    }

    [TestMethod]
    public void TestClassInfoClassCleanupMethodShouldNotInvokeWhenNoTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var ret = this.testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Assert.IsNull(ret);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    [TestMethod]
    public void TestClassInfoClassCleanupMethodShouldNotInvokeBaseClassCleanupMethodsWhenNoTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                null,
                typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")));
        this.testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitBaseClassMethod");

        var ret = this.testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Assert.IsNull(ret);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    [TestMethod]
    public void TestClassInfoClassCleanupMethodShouldInvokeWhenTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);
        var ret = this.testClassInfo.RunClassCleanup(); // call cleanup without calling init

        Assert.IsNull(ret);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    [TestMethod]
    public void TestClassInfoClassCleanupMethodShouldInvokeBaseClassCleanupMethodWhenTestClassInitializedIsCalled()
    {
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                typeof(DummyBaseTestClass).GetMethod("CleanupClassMethod")));

        this.testClassInfo.RunClassInitialize(this.testContext);
        var ret = this.testClassInfo.RunClassCleanup();

        Assert.IsNull(ret);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    [TestMethod]
    public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassDoesNotHaveCleanupMethod()
    {
        Assert.IsFalse(this.testClassInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueEvenIfClassInitializeThrowsAnException()
    {
        this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
        this.testClassInfo.ClassInitializationException = new NotImplementedException();

        Assert.IsTrue(this.testClassInfo.HasExecutableCleanupMethod);
    }

    [TestMethod]
    public void TestClassInfoHasExecutableCleanupMethodShouldReturnTrueIfClassHasCleanupMethod()
    {
        this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();

        Assert.IsTrue(this.testClassInfo.HasExecutableCleanupMethod);
    }

    #region Run Class Initialize tests

    [TestMethod]
    public void RunClassInitializeShouldNotInvokeIfClassInitializeIsNull()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.ClassInitializeMethod = null;

        this.testClassInfo.RunClassInitialize(null);

        Assert.AreEqual(0, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldThrowIfTestContextIsNull()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };

        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        void action() => this.testClassInfo.RunClassInitialize(null);

        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
    }

    [TestMethod]
    public void RunClassInitializeShouldNotExecuteClassInitializeIfItHasAlreadyExecuted()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.IsClassInitializeExecuted = true;
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(0, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldExecuteClassInitialize()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(1, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldSetClassInitializeExecutedFlag()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.IsTrue(this.testClassInfo.IsClassInitializeExecuted);
    }

    [TestMethod]
    public void RunClassInitializeShouldOnlyRunOnce()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);
        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(1, classInitCallCount, "Class Initialize called only once");
    }

    [TestMethod]
    public void RunClassInitializeShouldRunOnlyOnceIfThereIsNoDerivedClassInitializeAndSetClassInitializeExecutedFlag()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            Tuple.Create(typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"), (MethodInfo)null));

        this.testClassInfo.RunClassInitialize(this.testContext);
        Assert.IsTrue(this.testClassInfo.IsClassInitializeExecuted);

        this.testClassInfo.RunClassInitialize(this.testContext);
        Assert.AreEqual(1, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext));

        Assert.IsNotNull(this.testClassInfo.ClassInitializationException);
    }

    [TestMethod]
    public void RunClassInitializeShouldExecuteBaseClassInitializeMethod()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));
        this.testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(2, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldNotExecuteBaseClassInitializeMethodIfClassInitializeHasExecuted()
    {
        var classInitCallCount = 0;

        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount += 2;
        DummyDerivedTestClass.DerivedClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));
        this.testClassInfo.ClassInitializeMethod = typeof(DummyDerivedTestClass).GetMethod("InitDerivedClassMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);
        Assert.IsTrue(this.testClassInfo.IsClassInitializeExecuted);

        this.testClassInfo.RunClassInitialize(this.testContext); // this one shouldn't run
        Assert.AreEqual(3, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldExecuteBaseClassInitializeIfDerivedClassInitializeIsNull()
    {
        var classInitCallCount = 0;
        DummyBaseTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(
            new Tuple<MethodInfo, MethodInfo>(
                typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
                null));

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(1, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldNotExecuteBaseClassIfBaseClassInitializeIsNull()
    {
        var classInitCallCount = 0;
        DummyTestClass.ClassInitializeMethodBody = tc => classInitCallCount++;

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(new Tuple<MethodInfo, MethodInfo>(null, null));
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);

        Assert.AreEqual(1, classInitCallCount);
    }

    [TestMethod]
    public void RunClassInitializeShouldThrowTestFailedExceptionOnBaseInitializeMethodWithNonAssertExceptions()
    {
        DummyBaseTestClass.ClassInitializeMethodBody = tc => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };

        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(new Tuple<MethodInfo, MethodInfo>(
            typeof(DummyBaseTestClass).GetMethod("InitBaseClassMethod"),
            null));

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
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

    [TestMethod]
    public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Fail("Test failure");
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failure.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertFailedException));
    }

    [TestMethod]
    public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => UTF.Assert.Inconclusive("Test Inconclusive");
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test Inconclusive.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>");
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertInconclusiveException));
    }

    [TestMethod]
    public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { throw new ArgumentException("Argument exception"); };
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: Argument exception.",
            exception.Message);
        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>");
    }

    [TestMethod]
    public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { };
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");
        this.testClassInfo.ClassInitializationException = new TestFailedException(UnitTestOutcome.Failed, "Cached Test failure");

        var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(
            "Cached Test failure",
            exception.Message);
    }

    [TestMethod]
    public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
    {
        DummyTestClass.ClassInitializeMethodBody = tc => { Assert.AreEqual(tc, this.testContext); };
        this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

        this.testClassInfo.RunClassInitialize(this.testContext);
    }

    #endregion

    #region Run Class Cleanup tests

    [TestMethod]
    public void RunClassCleanupShouldInvokeIfClassCleanupMethod()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        Assert.IsNull(classCleanup);
        Assert.AreEqual(1, classcleanupCallCount);
    }

    [TestMethod]
    public void RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        this.testClassInfo.ClassCleanupMethod = null;

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        Assert.IsNull(classCleanup);
        Assert.AreEqual(0, classcleanupCallCount);
    }

    [TestMethod]
    public void RunClassCleanupShouldReturnAssertFailureExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure");
        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: Assert.Fail failed. Test Failure.");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertFailureExceptionDetails)}>");
    }

    [TestMethod]
    public void RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive");
        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: Assert.Inconclusive failed. Test Inconclusive.");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails)}>");
    }

    [TestMethod]
    public void RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyTestClass.ClassCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };
        this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.ClassCleanupMethod));

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyTestClass.ClassCleanupMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    [TestMethod]
    public void RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
    {
        // Arrange
        DummyBaseTestClass.ClassCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };
        var baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        this.testClassInfo.ClassCleanupMethod = null;
        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(Tuple.Create((MethodInfo)null, baseClassCleanupMethod));
        this.testClassInfo.BaseClassCleanupMethodsStack.Push(baseClassCleanupMethod);

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        StringAssert.StartsWith(classCleanup, "Class Cleanup method DummyBaseTestClass.CleanupClassMethod failed.");
        StringAssert.Contains(classCleanup, "Error Message: System.ArgumentException: Argument Exception. Stack Trace:");
        StringAssert.Contains(classCleanup, $"{typeof(TestClassInfoTests).FullName}.<>c.<{nameof(this.RunBaseClassCleanupWithNoDerivedClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions)}>");
    }

    [TestMethod]
    public void RunBaseClassCleanupEvenIfThereIsNoDerivedClassCleanup()
    {
        // Arrange
        var classcleanupCallCount = 0;
        DummyBaseTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
        var baseClassCleanupMethod = typeof(DummyBaseTestClass).GetMethod(nameof(DummyBaseTestClass.CleanupClassMethod));
        this.testClassInfo.ClassCleanupMethod = null;
        this.testClassInfo.BaseClassInitAndCleanupMethods.Enqueue(Tuple.Create((MethodInfo)null, baseClassCleanupMethod));
        this.testClassInfo.BaseClassCleanupMethodsStack.Push(baseClassCleanupMethod);

        // Act
        var classCleanup = this.testClassInfo.RunClassCleanup();

        // Assert
        Assert.IsTrue(this.testClassInfo.HasExecutableCleanupMethod);
        Assert.IsNull(classCleanup);
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
