// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The test method info tests.
    /// </summary>
    [TestClass]
    public class TestMethodInfoTests
    {
        private readonly TestMethodInfo testMethodInfo;

        private readonly MethodInfo methodInfo;

        private readonly UTF.TestClassAttribute classAttribute;

        private readonly UTF.TestMethodAttribute testMethodAttribute;

        private readonly PropertyInfo testContextProperty;

        private readonly TestAssemblyInfo testAssemblyInfo;

        private readonly ConstructorInfo constructorInfo;

        private readonly TestContextImplementation testContextImplementation;

        private readonly TestClassInfo testClassInfo;

        private readonly UTF.ExpectedExceptionAttribute expectedException;

        private readonly TestMethodOptions testMethodOptions;

        public TestMethodInfoTests()
        {
            this.constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            this.methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
            this.classAttribute = new UTF.TestClassAttribute();
            this.testMethodAttribute = new UTF.TestMethodAttribute();
            this.testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            this.testAssemblyInfo = new TestAssemblyInfo();
            var testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
            this.testContextImplementation = new TestContextImplementation(testMethod, new StringWriter(), new Dictionary<string, object>());
            this.testClassInfo = new TestClassInfo(
                type: typeof(DummyTestClass),
                constructor: this.constructorInfo,
                testContextProperty: this.testContextProperty,
                classAttribute: this.classAttribute,
                parent: this.testAssemblyInfo);
            this.expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));
            this.testMethodOptions = new TestMethodOptions()
            {
                Timeout = 3600 * 1000,
                Executor = this.testMethodAttribute,
                ExpectedException = null,
                TestContext = this.testContextImplementation
            };

            this.testMethodInfo = new TestMethodInfo(
                this.methodInfo,
                parent: this.testClassInfo,
                testmethodOptions: this.testMethodOptions);

            // Reset test hooks
            DummyTestClass.TestConstructorMethodBody = () => { };
            DummyTestClass.TestContextSetterBody = value => { };
            DummyTestClass.TestInitializeMethodBody = value => { };
            DummyTestClass.TestMethodBody = instance => { };
            DummyTestClass.TestCleanupMethodBody = value => { };
        }

        [TestMethodV1]
        public void SetArgumentsShouldSetArgumentsNeededForCurrentTestRun()
        {
            object[] arguments = new object[] { 10, 20, 30 };
            this.testMethodInfo.SetArguments(arguments);

            Assert.AreEqual(3, this.testMethodInfo.Arguments.Length);
            Assert.AreEqual(10, this.testMethodInfo.Arguments[0]);
            Assert.AreEqual(20, this.testMethodInfo.Arguments[1]);
            Assert.AreEqual(30, this.testMethodInfo.Arguments[2]);
        }

        #region TestMethod invoke scenarios

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
        {
            var methodCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { methodCalled = true; });
            var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var method = new TestMethodInfo(
                asyncMethodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            Assert.IsTrue(methodCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeAsyncShouldHandleThrowAssertInconclusive()
        {
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { throw new UTF.AssertInconclusiveException(); });
            var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var method = new TestMethodInfo(
                asyncMethodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeAsyncShouldHandleAssertInconclusive()
        {
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { UTF.Assert.Inconclusive(); });
            var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var method = new TestMethodInfo(
                asyncMethodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldHandleThrowAssertInconclusive()
        {
            DummyTestClass.TestMethodBody = (d) => { throw new UTF.AssertInconclusiveException(); };
            var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

            var method = new TestMethodInfo(
                dummyMethodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldHandleAssertInconclusive()
        {
            DummyTestClass.TestMethodBody = (d) => { UTF.Assert.Inconclusive(); };
            var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

            var method = new TestMethodInfo(
                dummyMethodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldListenForDebugAndTraceLogsWhenEnabled()
        {
            this.testMethodOptions.CaptureDebugTraces = true;

            StringWriter writer = new StringWriter(new StringBuilder());
            DummyTestClass.TestMethodBody = o => { writer.Write("Trace logs"); };

            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
            {
                testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

                PlatformServiceProvider.Instance = testablePlatformServiceProvider;
                var result = method.Invoke(null);

                Assert.AreEqual("Trace logs", result.DebugTrace);
            });
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotListenForDebugAndTraceLogsWhenDisabled()
        {
            this.testMethodOptions.CaptureDebugTraces = false;

            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);
            StringWriter writer = new StringWriter(new StringBuilder());

            DummyTestClass.TestMethodBody = o => { writer.Write("Trace logs"); };

            var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
            {
                testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

                PlatformServiceProvider.Instance = testablePlatformServiceProvider;
                var result = method.Invoke(null);

                Assert.AreEqual(string.Empty, result.DebugTrace);
            });
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldReportTestContextMessages()
        {
            DummyTestClass.TestMethodBody = o => { this.testContextImplementation.WriteLine("TestContext"); };

            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            StringAssert.Contains(result.TestContextMessages, "TestContext");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldClearTestContextMessagesAfterReporting()
        {
            DummyTestClass.TestMethodBody = o => { this.testContextImplementation.WriteLine("TestContext"); };

            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            var result = method.Invoke(null);

            StringAssert.Contains(result.TestContextMessages, "TestContext");

            DummyTestClass.TestMethodBody = o => { this.testContextImplementation.WriteLine("SeaShore"); };

            result = method.Invoke(null);

            StringAssert.Contains(result.TestContextMessages, "SeaShore");
        }

        #endregion

        #region TestClass constructor setup

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCreateNewInstanceOfTestClassOnEveryCall()
        {
            var ctorCallCount = 0;
            DummyTestClass.TestConstructorMethodBody = () => ctorCallCount++;

            var result = this.testMethodInfo.Invoke(null);
            this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            Assert.AreEqual(2, ctorCallCount);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

            var result = this.testMethodInfo.Invoke(null);

            var errorMessage = string.Format(
                Resource.UTA_InstanceCreationError,
                typeof(DummyTestClass).FullName,
                "System.NotImplementedException: dummyExceptionMessage");
            Assert.AreEqual(errorMessage, result.TestFailureException.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrowsWithoutInnerException()
        {
            var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, this.testContextProperty, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, testClass, this.testMethodOptions);

            var result = method.Invoke(null);
            var errorMessage = string.Format(
                Resource.UTA_InstanceCreationError,
                typeof(DummyTestClassWithParameterizedCtor).FullName,
                "System.Reflection.TargetParameterCountException: Parameter count mismatch.");

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
            Assert.AreEqual(errorMessage, result.TestFailureException.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrowsWithoutInnerException()
        {
            var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, this.testContextProperty, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, testClass, this.testMethodOptions);

            var exception = method.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultFilesIfTestContextHasAttachments()
        {
            Mock<ITestContext> testContext = new Mock<ITestContext>();
            testContext.Setup(tc => tc.GetResultFiles()).Returns(new List<string>() { "C:\\temp.txt" });
            var mockInnerContext = new Mock<UTFExtension.TestContext>();
            testContext.SetupGet(tc => tc.Context).Returns(mockInnerContext.Object);
            mockInnerContext.SetupGet(tc => tc.CancellationTokenSource).Returns(new CancellationTokenSource());
            this.testMethodOptions.TestContext = testContext.Object;

            var method = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

            var result = method.Invoke(null);
            CollectionAssert.Contains(result.ResultFiles.ToList(), "C:\\temp.txt");
        }

        #endregion

        #region TestClass.TestContext property setup
        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
        {
            var testClass = new TestClassInfo(typeof(DummyTestClass), this.constructorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, testClass, this.testMethodOptions);

            UTF.TestResult result = null;
            Action runMethod = () => result = method.Invoke(null);

            runMethod();
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
        {
            var testContext = typeof(DummyTestClassWithTestContextWithoutSetter).GetProperties().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClass), this.constructorInfo, testContext, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, testClass, this.testMethodOptions);

            UTF.TestResult result = null;
            Action runMethod = () => result = method.Invoke(null);

            runMethod();
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
        {
            UTFExtension.TestContext testContext = null;
            DummyTestClass.TestContextSetterBody = context => testContext = context as UTFExtension.TestContext;

            this.testMethodInfo.Invoke(null);

            Assert.AreSame(this.testContextImplementation, testContext);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
        {
            DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException(); };

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfSetTestContextThrows()
        {
            DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException("dummyExceptionMessage"); };

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            var errorMessage = string.Format(
                Resource.UTA_TestContextSetError,
                typeof(DummyTestClass).FullName,
                "System.NotImplementedException: dummyExceptionMessage");
            Assert.IsNotNull(exception);
            Assert.AreEqual(errorMessage, exception?.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows>b__");
        }

        #endregion

        #region TestInitialize method setup

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestInitialize()
        {
            var testInitializeCalled = false;
            DummyTestClass.TestInitializeMethodBody = classInstance => testInitializeCalled = true;
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testInitializeCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
        {
            var testInitializeCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { testInitializeCalled = true; });
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testInitializeCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestInitializeOfAllBaseClasses()
        {
            var callOrder = new List<string>();
            DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestInitializeCalled2"); };
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { callOrder.Add("baseAsyncTestInitializeCalled1"); });
            DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("classTestInitializeCalled");
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            this.testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
            this.testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod"));

            var result = this.testMethodInfo.Invoke(null);

            var expectedCallOrder = new List<string>
                                        {
                                            "baseAsyncTestInitializeCalled1",
                                            "baseTestInitializeCalled2",
                                            "classTestInitializeCalled"
                                        };
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeIsNull()
        {
            this.testClassInfo.TestInitializeMethod = null;

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
        {
            this.testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null);

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldMarkOutcomeAsFailIfTestInitializeThrows()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestInitializeThrows()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            var errorMessage = string.Format(
                Resource.UTA_InitMethodThrows,
                typeof(DummyTestClass).FullName,
                this.testClassInfo.TestInitializeMethod.Name,
                "System.NotImplementedException: dummyErrorMessage");

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(errorMessage, exception?.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestInitializeThrows()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestInitializeThrows>b__");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestInitializeThrowsUnitTestAssertException()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            const string ErrorMessage = "Assert.Fail failed. dummyFailMessage";

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(ErrorMessage, exception?.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestInitializeThrowsUnitTestAssertException()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestInitializeThrowsUnitTestAssertException>b__");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetTestInitializeExceptionEvenIfMethodHasExpectedExceptionAttriute()
        {
            // Arrange.
            DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            const string ErrorMessage = "Assert.Fail failed. dummyFailMessage";

            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

            // Act.
            var exception = testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            // Assert.
            Assert.IsNotNull(exception);
            Assert.AreEqual(ErrorMessage, exception?.Message);
        }

        #endregion

        #region TestCleanup method setup

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanup()
        {
            var cleanupMethodCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            Assert.IsTrue(cleanupMethodCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallAsyncTestCleanup()
        {
            var cleanupMethodCalled = false;
            DummyTestClass.TestCleanupMethodBody = classInstance => { cleanupMethodCalled = true; };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            Assert.IsTrue(cleanupMethodCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
        {
            this.testClassInfo.TestCleanupMethod = null;

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodForBaseClassIsNull()
        {
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(null);

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClasses()
        {
            var callOrder = new List<string>();
            DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
            DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

            var result = this.testMethodInfo.Invoke(null);

            var expectedCallOrder = new List<string>
                                        {
                                            "classTestCleanupCalled",
                                            "baseTestCleanupCalled1",
                                            "baseTestCleanupCalled2"
                                        };
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClassesAlways()
        {
            var callOrder = new List<string>();
            DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
            DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

            var result = this.testMethodInfo.Invoke(null);
            result = this.testMethodInfo.Invoke(null);

            var expectedCallOrder = new List<string>
                                        {
                                            "classTestCleanupCalled",
                                            "baseTestCleanupCalled1",
                                            "baseTestCleanupCalled2",
                                            "classTestCleanupCalled",
                                            "baseTestCleanupCalled4",
                                            "baseTestCleanupCalled5"
                                        };

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldMarkOutcomeAsFailedIfTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;
            var errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_CleanupMethodThrows,
                typeof(DummyTestClass).FullName,
                this.testClassInfo.TestCleanupMethod.Name,
                typeof(NotImplementedException).ToString(),
                "System.NotImplementedException: dummyErrorMessage");

            Assert.IsNotNull(exception);
            Assert.AreEqual(errorMessage, exception?.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace,
                "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestCleanupThrows>b__");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldAppendErrorMessagesIfBothTestMethodAndTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
            DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);
            var exception = result.TestFailureException as TestFailedException;
            var errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestMethodThrows,
                typeof(DummyTestClass).FullName,
                this.testMethodInfo.TestMethodName,
                "System.NotImplementedException: dummyMethodError");
            var cleanupError = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_CleanupMethodThrows,
                typeof(DummyTestClass).FullName,
                this.testClassInfo.TestCleanupMethod.Name,
                typeof(NotImplementedException).ToString(),
                "System.NotImplementedException: dummyErrorMessage");

            Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
            Assert.IsNotNull(exception);
            Assert.AreEqual(string.Concat(errorMessage, Environment.NewLine, cleanupError), exception?.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldAppendStackTraceInformationIfBothTestMethodAndTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);
            var exception = result.TestFailureException as TestFailedException;

            Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
            Assert.IsNotNull(exception);
            StringAssert.Contains(exception?.StackTraceInformation.ErrorStackTrace, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestMethod()");
            StringAssert.Contains(exception?.StackTraceInformation.ErrorStackTrace, Resource.UTA_CleanupStackTrace);
            StringAssert.Contains(exception?.StackTraceInformation.ErrorStackTrace, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestCleanupMethod()");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetOutcomeAsInconclusiveIfTestCleanupIsInconclusive()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);
            var exception = result.TestFailureException as TestFailedException;

            Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Inconclusive);
            Assert.IsNotNull(exception);
            StringAssert.Contains(exception?.Message, "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetMoreImportantOutcomeIfTestCleanupIsInconclusiveButTestMethodFails()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
            DummyTestClass.TestMethodBody = classInstance => { Assert.Fail(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);
            var exception = result.TestFailureException as TestFailedException;

            Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
        {
            var disposeCalled = false;
            DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
            var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, this.testMethodOptions);

            method.Invoke(null);

            Assert.IsTrue(disposeCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClassIfTestCleanupThrows()
        {
            var disposeCalled = false;
            DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
            DummyTestClassWithDisposable.DummyTestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, this.classAttribute, this.testAssemblyInfo);
            testClass.TestCleanupMethod = typeof(DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod");
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, this.testMethodOptions);

            method.Invoke(null);

            Assert.IsTrue(disposeCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
        {
            var testCleanupMethodCalled = false;
            DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testCleanupMethodCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
        {
            var testCleanupMethodCalled = false;
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testCleanupMethodCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
        {
            var testCleanupMethodCalled = false;

            // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsFalse(testCleanupMethodCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
        {
            var testCleanupCalled = false;
            DummyTestClass.TestCleanupMethodBody = classInstance => { testCleanupCalled = true; };
            DummyTestClass.TestContextSetterBody = o => { throw new NotImplementedException(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            this.testMethodInfo.Invoke(null);

            Assert.IsFalse(testCleanupCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
        {
            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

            var result = testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
        {
            DummyTestClass.TestMethodBody = o => { throw new IndexOutOfRangeException(); };
            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

            var result = testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
            var message = "Test method threw exception System.IndexOutOfRangeException, but exception System.DivideByZeroException was expected. " +
                "Exception message: System.IndexOutOfRangeException: Index was outside the bounds of the array.";
            Assert.AreEqual(message, result.TestFailureException.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsFailedWhenExceptionIsExpectedButIsNotThrown()
        {
            DummyTestClass.TestMethodBody = o => { return; };
            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);
            var result = testMethodInfo.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
            var message = "Test method did not throw expected exception System.DivideByZeroException.";
            StringAssert.Contains(result.TestFailureException.Message, message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
        {
            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);
            var result = testMethodInfo.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
            Assert.AreEqual(message, result.TestFailureException.Message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetTestOutcomeBeforeTestCleanup()
        {
            UTF.UnitTestOutcome testOutcome = UTF.UnitTestOutcome.Unknown;
            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            DummyTestClass.TestCleanupMethodBody = c =>
                        {
                            if (DummyTestClass.GetTestContext() != null)
                            {
                                testOutcome = DummyTestClass.GetTestContext().CurrentTestOutcome;
                            }
                        };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
            this.testMethodOptions.ExpectedException = this.expectedException;
            var testMethodInfo = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

            var result = testMethodInfo.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, testOutcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Attempted to divide by zero");
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = customExpectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(true, customExpectedException.IsVerifyInvoked);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = customExpectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(result.TestFailureException.Message, "The exception message doesn't contain the string defined in the exception attribute");
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldSetOutcomeAsInconclusveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = customExpectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            var result = method.Invoke(null);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldInvokeVerifyOfDerivedCustomExpectedException()
        {
            DerivedCustomExpectedExceptionAttribute derivedCustomExpectedException = new DerivedCustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Attempted to divide by zero");
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = derivedCustomExpectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(true, derivedCustomExpectedException.IsVerifyInvoked);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(Exception));
            expectedException.AllowDerivedTypes = true;
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            expectedException.AllowDerivedTypes = true;
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new ArgumentNullException(); };
            var result = method.Invoke(null);
            var message = "Test method threw exception System.ArgumentNullException, but exception System.DivideByZeroException" +
                " or a type derived from it was expected. Exception message: System.ArgumentNullException: Value cannot be null.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertFailedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));
            expectedException.AllowDerivedTypes = true;
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertFailedException(); };
            var result = method.Invoke(null);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertInconclusiveException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));
            expectedException.AllowDerivedTypes = true;
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            var result = method.Invoke(null);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldThrowIfThrownExceptionIsNotSameAsExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(Exception));
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            var message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. " +
                "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(Exception));
            this.testMethodOptions.Timeout = 0;
            this.testMethodOptions.ExpectedException = expectedException;
            var method = new TestMethodInfo(
                this.methodInfo,
                this.testClassInfo,
                this.testMethodOptions);

            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            var result = method.Invoke(null);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        }

        #endregion

        #region TestMethod invoke setup order

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldInitializeClassInstanceTestInitializeAndTestCleanupInOrder()
        {
            var callOrder = new List<string>();
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            DummyTestClass.TestConstructorMethodBody = () => { callOrder.Add("classCtor"); };
            DummyTestClass.TestContextSetterBody = o => { callOrder.Add("testContext"); };
            DummyTestClass.TestInitializeMethodBody = classInstance => { callOrder.Add("testInit"); };
            DummyTestClass.TestMethodBody = classInstance => { callOrder.Add("testMethod"); };
            DummyTestClass.TestCleanupMethodBody = classInstance => { callOrder.Add("testCleanup"); };

            var result = this.testMethodInfo.Invoke(null);

            var expectedCallOrder = new List<string>
                                        {
                                            "classCtor",
                                            "testContext",
                                            "testInit",
                                            "testMethod",
                                            "testCleanup"
                                        };
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        #endregion

        #region TestMethod timeout scenarios

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldReturnTestFailureOnTimeout()
        {
            var testablePlatformServiceProvider = new TestablePlatformServiceProvider();

            this.RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
            {
                testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

                PlatformServiceProvider.Instance = testablePlatformServiceProvider;

                testablePlatformServiceProvider.MockThreadOperations.Setup(
                 to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
                this.testMethodOptions.Timeout = 1;
                var method = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);

                var result = method.Invoke(null);

                Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
                StringAssert.Contains(result.TestFailureException.Message, "exceeded execution timeout period");
            });
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
        {
            DummyTestClass.TestMethodBody = o => { /* do nothing */ };
            var method = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);
            var result = method.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCancelTokenSourceOnTimeout()
        {
            var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
            {
                testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
                PlatformServiceProvider.Instance = testablePlatformServiceProvider;

                testablePlatformServiceProvider.MockThreadOperations.Setup(
                 to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
                this.testMethodOptions.Timeout = 1;

                var method = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);
                var result = method.Invoke(null);

                Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
                StringAssert.Contains(result.TestFailureException.Message, "exceeded execution timeout period");
                Assert.IsTrue(this.testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not cancelled..");
            });
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldFailOnTokenSourceCancellation()
        {
            var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
            {
                testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
                PlatformServiceProvider.Instance = testablePlatformServiceProvider;

                testablePlatformServiceProvider.MockThreadOperations.Setup(
                 to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback((Action action, int timeoOut, CancellationToken cancelToken) =>
                 {
                     try
                     {
                         Task.WaitAny(new[] { Task.Delay(100000) }, cancelToken);
                     }
                     catch (OperationCanceledException)
                     {
                     }
                 });

                this.testMethodOptions.Timeout = 100000;
                this.testContextImplementation.CancellationTokenSource.CancelAfter(100);
                var method = new TestMethodInfo(this.methodInfo, this.testClassInfo, this.testMethodOptions);
                var result = method.Invoke(null);

                Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
                StringAssert.Contains(result.TestFailureException.Message, "execution has been aborted");
                Assert.IsTrue(this.testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not cancelled..");
            });
        }

        #endregion

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnProvidedArgumentsWhenTooFewParameters()
        {
            var simpleArgumentsMethod = typeof(DummyTestClass).GetMethod("DummySimpleArgumentsMethod");

            var method = new TestMethodInfo(
                simpleArgumentsMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { "RequiredStr1" };
            object[] expectedArguments = new object[] { "RequiredStr1" };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(1, resolvedArguments.Length);
            CollectionAssert.AreEqual(expectedArguments, resolvedArguments);
        }

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnProvidedArgumentsWhenTooManyParameters()
        {
            var simpleArgumentsMethod = typeof(DummyTestClass).GetMethod("DummySimpleArgumentsMethod");

            var method = new TestMethodInfo(
                simpleArgumentsMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { "RequiredStr1", "RequiredStr2", "ExtraStr3" };
            object[] expectedArguments = new object[] { "RequiredStr1", "RequiredStr2", "ExtraStr3" };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(3, resolvedArguments.Length);
            CollectionAssert.AreEqual(expectedArguments, resolvedArguments);
        }

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnAdditionalOptionalParametersWithNoneProvided()
        {
            var optionalArgumentsMethod = typeof(DummyTestClass).GetMethod("DummyOptionalArgumentsMethod");

            var method = new TestMethodInfo(
                optionalArgumentsMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { "RequiredStr1" };
            object[] expectedArguments = new object[] { "RequiredStr1", null, null };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(3, resolvedArguments.Length);
            CollectionAssert.AreEqual(expectedArguments, resolvedArguments);
        }

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnAdditionalOptionalParametersWithSomeProvided()
        {
            var optionalArgumentsMethod = typeof(DummyTestClass).GetMethod("DummyOptionalArgumentsMethod");

            var method = new TestMethodInfo(
                optionalArgumentsMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { "RequiredStr1", "OptionalStr1" };
            object[] expectedArguments = new object[] { "RequiredStr1", "OptionalStr1", null };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(3, resolvedArguments.Length);
            CollectionAssert.AreEqual(expectedArguments, resolvedArguments);
        }

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnEmptyParamsWithNoneProvided()
        {
            var paramsArgumentMethod = typeof(DummyTestClass).GetMethod("DummyParamsArgumentMethod");

            var method = new TestMethodInfo(
                paramsArgumentMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { 1 };
            object[] expectedArguments = new object[] { 1, new string[] { } };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(2, resolvedArguments.Length);
            Assert.AreEqual(expectedArguments[0], resolvedArguments[0]);
            Assert.IsInstanceOfType(resolvedArguments[1], typeof(string[]));
            CollectionAssert.AreEqual((string[])expectedArguments[1], (string[])resolvedArguments[1]);
        }

        [TestMethodV1]
        public void ResolveArgumentsShouldReturnPopulatedParamsWithAllProvided()
        {
            var paramsArgumentMethod = typeof(DummyTestClass).GetMethod("DummyParamsArgumentMethod");

            var method = new TestMethodInfo(
                paramsArgumentMethod,
                this.testClassInfo,
                this.testMethodOptions);

            object[] arguments = new object[] { 1, "str1", "str2", "str3" };
            object[] expectedArguments = new object[] { 1, new string[] { "str1", "str2", "str3" } };
            var resolvedArguments = method.ResolveArguments(arguments);

            Assert.AreEqual(2, resolvedArguments.Length);
            Assert.AreEqual(expectedArguments[0], resolvedArguments[0]);
            Assert.IsInstanceOfType(resolvedArguments[1], typeof(string[]));
            CollectionAssert.AreEqual((string[])expectedArguments[1], (string[])resolvedArguments[1]);
        }

        #region helper methods

        private void RunWithTestablePlatformService(TestablePlatformServiceProvider testablePlatformServiceProvider, Action action)
        {
            try
            {
                testablePlatformServiceProvider.MockThreadOperations.
                    Setup(tho => tho.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).
                    Returns(true).
                    Callback((Action a, int timeout, CancellationToken token) =>
                    {
                        a.Invoke();
                    });
                testablePlatformServiceProvider.MockThreadOperations.
                    Setup(tho => tho.ExecuteWithAbortSafety(It.IsAny<Action>())).
                    Callback((Action a) => { a.Invoke(); });

                action.Invoke();
            }
            finally
            {
                PlatformServiceProvider.Instance = null;
            }
        }

        #endregion

        #region Test data
        public class DummyTestClassBase
        {
            public static Action<DummyTestClassBase> BaseTestClassMethodBody { get; set; }

            public void DummyBaseTestClassMethod()
            {
                BaseTestClassMethodBody(this);
            }
        }

        public class DummyTestClass : DummyTestClassBase
        {
            private static UTFExtension.TestContext tc;

            public DummyTestClass()
            {
                TestConstructorMethodBody();
            }

            public static Action TestConstructorMethodBody { get; set; }

            public static Action<object> TestContextSetterBody { get; set; }

            public static Action<DummyTestClass> TestInitializeMethodBody { get; set; }

            public static Action<DummyTestClass> TestMethodBody { get; set; }

            public static Action<DummyTestClass> TestCleanupMethodBody { get; set; }

            public static Func<Task> DummyAsyncTestMethodBody { get; set; }

            public static UTFExtension.TestContext GetTestContext()
            {
                return tc;
            }

            public UTFExtension.TestContext TestContext
            {
                get
                {
                    throw new NotImplementedException();
                }

                set
                {
                    TestContextSetterBody(value);
                    tc = value;
                }
            }

            public void DummyTestInitializeMethod()
            {
                TestInitializeMethodBody(this);
            }

            public void DummyTestCleanupMethod()
            {
                TestCleanupMethodBody(this);
            }

            public void DummyTestMethod()
            {
                TestMethodBody(this);
            }

            public Task DummyAsyncTestMethod()
            {
                // We use this method to validate async TestInitialize, TestCleanup, TestMethod
                return DummyAsyncTestMethodBody();
            }

            public void DummySimpleArgumentsMethod(string str1, string str2)
            {
                TestMethodBody(this);
            }

            public void DummyOptionalArgumentsMethod(string str1, string str2 = null, string str3 = null)
            {
                TestMethodBody(this);
            }

            public void DummyParamsArgumentMethod(int i, params string[] args)
            {
                TestMethodBody(this);
            }
        }

        public class DummyTestClassWithParameterizedCtor
        {
            public DummyTestClassWithParameterizedCtor(int x)
            {
            }
        }

        public class DummyTestClassWithTestContextWithoutSetter
        {
            public UTFExtension.TestContext TestContext { get; }
        }

        public class DummyTestClassWithDisposable : IDisposable
        {
            public static Action DisposeMethodBody { get; set; }

            public static Action<DummyTestClassWithDisposable> DummyTestCleanupMethodBody { get; set; }

            public void Dispose()
            {
                DisposeMethodBody();
            }

            public void DummyTestMethod()
            {
            }

            public void DummyTestCleanupMethod()
            {
                DummyTestCleanupMethodBody(this);
            }
        }

        #region Dummy implementation

        /// <summary>
        ///  Custom Expected exception attribute which doverrides the Verify method.
        /// </summary>
        public class CustomExpectedExceptionAttribute : UTF.ExpectedExceptionBaseAttribute
        {
            public CustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
                : base(noExceptionMessage)
            {
                this.ExceptionType = expectionType;
            }

            public bool IsVerifyInvoked { get; set; }

            public Type ExceptionType { get; private set; }

            protected override void Verify(Exception exception)
            {
                this.IsVerifyInvoked = true;
                if (exception is UTF.AssertInconclusiveException)
                {
                    throw new UTF.AssertInconclusiveException();
                }
                else if (!exception.Message.Contains(this.NoExceptionMessage))
                {
                    throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
                }
            }
        }

        /// <summary>
        ///  Custom Expected exception attribute which doverrides the Verify method.
        /// </summary>
        public class DerivedCustomExpectedExceptionAttribute : CustomExpectedExceptionAttribute
        {
            public DerivedCustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
                : base(expectionType, noExceptionMessage)
            {
                this.ExceptionType = expectionType;
            }

            public new Type ExceptionType { get; private set; }

            public new bool IsVerifyInvoked { get; set; }

            protected override void Verify(Exception exception)
            {
                this.IsVerifyInvoked = true;
                if (exception is UTF.AssertInconclusiveException)
                {
                    throw new UTF.AssertInconclusiveException();
                }
                else if (!exception.Message.Contains(this.NoExceptionMessage))
                {
                    throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
                }
            }
        }

        #endregion
    }
    #endregion
}