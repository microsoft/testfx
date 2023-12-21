// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        _methodInfo = typeof(DummyTestClass).GetMethods().Single(m => m.Name.Equals("DummyTestMethod", StringComparison.Ordinal));
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
        object[] arguments = [10, 20, 30];
        _testMethodInfo.SetArguments(arguments);

        Verify(_testMethodInfo.Arguments.Length == 3);
        Verify((int)_testMethodInfo.Arguments[0] == 10);
        Verify((int)_testMethodInfo.Arguments[1] == 20);
        Verify((int)_testMethodInfo.Arguments[2] == 30);
    }

    #region TestMethod invoke scenarios

    public void TestMethodInfoInvokeShouldWaitForAsyncTestMethodsToComplete()
    {
        var methodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { methodCalled = true; });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(methodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeAsyncShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { throw new UTF.AssertInconclusiveException(); });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeAsyncShouldHandleAssertInconclusive()
    {
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { UTF.Assert.Inconclusive(); });
        var asyncMethodInfo = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var method = new TestMethodInfo(
            asyncMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldHandleThrowAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => { throw new UTF.AssertInconclusiveException(); };
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldHandleAssertInconclusive()
    {
        DummyTestClass.TestMethodBody = d => { UTF.Assert.Inconclusive(); };
        var dummyMethodInfo = typeof(DummyTestClass).GetMethod("DummyTestMethod");

        var method = new TestMethodInfo(
            dummyMethodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void TestMethodInfoInvokeShouldReportTestContextMessages()
    {
        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("TestContext"); };

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.TestContextMessages.Contains("TestContext"));
    }

    public void TestMethodInfoInvokeShouldClearTestContextMessagesAfterReporting()
    {
        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("TestContext"); };

        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        var result = method.Invoke(null);

        Verify(result.TestContextMessages.Contains("TestContext"));

        DummyTestClass.TestMethodBody = o => { _testContextImplementation.WriteLine("SeaShore"); };

        result = method.Invoke(null);

        Verify(result.TestContextMessages.Contains("SeaShore"));
    }

    #endregion

    #region TestClass constructor setup

    public void TestMethodInfoInvokeShouldCreateNewInstanceOfTestClassOnEveryCall()
    {
        var ctorCallCount = 0;
        DummyTestClass.TestConstructorMethodBody = () => ctorCallCount++;

        var result = _testMethodInfo.Invoke(null);
        _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(ctorCallCount == 2);
    }

    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

        var result = _testMethodInfo.Invoke(null);

        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InstanceCreationError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(errorMessage == result.TestFailureException.Message);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfTestClassConstructorThrowsWithoutInnerException()
    {
        var ctorInfo = typeof(DummyTestClassWithParameterizedCtor).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithParameterizedCtor), ctorInfo, _testContextProperty, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        var result = method.Invoke(null);
        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
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
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfTestClassConstructorThrows>b__", StringComparison.Ordinal));
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
            "    at System.Reflection.RuntimeConstructorInfo.Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)", StringComparison.Ordinal));
    }

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
        Verify(result.ResultFiles.ToList().Contains("C:\\temp.txt"));
    }

    #endregion

    #region TestClass.TestContext property setup

    public void TestMethodInfoInvokeShouldNotThrowIfTestContextIsNotPresent()
    {
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result;
        void RunMethod() => result = method.Invoke(null);

        RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestContextDoesNotHaveASetter()
    {
        var testContext = typeof(DummyTestClassWithTestContextWithoutSetter).GetProperties().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClass), _constructorInfo, testContext, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(_methodInfo, testClass, _testMethodOptions);

        UTF.TestResult result;
        void RunMethod() => result = method.Invoke(null);

        RunMethod();
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldSetTestContextForTestClassInstance()
    {
        UTFExtension.TestContext testContext = null;
        DummyTestClass.TestContextSetterBody = context => testContext = context as UTFExtension.TestContext;

        _testMethodInfo.Invoke(null);

        Verify(_testContextImplementation.Equals(testContext));
    }

    public void TestMethodInfoInvokeShouldMarkOutcomeFailedIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException(); };

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldSetErrorMessageIfSetTestContextThrows()
    {
        DummyTestClass.TestContextSetterBody = value => { throw new NotImplementedException("dummyExceptionMessage"); };

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_TestContextSetError,
            typeof(DummyTestClass).FullName,
            "System.NotImplementedException: dummyExceptionMessage");
        Verify(exception is not null);
        Verify(errorMessage == exception?.Message);
    }

    public void TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows()
    {
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException("dummyExceptionMessage"); };

        var exception = _testMethodInfo.Invoke(null).TestFailureException as TestFailedException;

        Verify(exception is not null);
        Verify(
            (bool)exception?.StackTraceInformation.ErrorStackTrace.StartsWith(
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeShouldSetStackTraceInformationIfSetTestContextThrows>b__", StringComparison.Ordinal));
    }

    #endregion

    #region TestInitialize method setup

    public void TestMethodInfoInvokeShouldCallTestInitialize()
    {
        var testInitializeCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => testInitializeCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallAsyncTestInitializeAndWaitForCompletion()
    {
        var testInitializeCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { testInitializeCalled = true; });
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(testInitializeCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallTestInitializeOfAllBaseClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestInitializeCalled2"); };
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => { callOrder.Add("baseAsyncTestInitializeCalled1"); });
        DummyTestClass.TestInitializeMethodBody = classInstance => callOrder.Add("classTestInitializeCalled");
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod"));

        var result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "baseAsyncTestInitializeCalled1",
                                        "baseTestInitializeCalled2",
                                        "classTestInitializeCalled",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeIsNull()
    {
        _testClassInfo.TestInitializeMethod = null;

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestInitializeForBaseClassIsNull()
    {
        _testClassInfo.BaseTestInitializeMethodsQueue.Enqueue(null);

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

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
            "    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    public void TestInitialize_WhenTestReturnsTaskFromException_DisplayProperException()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBodyAsync = async classInstance =>
        {
            await Task.FromException<Exception>(new Exception("Outer", new InvalidOperationException("Inner")));
        };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethodAsync");
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.InnerException.GetType() == typeof(Exception));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));

        var expectedErrorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "System.Exception: Outer ---> System.InvalidOperationException: Inner");
        Verify(expectedErrorMessage == exception.Message);

#if NETFRAMEWORK
        Verify(exception.StackTraceInformation.ErrorStackTrace.StartsWith("    at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()", StringComparison.Ordinal));
#else
        Verify(exception.StackTraceInformation.ErrorStackTrace.StartsWith("    at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<<TestInitialize_WhenTestReturnsTaskFromException_DisplayProperException>", StringComparison.Ordinal));
#endif
    }

    public void TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Fail("dummyFailMessage"); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Fail failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertFailReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult()
    {
        // Arrange.
        DummyTestClass.TestInitializeMethodBody = classInstance => { UTF.Assert.Inconclusive("dummyFailMessage"); };
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_InitMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestInitializeMethod.Name,
            "Assert.Inconclusive failed. dummyFailMessage");

        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(errorMessage == exception.Message);
        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestThrowsAssertInconclusiveReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    #endregion

    #region TestCleanup method setup

    public void TestCleanup_WhenTestReturnsTaskFromException_DisplayProperException()
    {
        // Arrange.
        DummyTestClass.TestCleanupMethodBodyAsync = async classInstance =>
        {
            await Task.FromException<Exception>(new Exception("Outer", new InvalidOperationException("Inner")));
        };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethodAsync");
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        // Act.
        var result = testMethodInfo.Invoke(null);

        // Assert.
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(exception.InnerException.GetType() == typeof(Exception));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));

        var errorMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            "System.Exception: Outer ---> System.InvalidOperationException: Inner");
        Verify(errorMessage == exception.Message);

#if NETFRAMEWORK
        if (!exception.StackTraceInformation.ErrorStackTrace.StartsWith("   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()", StringComparison.Ordinal))
        {
            throw new Exception($"Expected stack trace to start with '   at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()' but\n{exception.StackTraceInformation.ErrorStackTrace}\n does not.");
        }
#else
        if (!exception.StackTraceInformation.ErrorStackTrace.StartsWith("   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<<TestCleanup_WhenTestReturnsTaskFromException_DisplayProperException>", StringComparison.Ordinal))
        {
            throw new Exception($"Expected stack trace to start with '   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<<TestCleanup_WhenTestReturnsTaskFromException_DisplayProperException>' but\n{exception.StackTraceInformation.ErrorStackTrace}\n does not.");
        }
#endif
    }

    public void TestMethodInfoInvokeShouldCallTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.DummyAsyncTestMethodBody = () => Task.Run(() => cleanupMethodCalled = true);
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyAsyncTestMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public void TestMethodInfoInvokeShouldCallAsyncTestCleanup()
    {
        var cleanupMethodCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => { cleanupMethodCalled = true; };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(cleanupMethodCalled);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodIsNull()
    {
        _testClassInfo.TestCleanupMethod = null;

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldNotThrowIfTestCleanupMethodForBaseClassIsNull()
    {
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(null);

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClasses()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
        DummyTestClass.TestCleanupMethodBody = classInstance => callOrder.Add("classTestCleanupCalled");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));
        _testClassInfo.BaseTestCleanupMethodsQueue.Enqueue(typeof(DummyTestClassBase).GetMethod("DummyBaseTestClassMethod"));

        var result = _testMethodInfo.Invoke(null);

        var expectedCallOrder = new List<string>
                                    {
                                        "classTestCleanupCalled",
                                        "baseTestCleanupCalled1",
                                        "baseTestCleanupCalled2",
                                    };
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupForBaseTestClassesAlways()
    {
        var callOrder = new List<string>();
        DummyTestClassBase.BaseTestClassMethodBody = classInstance => { callOrder.Add("baseTestCleanupCalled" + callOrder.Count); };
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
                                        "baseTestCleanupCalled5",
                                    };

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
        Verify(expectedCallOrder.SequenceEqual(callOrder));
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new ArgumentException("Some exception message", new InvalidOperationException("Inner exception message")); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            "System.ArgumentException: Some exception message ---> System.InvalidOperationException: Inner exception message");

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(ArgumentException));
        Verify(exception.InnerException.InnerException.GetType() == typeof(InvalidOperationException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { UTF.Assert.Inconclusive("Test inconclusive"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException: Assert.Inconclusive failed. Test inconclusive");

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Inconclusive);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertInconclusiveException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertInconclusiveReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { UTF.Assert.Fail("Test failed"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var expectedErrorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            typeof(DummyTestClass).FullName,
            _testClassInfo.TestCleanupMethod.Name,
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException: Assert.Fail failed. Test failed");

        var result = _testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);

        var exception = result.TestFailureException as TestFailedException;
        Verify(exception is not null);
        Verify(exception.Outcome == UnitTestOutcome.Failed);
        Verify(expectedErrorMessage == exception.Message);
        Verify(exception.InnerException.GetType() == typeof(UTF.AssertFailedException));

        Verify(
            exception.StackTraceInformation.ErrorStackTrace.StartsWith(
            "   at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.<>c.<TestMethodInfoInvokeWhenTestCleanupThrowsAssertFailedReturnsExpectedResult>b__", StringComparison.Ordinal));
    }

    public void TestMethodInfoInvokeShouldAppendErrorMessagesIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException("dummyErrorMessage"); };
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
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
            "System.NotImplementedException: dummyErrorMessage");

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
        Verify(string.Concat(errorMessage, Environment.NewLine, cleanupError) == exception.Message);
    }

    public void TestMethodInfoInvokeShouldAppendStackTraceInformationIfBothTestMethodAndTestCleanupThrows()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException("dummyMethodError"); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        Verify(exception is not null);
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestMethod()"));
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains(Resource.UTA_CleanupStackTrace));
        Verify(exception.StackTraceInformation.ErrorStackTrace.Contains("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.DummyTestClass.DummyTestCleanupMethod()"));
    }

    public void TestMethodInfoInvokeShouldSetOutcomeAsInconclusiveIfTestCleanupIsInconclusive()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        Verify(exception is not null);
        Verify((bool)exception?.Message.Contains("Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException"));
    }

    public void TestMethodInfoInvokeShouldSetMoreImportantOutcomeIfTestCleanupIsInconclusiveButTestMethodFails()
    {
        DummyTestClass.TestCleanupMethodBody = classInstance => { throw new UTF.AssertInconclusiveException(); };
        DummyTestClass.TestMethodBody = classInstance => { Fail(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);
        var exception = result.TestFailureException as TestFailedException;

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallDiposeForDisposableTestClass()
    {
        var disposeCalled = false;
        DummyTestClassWithDisposable.DisposeMethodBody = () => disposeCalled = true;
        var ctorInfo = typeof(DummyTestClassWithDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        method.Invoke(null);

        Verify(disposeCalled);
    }

#if NET6_0_OR_GREATER
    public void TestMethodInfoInvoke_WhenTestClassIsAsyncDisposable_ShouldDisposeAsync()
    {
        // Arrange
        var asyncDisposeCalled = false;
        DummyTestClassWithAsyncDisposable.DisposeAsyncMethodBody = () => asyncDisposeCalled = true;
        var ctorInfo = typeof(DummyTestClassWithAsyncDisposable).GetConstructors().Single();
        var testClass = new TestClassInfo(typeof(DummyTestClassWithAsyncDisposable), ctorInfo, null, _classAttribute, _testAssemblyInfo);
        var method = new TestMethodInfo(typeof(DummyTestClassWithAsyncDisposable).GetMethod("DummyTestMethod"), testClass, _testMethodOptions);

        // Act
        method.Invoke(null);

        // Assert
        Verify(asyncDisposeCalled);
    }

    public void TestMethodInfoInvoke_WhenTestClassIsDisposableAndAsyncDisposable_ShouldCallAsyncDiposeThenDipose()
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
        method.Invoke(null);

        // Assert
        Verify(disposeCalledOrder == 2);
        Verify(disposeAsyncCalledOrder == 1);
    }
#endif

    public void TestMethodInfoInvokeShouldCallDisposeForDisposableTestClassIfTestCleanupThrows()
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

        method.Invoke(null);

        Verify(disposeCalled);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupEvenIfTestInitializeMethodThrows()
    {
        var testCleanupMethodCalled = false;
        DummyTestClass.TestInitializeMethodBody = classInstance => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        var result = _testMethodInfo.Invoke(null);

        Verify(testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldCallTestCleanupIfTestClassInstanceIsNotNull()
    {
        var testCleanupMethodCalled = false;

        // Throwing in constructor to ensure classInstance is null in TestMethodInfo.Invoke
        DummyTestClass.TestConstructorMethodBody = () => { throw new NotImplementedException(); };
        DummyTestClass.TestCleanupMethodBody = classInstance => testCleanupMethodCalled = true;

        var result = _testMethodInfo.Invoke(null);

        Verify(!testCleanupMethodCalled);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void TestMethodInfoInvokeShouldNotCallTestCleanupIfClassSetContextThrows()
    {
        var testCleanupCalled = false;
        DummyTestClass.TestCleanupMethodBody = classInstance => { testCleanupCalled = true; };
        DummyTestClass.TestContextSetterBody = o => { throw new NotImplementedException(); };
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        _testMethodInfo.Invoke(null);

        Verify(!testCleanupCalled);
    }

    public void TestMethodInfoInvokeShouldSetResultAsPassedIfExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void TestMethodInfoInvokeShouldSetResultAsFailedIfExceptionDifferentFromExpectedExceptionIsThrown()
    {
        DummyTestClass.TestMethodBody = o => { throw new IndexOutOfRangeException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = testMethodInfo.Invoke(null);

        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        var message = "Test method threw exception System.IndexOutOfRangeException, but exception System.DivideByZeroException was expected. " +
            "Exception message: System.IndexOutOfRangeException: Index was outside the bounds of the array.";
        Verify(message == result.TestFailureException.Message);
    }

    public void TestMethodInfoInvokeShouldSetResultAsFailedWhenExceptionIsExpectedButIsNotThrown()
    {
        DummyTestClass.TestMethodBody = o => { return; };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
        var message = "Test method did not throw expected exception System.DivideByZeroException.";
        Verify(result.TestFailureException.Message.Contains(message));
    }

    public void TestMethodInfoInvokeShouldSetResultAsInconclusiveWhenExceptionIsAssertInconclusiveException()
    {
        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = testMethodInfo.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(message == result.TestFailureException.Message);
    }

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
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");
        _testMethodOptions.ExpectedException = _expectedException;
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

        var result = testMethodInfo.Invoke(null);

        Verify(testOutcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void HandleMethodExceptionShouldInvokeVerifyOfCustomExpectedException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = method.Invoke(null);
        Verify(customExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void HandleMethodExceptionShouldSetOutcomeAsFailedIfVerifyOfExpectedExceptionThrows()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = method.Invoke(null);
        Verify(result.TestFailureException.Message == "The exception message doesn't contain the string defined in the exception attribute");
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void HandleMethodExceptionShouldSetOutcomeAsInconclusiveIfVerifyOfExpectedExceptionThrowsAssertInconclusiveException()
    {
        CustomExpectedExceptionAttribute customExpectedException = new(typeof(DivideByZeroException), "Custom Exception");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = customExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void HandleMethodExceptionShouldInvokeVerifyOfDerivedCustomExpectedException()
    {
        DerivedCustomExpectedExceptionAttribute derivedCustomExpectedException = new(typeof(DivideByZeroException), "Attempted to divide by zero");
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = derivedCustomExpectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = method.Invoke(null);
        Verify(derivedCustomExpectedException.IsVerifyInvoked);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void VerifyShouldNotThrowIfThrownExceptionCanBeAssignedToExpectedException()
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
        var result = method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

    public void VerifyShouldThrowExceptionIfThrownExceptionCannotBeAssignedToExpectedException()
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
        var result = method.Invoke(null);
        var message = "Test method threw exception System.ArgumentNullException, but exception System.DivideByZeroException" +
            " or a type derived from it was expected. Exception message: System.ArgumentNullException: Value cannot be null.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertFailedException()
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
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowExceptionIfThrownExceptionIsAssertInconclusiveException()
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
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    public void VerifyShouldThrowIfThrownExceptionIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new DivideByZeroException(); };
        var result = method.Invoke(null);
        var message = "Test method threw exception System.DivideByZeroException, but exception System.Exception was expected. " +
            "Exception message: System.DivideByZeroException: Attempted to divide by zero.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Failed);
    }

    public void VerifyShouldRethrowIfThrownExceptionIsAssertExceptionWhichIsNotSameAsExpectedException()
    {
        UTF.ExpectedExceptionAttribute expectedException = new(typeof(Exception));
        _testMethodOptions.Timeout = 0;
        _testMethodOptions.ExpectedException = expectedException;
        var method = new TestMethodInfo(
            _methodInfo,
            _testClassInfo,
            _testMethodOptions);

        DummyTestClass.TestMethodBody = o => { throw new UTF.AssertInconclusiveException(); };
        var result = method.Invoke(null);
        var message = "Exception of type 'Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException' was thrown.";
        Verify(result.TestFailureException.Message == message);
        Verify(result.Outcome == UTF.UnitTestOutcome.Inconclusive);
    }

    #endregion

    #region TestMethod invoke setup order

    public void TestMethodInfoInvokeShouldInitializeClassInstanceTestInitializeAndTestCleanupInOrder()
    {
        var callOrder = new List<string>();
        _testClassInfo.TestInitializeMethod = typeof(DummyTestClass).GetMethod("DummyTestInitializeMethod");
        _testClassInfo.TestCleanupMethod = typeof(DummyTestClass).GetMethod("DummyTestCleanupMethod");

        DummyTestClass.TestConstructorMethodBody = () => { callOrder.Add("classCtor"); };
        DummyTestClass.TestContextSetterBody = o => { callOrder.Add("testContext"); };
        DummyTestClass.TestInitializeMethodBody = classInstance => { callOrder.Add("testInit"); };
        DummyTestClass.TestMethodBody = classInstance => { callOrder.Add("testMethod"); };
        DummyTestClass.TestCleanupMethodBody = classInstance => { callOrder.Add("testCleanup"); };

        var result = _testMethodInfo.Invoke(null);

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

        RunWithTestablePlatformService(testablePlatformServiceProvider, () =>
        {
            testablePlatformServiceProvider.MockThreadOperations.CallBase = true;

            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            testablePlatformServiceProvider.MockThreadOperations.Setup(
             to => to.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(false);
            _testMethodOptions.Timeout = 1;
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);

            var result = method.Invoke(null);

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException.Message.Contains("exceeded execution timeout period"));
        });
    }

    public void TestMethodInfoInvokeShouldReturnTestPassedOnCompletionWithinTimeout()
    {
        DummyTestClass.TestMethodBody = o => { /* do nothing */ };
        var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
        var result = method.Invoke(null);
        Verify(result.Outcome == UTF.UnitTestOutcome.Passed);
    }

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

            Verify(result.Outcome == UTF.UnitTestOutcome.Timeout);
            Verify(result.TestFailureException.Message.Contains("exceeded execution timeout period"));
            Verify(_testContextImplementation.CancellationTokenSource.IsCancellationRequested, "Not canceled..");
        });
    }

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
                     Task.WaitAny([Task.Delay(100000, cancelToken)], cancelToken);
                 }
                 catch (OperationCanceledException)
                 {
                 }
             });

            _testMethodOptions.Timeout = 100000;
            _testContextImplementation.CancellationTokenSource.CancelAfter(100);
            var method = new TestMethodInfo(_methodInfo, _testClassInfo, _testMethodOptions);
            var result = method.Invoke(null);

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

        object[] arguments = ["RequiredStr1"];
        object[] expectedArguments = ["RequiredStr1"];
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

        object[] arguments = ["RequiredStr1", "RequiredStr2", "ExtraStr3"];
        object[] expectedArguments = ["RequiredStr1", "RequiredStr2", "ExtraStr3"];
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

        object[] arguments = ["RequiredStr1"];
        object[] expectedArguments = ["RequiredStr1", null, null];
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

        object[] arguments = ["RequiredStr1", "OptionalStr1"];
        object[] expectedArguments = ["RequiredStr1", "OptionalStr1", null];
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

        object[] arguments = [1];
        object[] expectedArguments = [1, Array.Empty<string>()];
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

        object[] arguments = [1, "str1", "str2", "str3"];
        object[] expectedArguments = [1, new string[] { "str1", "str2", "str3" }];
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
                Setup(tho => tho.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).
                Returns(true).
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

        public static Func<DummyTestClass, Task> TestInitializeMethodBodyAsync { get; set; }

        public static Action<DummyTestClass> TestInitializeMethodBody { get; set; }

        public static Action<DummyTestClass> TestMethodBody { get; set; }

        public static Action<DummyTestClass> TestCleanupMethodBody { get; set; }

        public static Func<DummyTestClass, Task> TestCleanupMethodBodyAsync { get; set; }

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
        public async Task DummyTestInitializeMethodAsync()
        {
            await TestInitializeMethodBodyAsync(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void DummyTestCleanupMethod()
        {
            TestCleanupMethodBody(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task DummyTestCleanupMethodAsync()
        {
            await TestCleanupMethodBodyAsync(this);
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
