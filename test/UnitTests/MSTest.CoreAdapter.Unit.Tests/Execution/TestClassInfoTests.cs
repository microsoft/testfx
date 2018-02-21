// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
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
    public class TestClassInfoTests
    {
        private readonly Type testClassType;

        private readonly ConstructorInfo testClassConstructor;

        private readonly PropertyInfo testContextProperty;

        private readonly UTF.TestClassAttribute testClassAttribute;

        private readonly TestAssemblyInfo testAssemblyInfo;

        private readonly TestClassInfo testClassInfo;

        private UTFExtension.TestContext testContext;

        public TestClassInfoTests()
        {
            this.testClassType = typeof(DummyTestClass);
            this.testClassConstructor = this.testClassType.GetConstructors().First();
            this.testContextProperty = this.testClassType.GetProperties().First();
            this.testClassAttribute = (UTF.TestClassAttribute)this.testClassType.GetCustomAttributes().First();
            this.testAssemblyInfo = new TestAssemblyInfo();

            this.testClassInfo = new TestClassInfo(
                this.testClassType,
                this.testClassConstructor,
                this.testContextProperty,
                this.testClassAttribute,
                this.testAssemblyInfo);

            this.testContext = new Mock<UTFExtension.TestContext>().Object;
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
            Action action = () =>
                {
                    this.testClassInfo.ClassInitializeMethod = this.testClassType.GetMethods().First();
                    this.testClassInfo.ClassInitializeMethod = this.testClassType.GetMethods().First();
                };

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TypeInspectionException));
        }

        [TestMethod]
        public void TestClassInfoClassCleanupMethodSetShouldThrowForMultipleClassCleanupMethods()
        {
            Action action = () =>
                {
                    this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
                    this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
                };

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

            Assert.AreEqual(null, ret);
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

            Assert.AreEqual(null, ret);
            Assert.AreEqual(1, classcleanupCallCount);
        }

        [TestMethod]
        public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassDoesNotHaveCleanupMethod()
        {
            Assert.IsFalse(this.testClassInfo.HasExecutableCleanupMethod);
        }

        [TestMethod]
        public void TestClassInfoHasExecutableCleanupMethodShouldReturnFalseIfClassInitializeThrowsAnException()
        {
            this.testClassInfo.ClassCleanupMethod = this.testClassType.GetMethods().First();
            this.testClassInfo.ClassInitializationException = new NotImplementedException();

            Assert.IsFalse(this.testClassInfo.HasExecutableCleanupMethod);
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
            DummyTestClass.ClassInitializeMethodBody = (tc) => classInitCallCount++;

            this.testClassInfo.ClassInitializeMethod = null;

            this.testClassInfo.RunClassInitialize(null);

            Assert.AreEqual(0, classInitCallCount);
        }

        [TestMethod]
        public void RunClassInitializeShouldThrowIfTestContextIsNull()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => { };

            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            Action action = () => this.testClassInfo.RunClassInitialize(null);

            ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(NullReferenceException));
        }

        [TestMethod]
        public void RunClassInitializeShouldNotExecuteClassInitializeIfItHasAlreadyExecuted()
        {
            var classInitCallCount = 0;
            DummyTestClass.ClassInitializeMethodBody = (tc) => classInitCallCount++;

            this.testClassInfo.IsClassInitializeExecuted = true;
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            this.testClassInfo.RunClassInitialize(this.testContext);

            Assert.AreEqual(0, classInitCallCount);
        }

        [TestMethod]
        public void RunClassInitializeShouldExecuteClassInitialize()
        {
            var classInitCallCount = 0;
            DummyTestClass.ClassInitializeMethodBody = (tc) => classInitCallCount++;
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            this.testClassInfo.RunClassInitialize(this.testContext);

            Assert.AreEqual(1, classInitCallCount);
        }

        [TestMethod]
        public void RunClassInitializeShouldSetClassInitializeExecutedFlag()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => { };
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            this.testClassInfo.RunClassInitialize(this.testContext);

            Assert.IsTrue(this.testClassInfo.IsClassInitializeExecuted);
        }

        [TestMethod]
        public void RunClassInitializeShouldSetClassInitializationExceptionOnException()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext));

            Assert.IsNotNull(this.testClassInfo.ClassInitializationException);
        }

        [TestMethod]
        public void RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => UTF.Assert.Fail("Test failure");
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
            StringAssert.Contains(exception.Message, "Test failure");
            StringAssert.StartsWith(
                exception.StackTraceInformation.ErrorStackTrace,
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionOnAssertionFailure>");
        }

        [TestMethod]
        public void RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => UTF.Assert.Inconclusive("Test Inconclusive");
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
            StringAssert.Contains(exception.Message, "Test Inconclusive");
            StringAssert.StartsWith(
                exception.StackTraceInformation.ErrorStackTrace,
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithInconclusiveOnAssertInconclusive>");
        }

        [TestMethod]
        public void RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => { throw new ArgumentException("Argument exception"); };
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testClassInfo.RunClassInitialize(this.testContext)) as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
            Assert.AreEqual(
                "Class Initialization method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests+DummyTestClass.ClassInitializeMethod threw exception. System.ArgumentException: System.ArgumentException: Argument exception.",
                exception.Message);
            StringAssert.StartsWith(
                exception.StackTraceInformation.ErrorStackTrace,
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassInitializeShouldThrowTestFailedExceptionWithNonAssertExceptions>");
        }

        [TestMethod]
        public void RunClassInitializeShouldThrowForAlreadyExecutedTestClassInitWithException()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => { };
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
        public void RunClassCleanupShouldInvokeIfClassCleanupMethod()
        {
           var classcleanupCallCount = 0;
            DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;
            this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
            Assert.IsNull(this.testClassInfo.RunClassCleanup());
            Assert.AreEqual(1, classcleanupCallCount);
        }

        [TestMethod]
        public void RunAssemblyInitializeShouldPassOnTheTestContextToAssemblyInitMethod()
        {
            DummyTestClass.ClassInitializeMethodBody = (tc) => { Assert.AreEqual(tc, this.testContext); };
            this.testClassInfo.ClassInitializeMethod = typeof(DummyTestClass).GetMethod("ClassInitializeMethod");

            this.testClassInfo.RunClassInitialize(this.testContext);
        }

        #endregion

        #region Run Class Cleanup tests

        [TestMethod]
        public void RunClassCleanupShouldNotInvokeIfClassCleanupIsNull()
        {
            var classcleanupCallCount = 0;
            DummyTestClass.ClassCleanupMethodBody = () => classcleanupCallCount++;

            this.testClassInfo.ClassCleanupMethod = null;

            Assert.IsNull(this.testClassInfo.RunClassCleanup());
            Assert.AreEqual(0, classcleanupCallCount);
        }

        [TestMethod]
        public void RunClassCleanupShouldReturnAssertFailureExceptionDetails()
        {
            DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Fail("Test Failure.");

            this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");

            StringAssert.StartsWith(
                this.testClassInfo.RunClassCleanup(),
                "Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: Assert.Fail failed. Test Failure.. Stack Trace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassCleanupShouldReturnAssertFailureExceptionDetails>");
        }

        [TestMethod]
        public void RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails()
        {
            DummyTestClass.ClassCleanupMethodBody = () => UTF.Assert.Inconclusive("Test Inconclusive.");

            this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
            StringAssert.StartsWith(
                this.testClassInfo.RunClassCleanup(),
                "Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: Assert.Inconclusive failed. Test Inconclusive.. Stack Trace:    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassCleanupShouldReturnAssertInconclusiveExceptionDetails>");
        }

        [TestMethod]
        public void RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions()
        {
            DummyTestClass.ClassCleanupMethodBody = () => { throw new ArgumentException("Argument Exception"); };

            this.testClassInfo.ClassCleanupMethod = typeof(DummyTestClass).GetMethod("ClassCleanupMethod");
            StringAssert.StartsWith(
                this.testClassInfo.RunClassCleanup(),
                "Class Cleanup method DummyTestClass.ClassCleanupMethod failed. Error Message: System.ArgumentException: Argument Exception. Stack Trace:     at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestClassInfoTests.<>c.<RunClassCleanupShouldReturnExceptionDetailsOfNonAssertExceptions>");
        }

        #endregion

        [UTF.TestClass]
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
    }
}