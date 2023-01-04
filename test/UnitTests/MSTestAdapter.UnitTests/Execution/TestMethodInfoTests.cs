// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

/// <summary>
/// The test method info tests.
/// </summary>
public class TestMethodInfoTests : TestContainer
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
            TestContext = _testContextImplementation,
        };

        _testMethodInfo = new TestMethodInfo(
            _methodInfo,
            parent: _testClassInfo,
            testMethodOptions: _testMethodOptions);

        // Reset test hooks
        DummyTestClass.TestConstructorMethodBody = () => { };
        DummyTestClass.TestContextSetterBody = value => { };
        DummyTestClass.TestInitializeMethodBody = value => { };
        DummyTestClass.TestMethodBody = instance => { };
        DummyTestClass.TestCleanupMethodBody = value => { };
    }

    public void SetArgumentsShouldSetArgumentsNeededForCurrentTestRun()
    {
        object[] arguments = new object[] { 10, 20, 30 };
        _testMethodInfo.SetArguments(arguments);

        Verify(_testMethodInfo.Arguments.Length == 3);
        Verify((int)_testMethodInfo.Arguments[0] == 10);
        Verify((int)_testMethodInfo.Arguments[1] == 20);
        Verify((int)_testMethodInfo.Arguments[2] == 30);
    }

    #region TestMethod invoke scenarios

    public async Task TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
    {
        var methodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { methodCalled = true; });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(methodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeAsyncShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { throw new UTF.AssertInconclusiveException(); });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task TestMethodInfoInvokeAsyncShouldHandleAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { UTF.Assert.Inconclusive(); });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task TestMethodInfoInvokeShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => { throw new UTF.AssertInconclusiveException(); };
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task TestMethodInfoInvokeShouldHandleAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => { UTF.Assert.Inconclusive(); };
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task TestMethodInfoInvokeShouldReportTestContextMessages()
    {
        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("TestContext"); };

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.TestContextMessages.Contains("TestContext"));
    }

    public async Task TestMethodInfoInvokeShouldClearTestContextMessagesAfterReporting()
    {
        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("TestContext"); };

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = await method.Invoke(null);

        Verify(result.TestContextMessages.Contains("TestContext"));

        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("SeaShore"); };

        result = await method.Invoke(null);

        Verify(result.TestContextMessages.Contains("SeaShore"));
    }

    #endregion

    #region TestClass constructor setup

    public void TestMethodInfoInvokeShouldCreateNewInstanceOfTestClassOnEveryCall()
    {
        var ctorCallCount = 0;
        DummyTestClass.TestConstructorMethodBody = () => ctorCallCount++;

        var result = await _testMethodInfo.Invoke(null);
        await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(ctorCallCount == 2);
    }

    public async Task TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

        var result = await _testMethodInfo.Invoke(null);

        var errorMessage = string.Format(
            Resource.UTA_InstanceCreationError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(errorMessage == result.TestFailureException.Message);
    }

    public async Task TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrowsWithoutInnerException()
    {
        var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, _testContextProperty, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        var result = await method.Invoke(null);
        var errorMessage = string.Format(
            Resource.UTA_InstanceCreationError,
            typeof(DummyTestClassWithParameterizedCtor).FullName,
            "System.Reflection.TargetParameterCountException: Parameter count mismatch.");

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(errorMessage == result.TestFailureException.Message);
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(
            (bool)exception?.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__"));
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrowsWithoutInnerException()
    {
        var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, _testContextProperty, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        var exception = method.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(
            (bool)exception?.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)"));
    }

    public async Task TestMethodInfoInvokeShouldSetResultFilesIfTestContextHasAttachments()
    {
        Mock<ITestContext> testContext = new();
        testContext.Setup(tc => tc.GetResultFiles()).Returns(new List<string>() { "C:\\temp.txt" });
        var mockInnerContext = new Mock<UTFExtension.TestContext>();
        testContext.SetupGet(tc => tc.Context).Returns(mockInnerContext.Object);
        mockInnerContext.SetupGet(tc => tc.CancellationTokenSource).Returns(new CancellationTokenSource());
        _testMethodOptions.TestContext = testContext.Object;

        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = await method.Invoke(null);
        Verify(result.ResultFiles.ToList().Contains("C:\\temp.txt"));
    }

    #endregion

    #region TestClass.TestContext property setup

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
    {
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result = null;
        async Task RunMethod() => result = await method.Invoke(null);

        await RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
    {
        var testContext = typeof(DummyTestClassWithTestContextWithoutSetter).GetProperties().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, testContext, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result = null;
        async Task RunMethod() => result = await method.Invoke(null);

        await RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
    {
        UTFExtension.TestContext testContext = null;
        DummyTestClass.TestContextSetterBody = context => testContext = context as UTFExtension.TestContext;

        await _testMethodInfo.Invoke(null);

        Verify(_testContextImplementation.Equals(testContext));
    }

    public async Task TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException(); };

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldSetErrorMessageIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException("dummyExceptionMessage"); };

        var exception = (await _testMethodInfo.Invoke(null)).TestFailureException as TestFailedException;

        var errorMessage = string.Format(
            Resource.UTA_TestContextSetError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(exception is not null);
        Verify(errorMessage == exception?.Message);
    }

    public async Task TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

        var exception = (await _testMethodInfo.Invoke(null)).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(
            (bool)exception?.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows>b__"));
    }

    #endregion

    #region TestInitialize method setup

    public async Task TestMethodInfoInvokeShouldCallTestInitialize()
    {
        var testInitializeCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => testInitializeCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
    {
        var testInitializeCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { testInitializeCalled = true; });
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldCallTestInitializeOfAllBaseClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestInitializeCalled2"); };
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { callOrder.Add("baseAsyncTestInitializeCalled1"); });
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("classTestInitializeCalled");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod"));

        var result = await _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "baseAsyncTestInitializeCalled1",
                                        "baseTestInitializeCalled2",
                                        "classTestInitializeCalled",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestInitializeIsNull()
    {
        _testClassInfo.TestInitializeMethod = null;

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
    {
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null);

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = await testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.InnerException.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult>b__"));
    }

    public async Task TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Fail failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = await testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult>b__"));
    }

    public async Task TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Inconclusive("dummyFailMessage"); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Inconclusive failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = await testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult>b__"));
    }

    #endregion

    #region TestCleanup method setup

    public async Task TestMethodInfoInvokeShouldCallTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public async Task TestMethodInfoInvokeShouldCallAsyncTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => { cleanupMethodCalled = true; };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
    {
        _testClassInfo.TestCleanupMethod = null;

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodForBaseClassIsNull()
    {
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(null);

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

        var result = await _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled1",
                                        "baseTestCleanupCalled2",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public async Task TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClassesAlways()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

        var result = await _testMethodInfo.Invoke(null);
        result = await _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled1",
                                        "baseTestCleanupCalled2",
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled4",
                                        "baseTestCleanupCalled5",
                                    };

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public async Task TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(ArgumentException).ToString(),
            "Some exception message");

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult>b__"));
    }

    public async Task TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { UTF.Assert.Inconclusive("Test inconclusive"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(UTF.AssertInconclusiveException).ToString(),
            "Assert.Inconclusive failed. Test inconclusive");

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult>b__"));
    }

    public async Task TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { UTF.Assert.Fail("Test failed"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            typeof(UTF.AssertFailedException).ToString(),
            "Assert.Fail failed. Test failed");

        var result = await _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult>b__"));
    }

    public async Task TestMethodInfoInvokeShouldAppendErrorMessagesIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);
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

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
        Verify(string.Concat(errorMessage, Environment.NewLine, cleanupError) == exception?.Message);
    }

    public async Task TestMethodInfoInvokeShouldAppendStackTraceInformationIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestMethod()"));
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains(Resource.UTA_CleanupStackTrace));
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestCleanupMethod()"));
    }

    public async Task TestMethodInfoInvokeShouldSetOutcomeAsInconclusiveIfTestCleanupIsInconclusive()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(exception is not null);
        Verify((bool)exception?.Message.Contains("Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException"));
    }

    public async Task TestMethodInfoInvokeShouldSetMoreImportantOutcomeIfTestCleanupIsInconclusiveButTestMethodFails()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
        DummyTestClass.TestMethodBody = classInstance => { Fail(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
    {
        var disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        await method.Invoke(null);

        Verify(disposeCalled);
    }

#if NET6_0_OR_GREATER
    public async Task TestMethodInfoInvoke_WhenTestClassIsAsyncDisposable_ShouldDisposeAsync()
    {
        // Arrange
        var asyncDisposeCalled = false;
        DummyTestClassWithAsyncDisposable.DisposeAsyncMethodBody = () => asyncDisposeCalled = true;
        var ctorInfo = typeof(DummyTestClassWithAsyncDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithAsyncDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithAsyncDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        // Act
        await method.Invoke(null);

        // Assert
        Verify(asyncDisposeCalled);
    }

    public async Task TestMethodInfoInvoke_WhenTestClassIsDisposableAndAsyncDisposable_ShouldCallAsyncDiposeThenDipose()
    {
        // Arrange
        var order = 0;
        var disposeCalledOrder = 0;
        var disposeAsyncCalledOrder = 0;

        DummyTestClassWithAsyncDisposableAndDisposable.DisposeMethodBody = () => disposeCalledOrder = ++order;
        DummyTestClassWithAsyncDisposableAndDisposable.DisposeAsyncMethodBody = () => disposeAsyncCalledOrder = ++order;

        var ctorInfo = typeof(DummyTestClassWithAsyncDisposableAndDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithAsyncDisposableAndDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithAsyncDisposableAndDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        // Act
        await method.Invoke(null);

        // Assert
        Verify(disposeCalledOrder == 2);
        Verify(disposeAsyncCalledOrder == 1);
    }
#endif

    public async Task TestMethodInfoInvokeShouldCallDisposeForDisposableTestClassIfTestCleanupThrows()
    {
        var disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        DummyTestClassWithDisposable.DummyTestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
        var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo)
        {
            TestCleanupMethod = typeof(DummyTestClassWithDisposable).GetMethod("DummyTestCleanupMethod"),
        };
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        await method.Invoke(null);

        Verify(disposeCalled);
    }

    public async Task TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = await _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
    {
        var testCleanupMethodCalled = false;

        // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

        var result = await _testMethodInfo.Invoke(null);

        Verify(!testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
    {
        var testCleanupCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => { testCleanupCalled = true; };
        DummyTestClass.TestContextSetterBody = o => { throw new NotImplementedException(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        await _testMethodInfo.Invoke(null);

        Verify(!testCleanupCalled);
    }

    public async Task TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = await testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => { throw new IndexOutOfRangeException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = await testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        var message = "Test method threw exception System.IndexOutOfRangeException, but exception System.DivideByZeroException was expected. " +
            "Exception message: System.IndexOutOfRangeException: Index was outside the bounds of the array.";
        Verify(message == result.TestFailureException.Message);
    }

    public async Task TestMethodInfoInvokeShouldSetResultAsFailedWhenExceptionIsExpectedButIsNotThrown()
    {
        DummyTestClass.TestMethodBody = o => { return; };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = await testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        var message = "Test method did not throw expected exception System.DivideByZeroException.";
        Verify(result.TestFailureException.Message.Contains(message));
    }

    public async Task TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
    {
        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = await testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(message == result.TestFailureException.Message);
    }

    public async Task TestMethodInfoInvokeShouldSetTestOutcomeBeforeTestCleanup()
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
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = await testMethodInfo.Invoke(null);

        Verify(testOutcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = await method.Invoke(null);
        Verify(customExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = await method.Invoke(null);
        Verify(result.TestFailureException.Message == "The exception message doesn't contain the string defined in the exception attribute");
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task HandleMethodExceptionShouldSetOutcomeAsInconclusiveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        var result = await method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task HandleMethodExceptionShouldInvokeVerifyOfDerivedCustomExpectedException()
    {
        DerivedCustomExpectedExceptionAttribute derivedCustomExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = derivedCustomExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = await method.Invoke(null);
        Verify(derivedCustomExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception))
        {
            AllowDerivedTypes = true,
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = await method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException), "Custom Exception")
        {
            AllowDerivedTypes = true,
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new ArgumentNullException(); };
        var result = await method.Invoke(null);
        var message = "Test method threw exception System.ArgumentNullException, but exception System.DivideByZeroException" +
            " or a type derived from it was expected. Exception message: System.ArgumentNullException: Value cannot be null.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task VerifyShouldRethrowExceptionIfThrownExceptionIsAssertFailedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true,
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertFailedException(); };
        var result = await method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task VerifyShouldRethrowExceptionIfThrownExceptionIsAssertInconclusiveException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(DivideByZeroException))
        {
            AllowDerivedTypes = true,
        };
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        var result = await method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public async Task VerifyShouldThrowIfThrownExceptionIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = await method.Invoke(null);
        var message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. " +
            "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public async Task VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        var result = await method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    #endregion

    #region TestMethod invoke setup order

    public async Task TestMethodInfoInvokeShouldInitializeClassInstanceTestInitializeAndTestCleanupInOrder()
    {
        var callOrder = new List<string>();
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        DummyTestClass.TestConstructorMethodBody = () => { callOrder.Add("classCtor"); };
        DummyTestClass.TestContextSetterBody = o => { callOrder.Add("testContext"); };
        DummyTestClass.TestInitializeMethodBody = classInstance => { callOrder.Add("testInit"); };
        DummyTestClass.TestMethodBody = classInstance => { callOrder.Add("testMethod"); };
        DummyTestClass.TestCleanupMethodBody = classInstance => { callOrder.Add("testCleanup"); };

        var result = await _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classCtor",
                                        "testContext",
                                        "testInit",
                                        "testMethod",
                                        "testCleanup",
                                    };
        Verify(expectedCallOrder.SequenceEqual(callOrder));
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    #endregion

    #region TestMethod timeout scenarios

    public void TestMethodInfoInvokeShouldReturnTestFailureOnTimeout()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        RunWithTestablePlatformService(testablePlatformServiceProvider, async () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            _testMethodOptions.Timeout = 1;
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

            var result = await method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException.Message.Contains("exceeded execution timeout period"));
        });
    }

    public async Task TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
    {
        DummyTestClass.TestMethodBody = o => { /* do nothing */ };
        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = await method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCancelTokenSourceOnTimeout()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, async () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;
            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            _testMethodOptions.Timeout = 1;

            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
            var result = await method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException.Message.Contains("exceeded execution timeout period"));
            Verify(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    public void TestMethodInfoInvokeShouldFailOnTokenSourceCancellation()
    {
        var testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        RunWithTestablePlatformService(testablePlatformServiceProvider, async () =>
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
            var result = await method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException.Message.Contains("execution has been aborted"));
            Verify(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

    #endregion

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

        Verify(resolvedArguments.Length == 1);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

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

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

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

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

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

        Verify(resolvedArguments.Length == 3);
        Verify(expectedArguments.SequenceEqual(resolvedArguments));
    }

    public void ResolveArgumentsShouldReturnEmptyParamsWithNoneProvided()
    {
        var paramsArgumentMethod = typeof(DummyTestClass).GetMethod("DummyParamsArgumentMethod");

        var method = new TestMethodInfo(
            paramsArgumentMethod,
            _testClassInfo,
            _testMethodOptions);

        object[] arguments = new object[] { 1 };
        object[] expectedArguments = new object[] { 1, Array.Empty<string>() };
        var resolvedArguments = method.ResolveArguments(arguments);

        Verify(resolvedArguments.Length == 2);
        Verify(expectedArguments[0].Equals(resolvedArguments[0]));
        Verify(resolvedArguments[1].GetType() == typeof(string[]));
        Verify(((string[])expectedArguments[1]).SequenceEqual((string[])resolvedArguments[1]));
    }

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

        Verify(resolvedArguments.Length == 2);
        Verify(expectedArguments[0].Equals(resolvedArguments[0]));
        Verify(resolvedArguments[1].GetType() == typeof(string[]));
        Verify(((string[])expectedArguments[1]).SequenceEqual((string[])resolvedArguments[1]));
    }

    #region helper methods

    private void RunWithTestablePlatformService(TestablePlatformServiceProvider testablePlatformServiceProvider, Action action)
    {
        try
        {
            testablePlatformServiceProvider.MockThreadOperations.
                Setup(tho => tho.Execute(It.IsAny<Func<Task>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(true)).
                Callback((Action a, int timeout, CancellationToken token) =>
                {
                    a.Invoke();
                });

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
        private static UTFExtension.TestContext s_tc;

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
            return s_tc;
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
                s_tc = value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestInitializeMethod()
        {
            TestInitializeMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestCleanupMethod()
        {
            TestCleanupMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestMethod()
        {
            TestMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public Task DummyAsyncTestMethod()
        {
            // We use this method to validate async TestInitialize, TestCleanup, TestMethod
            return DummyAsyncTestMethodBody();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummySimpleArgumentsMethod(string str1, string str2)
        {
            TestMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyOptionalArgumentsMethod(string str1, string str2 = null, string str3 = null)
        {
            TestMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
    ///  Custom Expected exception attribute which overrides the Verify method.
    /// </summary>
    public class CustomExpectedExceptionAttribute : UTF.ExpectedExceptionBaseAttribute
    {
        public CustomExpectedExceptionAttribute(Type expectionType, string noExceptionMessage)
            : base(noExceptionMessage)
        {
            ExceptionType = expectionType;
        }

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
            : base(expectionType, noExceptionMessage)
        {
            ExceptionType = expectionType;
        }

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

#if NET6_0_OR_GREATER
    public class DummyTestClassWithAsyncDisposable : IAsyncDisposable
    {
        public static Action DisposeAsyncMethodBody { get; set; }

        public static Action<DummyTestClassWithDisposable> DummyTestCleanupMethodBody { get; set; }

        public void DummyTestMethod()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncMethodBody();
            return ValueTask.CompletedTask;
        }
    }

    public class DummyTestClassWithAsyncDisposableAndDisposable : IAsyncDisposable, IDisposable
    {
        public static Action DisposeMethodBody { get; set; }

        public static Action DisposeAsyncMethodBody { get; set; }

        public static Action<DummyTestClassWithDisposable> DummyTestCleanupMethodBody { get; set; }

        public void DummyTestMethod()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncMethodBody();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            DisposeMethodBody();
        }
    }
#endif

}
#endregion
