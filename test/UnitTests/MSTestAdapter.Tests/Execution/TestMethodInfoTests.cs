// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestMethodInfoTests.cs" company="Microsoft">
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
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

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

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
            this.testMethodInfo = new TestMethodInfo(
                this.methodInfo, 
                timeout: 3600 * 1000, 
                executor: this.testMethodAttribute, 
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
        {
            var methodCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { methodCalled = true; });
            var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");
            var method = new TestMethodInfo(
                asyncMethodInfo, 
                3600 * 1000, 
                this.testMethodAttribute, 
                this.testClassInfo, 
                this.testContextImplementation);

            var result = method.Invoke(null);

            Assert.IsTrue(methodCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        #endregion

        #region TestClass constructor setup

        [TestMethod]
        public void TestMethodInfoInvokeShouldCreateNewInstanceOfTestClassOnEveryCall()
        {
            var ctorCallCount = 0;
            DummyTestClass.TestConstructorMethodBody = () => ctorCallCount++;

            var result = this.testMethodInfo.Invoke(null);
            this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
            Assert.AreEqual(2, ctorCallCount);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrowsWithoutInnerException()
        {
            var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, this.testContextProperty, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            var result = method.Invoke(null);
            var errorMessage = string.Format(
                Resource.UTA_InstanceCreationError, 
                typeof(DummyTestClassWithParameterizedCtor).FullName, 
                "System.Reflection.TargetParameterCountException: Parameter count mismatch.");

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
            Assert.AreEqual(errorMessage, result.TestFailureException.Message);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows()
        {
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(
                exception?.StackTraceInformation.ErrorStackTrace, 
                "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__");
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrowsWithoutInnerException()
        {
            var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, this.testContextProperty, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            var exception = method.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            StringAssert.StartsWith(exception?.StackTraceInformation.ErrorStackTrace, 
                "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)");
        }

        #endregion

        #region TestClass.TestContext property setup
        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
        {
            var testClass = new TestClassInfo(typeof(DummyTestClass), this.constructorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            UTF.TestResult result = null;
            Action runMethod = () => result = method.Invoke(null);

            runMethod();
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
        {
            var testContext = typeof(DummyTestClassWithTestContextWithoutSetter).GetProperties().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClass), this.constructorInfo, testContext, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(this.methodInfo, 3600 * 1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            UTF.TestResult result = null;
            Action runMethod = () => result = method.Invoke(null);

            runMethod();
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
        {
            UTF.TestContext testContext = null;
            DummyTestClass.TestContextSetterBody = context => testContext = context as UTF.TestContext;

            this.testMethodInfo.Invoke(null);

            Assert.AreSame(this.testContextImplementation, testContext);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
        {
            DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException(); };

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallTestInitialize()
        {
            var testInitializeCalled = false;
            DummyTestClass.TestInitializeMethodBody = classInstance => testInitializeCalled = true;
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testInitializeCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
        {
            var testInitializeCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { testInitializeCalled = true; });
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testInitializeCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
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
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeIsNull()
        {
            this.testClassInfo.TestInitializeMethod = null;

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
        {
            this.testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null);

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldMarkOutcomeAsFailIfTestInitializeThrows()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldSetErrorMessageIfTestInitializeThrowsUnitTestAssertException()
        {
            DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
            this.testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
            const string ErrorMessage = "Assert.Fail failed. dummyFailMessage";

            var exception = this.testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

            Assert.IsNotNull(exception);
            Assert.AreEqual(ErrorMessage, exception?.Message);
        }

        [TestMethod]
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallTestCleanup()
        {
            var cleanupMethodCalled = false;
            DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
            Assert.IsTrue(cleanupMethodCalled);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallAsyncTestCleanup()
        {
            var cleanupMethodCalled = false;
            DummyTestClass.TestCleanupMethodBody = classInstance => { cleanupMethodCalled = true; };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
            Assert.IsTrue(cleanupMethodCalled);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
        {
            this.testClassInfo.TestCleanupMethod = null;

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodForBaseClassIsNull()
        {
            this.testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(null);

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
        }

        [TestMethod]
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
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
            CollectionAssert.AreEqual(expectedCallOrder, callOrder);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldMarkOutcomeAsFailedIfTestCleanupThrows()
        {
            DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
        {
            var disposeCalled = false;
            DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
            var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, this.classAttribute, this.testAssemblyInfo);
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), 3600*1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            method.Invoke(null);

            Assert.IsTrue(disposeCalled);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClassIfTestCleanupThrows()
        {
            var disposeCalled = false;
            DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
            DummyTestClassWithDisposable.DummyTestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
            var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
            var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, this.classAttribute, this.testAssemblyInfo);
            testClass.TestCleanupMethod = typeof (DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod");
            var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), 3600*1000, this.testMethodAttribute, testClass, this.testContextImplementation);

            method.Invoke(null);

            Assert.IsTrue(disposeCalled);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
        {
            var testCleanupMethodCalled = false;
            DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testCleanupMethodCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
        {
            var testCleanupMethodCalled = false;
            DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
            this.testClassInfo.TestInitializeMethod = typeof (DummyTestClass).GetMethod("DummyTestInitializeMethod");
            this.testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsTrue(testCleanupMethodCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
        {
            var testCleanupMethodCalled = false;

            // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
            DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };
            DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

            var result = this.testMethodInfo.Invoke(null);

            Assert.IsFalse(testCleanupMethodCalled);
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
        {
            var testCleanupCalled = false;
            DummyTestClass.TestCleanupMethodBody = classInstance => { testCleanupCalled = true; };
            DummyTestClass.TestContextSetterBody = o => { throw new NotImplementedException(); };
            this.testClassInfo.TestCleanupMethod = typeof (DummyTestClass).GetMethod("DummyTestCleanupMethod");

            this.testMethodInfo.Invoke(null);

            Assert.IsFalse(testCleanupCalled);
        }


        #endregion

        #region TestMethod invoke setup order

        [TestMethod]
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
            Assert.AreEqual(TestTools.UnitTesting.UnitTestOutcome.Passed, result.Outcome);
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

            public UTF.TestContext TestContext
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
            public UTF.TestContext TestContext { get; }
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

        #endregion
    }

}