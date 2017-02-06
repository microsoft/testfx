// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

    using Moq;
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
        
        public TestMethodInfoTests()
        {
            this.constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
            this.methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
            this.classAttribute = new UTF.TestClassAttribute();
            this.testMethodAttribute = new UTF.TestMethodAttribute();
            this.testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

            this.testAssemblyInfo = new TestAssemblyInfo();
            var testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
            this.testContextImplementation = new TestContextImplementation(testMethod, null, new Dictionary<string, object>());
            this.testClassInfo = new TestClassInfo(
                type: typeof(DummyTestClass), 
                constructor: this.constructorInfo, 
                testContextProperty: this.testContextProperty, 
                classAttribute: this.classAttribute, 
                parent: this.testAssemblyInfo);
            this.expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));
            this.testMethodInfo = new TestMethodInfo(
                this.methodInfo, 
                timeout: 3600 * 1000, 
                executor: this.testMethodAttribute, 
                expectedException: null,
                parent: this.testClassInfo, 
                testContext: this.testContextImplementation);

            // Reset test hooks
            DummyTestClass.TestConstructorMethodBody = () => { };
            DummyTestClass.TestContextSetterBody = value => { };
            DummyTestClass.TestInitializeMethodBody = value => { };
            DummyTestClass.TestMethodBody = instance => { };
            DummyTestClass.TestCleanupMethodBody = value => { };
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
                3600 * 1000, 
                this.testMethodAttribute, 
                null,
                this.testClassInfo, 
                this.testContextImplementation);

            var result = method.Invoke(null);

            Assert.IsTrue(methodCalled);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
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
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

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
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

            var exception = method.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(exception?.StackTraceInformation.ErrorStackTrace, 
                "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultFilesIfTestContextHasAttachments()
        {
            Mock<ITestContext> testContext = new Mock<ITestContext>();
            testContext.Setup(tc => tc.GetResultFiles()).Returns(new List<string>() {"C:\\temp.txt"});

            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, testClassInfo, testContext.Object);

            var result = method.Invoke(null);
            CollectionAssert.Contains(result.ResultFiles.ToList(), "C:\\temp.txt");
        }

        #endregion

        #region TestClass.TestContext property setup
        [TestMethodV1]
        public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
        {
            var testClass = new TestClassInfo(typeof(DummyTestClass), this.constructorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

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
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

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
            StringAssert.StartsWith(exception?.StackTraceInformation.ErrorStackTrace, 
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
                "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestCleanupThrows>b__");
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
        {
            var disposeCalled = false;
            DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
            var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), 3600*1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

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
            testClass.TestCleanupMethod = typeof (DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod");
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), 3600*1000, this.testMethodAttribute, null, testClass, this.testContextImplementation);

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
            this.testClassInfo.TestInitializeMethod = typeof (DummyTestClass).GetMethod("DummyTestInitializeMethod");
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
            this.testClassInfo.TestCleanupMethod = typeof (DummyTestClass).GetMethod("DummyTestCleanupMethod");

            this.testMethodInfo.Invoke(null);

            Assert.IsFalse(testCleanupCalled);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
        {
            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var testMethodInfo = new TestMethodInfo(this.methodInfo,3600 * 1000,this.testMethodAttribute,
            this.expectedException,this.testClassInfo,this.testContextImplementation);
            var result = testMethodInfo.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
        {
            DummyTestClass.TestMethodBody = o => { throw new IndexOutOfRangeException(); };
            var testMethodInfo = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute,
            this.expectedException, this.testClassInfo, this.testContextImplementation);
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
            var testMethodInfo = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute,
            this.expectedException, this.testClassInfo, this.testContextImplementation);
            var result = testMethodInfo.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
            var message = "Test method did not throw expected exception System.DivideByZeroException.";
            StringAssert.Contains(result.TestFailureException.Message, message);
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
        {
            DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
            var testMethodInfo = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute,
            this.expectedException, this.testClassInfo, this.testContextImplementation);
            var result = testMethodInfo.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
            var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
            Assert.AreEqual(message, result.TestFailureException.Message);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException),"Attempted to divide by zero");
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                customExpectedException,
                this.testClassInfo,
                this.testContextImplementation);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(true,customExpectedException.isVerifyInvoked);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                customExpectedException,
                this.testClassInfo,
                this.testContextImplementation);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(result.TestFailureException.Message, "The exception message doesn't contain the string defined in the exception attribute");
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void HandleMethodExceptionShouldSetOutcomeAsInconclusveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
        {
            CustomExpectedExceptionAttribute customExpectedException = new CustomExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                customExpectedException,
                this.testClassInfo,
                this.testContextImplementation);

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
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                derivedCustomExpectedException,
                this.testClassInfo,
                this.testContextImplementation);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(true, derivedCustomExpectedException.isVerifyInvoked);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(Exception));
            expectedException.AllowDerivedTypes = true;
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException), "Custom Exception");
            expectedException.AllowDerivedTypes = true;
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

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
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

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
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

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
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

            DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
            var result = method.Invoke(null);
            var message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. "+
                "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
            Assert.AreEqual(result.TestFailureException.Message, message);
            Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethodV1]
        public void VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
        {
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(Exception));
            var method = new TestMethodInfo(
                methodInfo,
                0,
                this.testMethodAttribute,
                expectedException,
                this.testClassInfo,
                this.testContextImplementation);

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
            this.testClassInfo.TestInitializeMethod = typeof (DummyTestClass).GetMethod("DummyTestInitializeMethod");
            this.testClassInfo.TestCleanupMethod = typeof (DummyTestClass).GetMethod("DummyTestCleanupMethod");

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
            try
            {
                var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
                testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

                PlatformServiceProvider.Instance = testablePlatformServiceProvider;

                testablePlatformServiceProvider.MockThreadOperations.Setup(
                 to => to.Execute(It.IsAny<Action>(), It.IsAny<int>())).Returns(false);

                var method = new TestMethodInfo(this.methodInfo, 1, this.testMethodAttribute, null, this.testClassInfo, this.testContextImplementation);

                var result = method.Invoke(null);

                Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
                StringAssert.Contains(result.TestFailureException.Message, "exceeded execution timeout period");
            }
            finally
            {
                PlatformServiceProvider.Instance = null;
            }
        }

        [TestMethodV1]
        public void TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
        {
            DummyTestClass.TestMethodBody = o =>
                {
                    /* do nothing */
                };
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, null, this.testClassInfo, this.testContextImplementation);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
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

            public UTFExtension.TestContext TestContext
            {
                get
                {
                    throw new NotImplementedException();
                }

                set
                {
                    TestContextSetterBody(value);
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

            protected override void Verify(Exception exception)
            {
                isVerifyInvoked = true;
                if (exception is UTF.AssertInconclusiveException)
                    throw new UTF.AssertInconclusiveException();
                else if (!exception.Message.Contains(this.NoExceptionMessage))
                    throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }

            public Type ExceptionType { get; private set; }

            public bool isVerifyInvoked;
        }

        /// <summary>
        ///  Custom Expected exception attribute which doverrides the Verify method.
        /// </summary>
        public class DerivedCustomExpectedExceptionAttribute : CustomExpectedExceptionAttribute
        {
            public DerivedCustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
                : base(expectionType,noExceptionMessage)
            {
                this.ExceptionType = expectionType;
            }

            protected override void Verify(Exception exception)
            {
                this.isVerifyInvoked = true;
                if (exception is UTF.AssertInconclusiveException)
                    throw new UTF.AssertInconclusiveException();
                else if (!exception.Message.Contains(this.NoExceptionMessage))
                    throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }

            public new Type ExceptionType { get; private set; }

            public new bool isVerifyInvoked;
        }

        #endregion
    }
    #endregion
}