// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    private readonly TestMethodInfo _testMethodInfo;

    private readonly MethodInfo _methodInfo;

    private readonly UTF.TestClassAttribute _classAttribute;

    private readonly UTF.TestMethodAttribute _testMethodAttribute;

    private readonly PropertyInfo _testContextProperty;

    private readonly TestAssemblyInfo _testAssemblyInfo;

    private readonly ConstructorInfo _constructorInfo;

    private readonly TestContextImplementation _testContextImplementation;

    private readonly TestClassInfo _testClassInfo;

    private readonly UTF.ExpectedExceptionAttribute _expectedException;

    private readonly TestMethodOptions _testMethodOptions;

    public TestMethodInfoTests()
    {
        _constructorInfo = typeof(DummyTestClass).GetConstructors().Single();
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod"));
        _classAttribute = new UTF.TestClassAttribute();
        _testMethodAttribute = new UTF.TestMethodAttribute();
        _testContextProperty = typeof(DummyTestClass).GetProperty("TestContext");

        _testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClass).Assembly);
        var testMethod = new TestMethod("dummyTestName", "dummyClassName", "dummyAssemblyName", false);
        _testContextImplementation = new TestContextImplementation(testMethod, new ThreadSafeStringWriter(null, "test"), new Dictionary<string, object>());
        _testClassInfo = new TestClassInfo(
            type: typeof(DummyTestClass),
            constructor: _constructorInfo,
            testContextProperty: _testContextProperty,
            classAttribute: _classAttribute,
            parent: _testAssemblyInfo);
        _expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));
        _testMethodOptions = new TestMethodOptions()
        {
            Timeout = 3600 * 1000,
            Executor = _testMethodAttribute,
            ExpectedException = null,
            TestContext = _testContextImplementation
        };

        _testMethodInfo = new TestMethodInfo(
            _methodInfo,
            parent: _testClassInfo,
            testmethodOptions: _testMethodOptions);

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
        _testMethodInfo.SetArguments(arguments);

        Assert.AreEqual(3, _testMethodInfo.Arguments.Length);
        Assert.AreEqual(10, _testMethodInfo.Arguments[0]);
        Assert.AreEqual(20, _testMethodInfo.Arguments[1]);
        Assert.AreEqual(30, _testMethodInfo.Arguments[2]);
    }

    #region TestMethod invoke scenarios

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
    {
        var methodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => methodCalled = true);
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Assert.IsTrue(methodCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeAsyncShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => throw new UTF.AssertInconclusiveException());
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeAsyncShouldHandleAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => UTF.Assert.Inconclusive());
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => throw new UTF.AssertInconclusiveException();
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldHandleAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => UTF.Assert.Inconclusive();
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldReportTestContextMessages()
    {
        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("TestContext");

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        StringAssert.Contains(result.TestContextMessages, "TestContext");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldClearTestContextMessagesAfterReporting()
    {
        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("TestContext");

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        StringAssert.Contains(result.TestContextMessages, "TestContext");

        DummyTestClass.TestMethodBody = o => _testContextImplementation.WriteLine("SeaShore");

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

        var result = _testMethodInfo.Invoke(null);
        _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        Assert.AreEqual(2, ctorCallCount);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException();

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        var result = _testMethodInfo.Invoke(null);

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
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, _testContextProperty, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

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
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        Assert.IsNotNull(exception);
        StringAssert.StartsWith(
            exception?.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrowsWithoutInnerException()
    {
        var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, _testContextProperty, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        var exception = method.Invoke(null).TestFailureException as TestFailedException;

        Assert.IsNotNull(exception);
        StringAssert.StartsWith(
            exception?.StackTraceInformation.ErrorStackTrace,
            "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetResultFilesIfTestContextHasAttachments()
    {
        Mock<ITestContext> testContext = new();
        testContext.Setup(tc => tc.GetResultFiles()).Returns(new List<string>() { "C:\\temp.txt" });
        var mockInnerContext = new Mock<UTFExtension.TestContext>();
        testContext.SetupGet(tc => tc.Context).Returns(mockInnerContext.Object);
        mockInnerContext.SetupGet(tc => tc.CancellationTokenSource).Returns(new CancellationTokenSource());
        _testMethodOptions.TestContext = testContext.Object;

        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = method.Invoke(null);
        CollectionAssert.Contains(result.ResultFiles.ToList(), "C:\\temp.txt");
    }

    #endregion

    #region TestClass.TestContext property setup
    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
    {
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result = null;
        void runMethod() => result = method.Invoke(null);

        runMethod();
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
    {
        var testContext = typeof(DummyTestClassWithTestContextWithoutSetter).GetProperties().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, testContext, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result = null;
        void runMethod() => result = method.Invoke(null);

        runMethod();
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
    {
        UTFExtension.TestContext testContext = null;
        DummyTestClass.TestContextSetterBody = context => testContext = context as UTFExtension.TestContext;

        _testMethodInfo.Invoke(null);

        Assert.AreSame(_testContextImplementation, testContext);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => throw new NotImplementedException();

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetErrorMessageIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

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
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException("dummyExceptionMessage");

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

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
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.IsTrue(testInitializeCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
    {
        var testInitializeCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => testInitializeCalled = true);
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.IsTrue(testInitializeCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestInitializeOfAllBaseClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestInitializeCalled2");
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => callOrder.Add("baseAsyncTestInitializeCalled1"));
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("classTestInitializeCalled");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod"));

        var result = _testMethodInfo.Invoke(null);

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
        _testClassInfo.TestInitializeMethod = null;

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
    {
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null);

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(errorMessage, exception.Message);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
        Assert.IsInstanceOfType(exception.InnerException.InnerException, typeof(InvalidOperationException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => UTF.Assert.Fail("dummyFailMessage");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Fail failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(errorMessage, exception.Message);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertFailedException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => UTF.Assert.Inconclusive("dummyFailMessage");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Inconclusive failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(errorMessage, exception.Message);
        Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertInconclusiveException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult>b__");
    }

    #endregion

    #region TestCleanup method setup

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        Assert.IsTrue(cleanupMethodCalled);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallAsyncTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => cleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
        Assert.IsTrue(cleanupMethodCalled);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
    {
        _testClassInfo.TestCleanupMethod = null;

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodForBaseClassIsNull()
    {
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(null);

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestCleanupCalled" + callOrder.Count);
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

        var result = _testMethodInfo.Invoke(null);

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
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => callOrder.Add("baseTestCleanupCalled" + callOrder.Count);
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

        var result = _testMethodInfo.Invoke(null);
        result = _testMethodInfo.Invoke(null);

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
    public void TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message"));
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(ArgumentException).ToString(),
            "Some exception message");

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(expectedErrorMessage, exception.Message);
        Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
        Assert.IsInstanceOfType(exception.InnerException.InnerException, typeof(InvalidOperationException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => UTF.Assert.Inconclusive("Test inconclusive");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(UTF.AssertInconclusiveException).ToString(),
            "Assert.Inconclusive failed. Test inconclusive");

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Inconclusive, exception.Outcome);
        Assert.AreEqual(expectedErrorMessage, exception.Message);
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertInconclusiveException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => UTF.Assert.Fail("Test failed");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(UTF.AssertFailedException).ToString(),
            "Assert.Fail failed. Test failed");

        var result = _testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);

        var exception = result.TestFailureException as TestFailedException;
        Assert.IsNotNull(exception);
        Assert.AreEqual(UnitTestOutcome.Failed, exception.Outcome);
        Assert.AreEqual(expectedErrorMessage, exception.Message);
        Assert.IsInstanceOfType(exception.InnerException, typeof(UTF.AssertFailedException));

        StringAssert.StartsWith(
            exception.StackTraceInformation.ErrorStackTrace,
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult>b__");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldAppendErrorMessagesIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new NotImplementedException("dummyErrorMessage");
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException("dummyMethodError");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;
        var errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestMethodThrows,
            typeof(DummyTestClass).FullName,
            _testMethodInfo.TestMethodName,
            "System.NotImplementedException: dummyMethodError");
        var cleanupError = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(NotImplementedException).ToString(),
            "dummyErrorMessage");

        Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
        Assert.IsNotNull(exception);
        Assert.AreEqual(string.Concat(errorMessage, Environment.NewLine, cleanupError), exception?.Message);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldAppendStackTraceInformationIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException("dummyMethodError");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
        Assert.IsNotNull(exception);
        StringAssert.Contains(exception.StackTraceInformation.ErrorStackTrace, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestMethod()");
        StringAssert.Contains(exception.StackTraceInformation.ErrorStackTrace, Resource.UTA_CleanupStackTrace);
        StringAssert.Contains(exception.StackTraceInformation.ErrorStackTrace, "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestCleanupMethod()");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetOutcomeAsInconclusiveIfTestCleanupIsInconclusive()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new UTF.AssertInconclusiveException();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Inconclusive);
        Assert.IsNotNull(exception);
        StringAssert.Contains(exception?.Message, "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException");
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetMoreImportantOutcomeIfTestCleanupIsInconclusiveButTestMethodFails()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => throw new UTF.AssertInconclusiveException();
        DummyTestClass.TestMethodBody = classInstance => Assert.Fail();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Assert.AreEqual(result.Outcome, UTF.UnitTestOutcome.Failed);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
    {
        var disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        method.Invoke(null);

        Assert.IsTrue(disposeCalled);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClassIfTestCleanupThrows()
    {
        var disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        DummyTestClassWithDisposable.DummyTestCleanupMethodBody = classInstance => throw new NotImplementedException();
        var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo)
        {
            TestCleanupMethod = typeof(DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod")
        };
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        method.Invoke(null);

        Assert.IsTrue(disposeCalled);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.IsTrue(testCleanupMethodCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Assert.IsTrue(testCleanupMethodCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
    {
        var testCleanupMethodCalled = false;

        // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
        DummyTestClass.TestConstructorMethodBody = () => throw new NotImplementedException();
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

        var result = _testMethodInfo.Invoke(null);

        Assert.IsFalse(testCleanupMethodCalled);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
    {
        var testCleanupCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupCalled = true;
        DummyTestClass.TestContextSetterBody = o => throw new NotImplementedException();
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        _testMethodInfo.Invoke(null);

        Assert.IsFalse(testCleanupCalled);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => throw new IndexOutOfRangeException();
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

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
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = testMethodInfo.Invoke(null);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
        var message = "Test method did not throw expected exception System.DivideByZeroException.";
        StringAssert.Contains(result.TestFailureException.Message, message);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
    {
        DummyTestClass.TestMethodBody = o => throw new UTF.AssertInconclusiveException();
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = testMethodInfo.Invoke(null);
        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Assert.AreEqual(message, result.TestFailureException.Message);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldSetTestOutcomeBeforeTestCleanup()
    {
        UTF.UnitTestOutcome testOutcome = UTF.UnitTestOutcome.Unknown;
        DummyTestClass.TestMethodBody = o => throw new UTF.AssertInconclusiveException();
        DummyTestClass.TestCleanupMethodBody = c =>
                    {
                        if (DummyTestClass.GetTestContext() != null)
                        {
                            testOutcome = DummyTestClass.GetTestContext().CurrentTestOutcome;
                        }
                    };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = testMethodInfo.Invoke(null);

        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, testOutcome);
    }

    [TestMethodV1]
    public void HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var result = method.Invoke(null);
        Assert.IsTrue(customExpectedException.IsVerifyInvoked);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var result = method.Invoke(null);
        Assert.AreEqual("The exception message doesn't contain the string defined in the exception attribute", result.TestFailureException.Message);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void HandleMethodExceptionShouldSetOutcomeAsInconclusveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new UTF.AssertInconclusiveException();
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Assert.AreEqual(result.TestFailureException.Message, message);
        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void HandleMethodExceptionShouldInvokeVerifyOfDerivedCustomExpectedException()
    {
        DerivedCustomExpectedExceptionAttribute derivedCustomExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = derivedCustomExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var result = method.Invoke(null);
        Assert.IsTrue(derivedCustomExpectedException.IsVerifyInvoked);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception))
        {
            AllowDerivedTypes = true
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var result = method.Invoke(null);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException), "Custom Exception")
        {
            AllowDerivedTypes = true
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new ArgumentNullException();
        var result = method.Invoke(null);
        var message = "Test method threw exception System.ArgumentNullException, but exception System.DivideByZeroException" +
            " or a type derived from it was expected. Exception message: System.ArgumentNullException: Value cannot be null.";
        Assert.AreEqual(result.TestFailureException.Message, message);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertFailedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new UTF.AssertFailedException();
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.";
        Assert.AreEqual(result.TestFailureException.Message, message);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertInconclusiveException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new UTF.AssertInconclusiveException();
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Assert.AreEqual(result.TestFailureException.Message, message);
        Assert.AreEqual(UTF.UnitTestOutcome.Inconclusive, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldThrowIfThrownExceptionIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new DivideByZeroException();
        var result = method.Invoke(null);
        var message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. " +
            "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
        Assert.AreEqual(result.TestFailureException.Message, message);
        Assert.AreEqual(UTF.UnitTestOutcome.Failed, result.Outcome);
    }

    [TestMethodV1]
    public void VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => throw new UTF.AssertInconclusiveException();
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
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        DummyTestClass.TestConstructorMethodBody = () => callOrder.Add("classCtor");
        DummyTestClass.TestContextSetterBody = o => callOrder.Add("testContext");
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("testInit");
        DummyTestClass.TestMethodBody = classInstance => callOrder.Add("testMethod");
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("testCleanup");

        var result = _testMethodInfo.Invoke(null);

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

        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            _testMethodOptions.Timeout = 1;
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
            StringAssert.Contains(result.TestFailureException.Message, "exceeded execution timeout period");
        });
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
    {
        DummyTestClass.TestMethodBody = o => { /* do nothing */ };
        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = method.Invoke(null);
        Assert.AreEqual(UTF.UnitTestOutcome.Passed, result.Outcome);
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldCancelTokenSourceOnTimeout()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            _testMethodOptions.Timeout = 1;

            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
            StringAssert.Contains(result.TestFailureException.Message, "exceeded execution timeout period");
            Assert.IsTrue(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    [TestMethodV1]
    public void TestMethodInfoInvokeShouldFailOnTokenSourceCancellation()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
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

            _testMethodOptions.Timeout = 100000;
            _testContextImplementation.CancellationTokenSource.CancelAfter(100);
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
            var result = method.Invoke(null);

            Assert.AreEqual(UTF.UnitTestOutcome.Timeout, result.Outcome);
            StringAssert.Contains(result.TestFailureException.Message, "execution has been aborted");
            Assert.IsTrue(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    #endregion

    [TestMethodV1]
    public void ResolveArgumentsShouldReturnProvidedArgumentsWhenTooFewParameters()
    {
        var simpleArgumentsMethod = typeof(DummyTestClass).GetMethod("DummySimpleArgumentsMethod");

        var method = new TestMethodInfo(
            simpleArgumentsMethod,
            _testClassInfo,
            _testMethodOptions);

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
            _testClassInfo,
            _testMethodOptions);

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
            _testClassInfo,
            _testMethodOptions);

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
            _testClassInfo,
            _testMethodOptions);

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
            _testClassInfo,
            _testMethodOptions);

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
            _testClassInfo,
            _testMethodOptions);

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
                Callback((Action a, int timeout, CancellationToken token) => a.Invoke());
            testablePlatformServiceProvider.MockThreadOperations.
                Setup(tho => tho.ExecuteWithAbortSafety(It.IsAny<Action>())).
                Callback((Action a) => a.Invoke());

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

        public void DummyBaseTestClassMethod() => BaseTestClassMethodBody(this);
    }

    public class DummyTestClass : DummyTestClassBase
    {
        private static UTFExtension.TestContext s_tc;

        public DummyTestClass() => TestConstructorMethodBody();

        public static Action TestConstructorMethodBody { get; set; }

        public static Action<object> TestContextSetterBody { get; set; }

        public static Action<DummyTestClass> TestInitializeMethodBody { get; set; }

        public static Action<DummyTestClass> TestMethodBody { get; set; }

        public static Action<DummyTestClass> TestCleanupMethodBody { get; set; }

        public static Func<Task> DummyAsyncTestMethodBody { get; set; }

        public static UTFExtension.TestContext GetTestContext() => s_tc;

        public UTFExtension.TestContext TestContext
        {
            get => throw new NotImplementedException();

            set
            {
                TestContextSetterBody(value);
                s_tc = value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestInitializeMethod() => TestInitializeMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestCleanupMethod() => TestCleanupMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestMethod() => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public Task DummyAsyncTestMethod() =>
            // We use this method to validate async TestInitialize, TestCleanup, TestMethod
            DummyAsyncTestMethodBody();

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummySimpleArgumentsMethod(string str1, string str2) => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyOptionalArgumentsMethod(string str1, string str2 = null, string str3 = null) => TestMethodBody(this);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyParamsArgumentMethod(int i, params string[] args) => TestMethodBody(this);
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

        public void Dispose() => DisposeMethodBody();

        public void DummyTestMethod()
        {
        }

        public void DummyTestCleanupMethod() => DummyTestCleanupMethodBody(this);
    }

    #region Dummy implementation

    /// <summary>
    ///  Custom Expected exception attribute which overrides the Verify method.
    /// </summary>
    public class CustomExpectedExceptionAttribute : UTF.ExpectedExceptionBaseAttribute
    {
        public CustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
            : base(noExceptionMessage) => ExceptionType = expectionType;

        public bool IsVerifyInvoked { get; set; }

        public Type ExceptionType { get; private set; }

        protected override void Verify(Exception exception)
        {
            IsVerifyInvoked = true;
            if (exception is UTF.AssertInconclusiveException)
            {
                throw new UTF.AssertInconclusiveException();
            }
            else if (!exception.Message.Contains(NoExceptionMessage))
            {
                throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }
        }
    }

    /// <summary>
    ///  Custom Expected exception attribute which overrides the Verify method.
    /// </summary>
    public class DerivedCustomExpectedExceptionAttribute : CustomExpectedExceptionAttribute
    {
        public DerivedCustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
            : base(expectionType, noExceptionMessage) => ExceptionType = expectionType;

        public new Type ExceptionType { get; private set; }

        public new bool IsVerifyInvoked { get; set; }

        protected override void Verify(Exception exception)
        {
            IsVerifyInvoked = true;
            if (exception is UTF.AssertInconclusiveException)
            {
                throw new UTF.AssertInconclusiveException();
            }
            else if (!exception.Message.Contains(NoExceptionMessage))
            {
                throw new InvalidOperationException("The exception message doesn't contain the string defined in the exception attribute");
            }
        }
    }

    #endregion
}
#endregion
