// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ObjectModelUnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Defines the TestMethod Info object
/// </summary>
public class TestMethodInfo : ITestMethod
{
    /// <summary>
    /// Specifies the timeout when it is not set in a test case
    /// </summary>
    public const int TimeoutWhenNotSet = 0;

    private object[] _arguments;

    internal TestMethodInfo(
        MethodInfo testMethod,
        TestClassInfo parent,
        TestMethodOptions testmethodOptions)
    {
        Debug.Assert(testMethod != null, "TestMethod should not be null");
        Debug.Assert(parent != null, "Parent should not be null");

        TestMethod = testMethod;
        Parent = parent;
        TestMethodOptions = testmethodOptions;
    }

    /// <summary>
    /// Gets a value indicating whether timeout is set.
    /// </summary>
    public bool IsTimeoutSet => TestMethodOptions.TimeoutContext.Timeout != TimeoutWhenNotSet;

    /// <summary>
    /// Gets the reason why the test is not runnable
    /// </summary>
    public string NotRunnableReason { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether test is runnable
    /// </summary>
    public bool IsRunnable => string.IsNullOrEmpty(NotRunnableReason);

    public ParameterInfo[] ParameterTypes => TestMethod.GetParameters();

    public Type ReturnType => TestMethod.ReturnType;

    public string TestClassName => Parent.ClassType.FullName;

    public string TestMethodName => TestMethod.Name;

    public MethodInfo MethodInfo => TestMethod;

    public object[] Arguments => _arguments;

    /// <summary>
    /// Gets testMethod referred by this object
    /// </summary>
    internal MethodInfo TestMethod { get; private set; }

    /// <summary>
    /// Gets the parent class Info object
    /// </summary>
    internal TestClassInfo Parent { get; private set; }

    /// <summary>
    /// Gets the options for the test method in this environment.
    /// </summary>
    internal TestMethodOptions TestMethodOptions { get; private set; }

    public Attribute[] GetAllAttributes(bool inherit)
    {
        return ReflectHelper.GetCustomAttributes(TestMethod, inherit) as Attribute[];
    }

    public TAttributeType[] GetAttributes<TAttributeType>(bool inherit)
        where TAttributeType : Attribute
        => ReflectHelper.GetAttributes<TAttributeType>(TestMethod, inherit)
        ?? EmptyHolder<TAttributeType>.Array;

    /// <summary>
    /// Execute test method. Capture failures, handle async and return result.
    /// </summary>
    /// <param name="arguments">
    ///  Arguments to pass to test method. (E.g. For data driven)
    /// </param>
    /// <returns>Result of test method invocation.</returns>
    public virtual TestResult Invoke(object[] arguments)
    {
        Stopwatch watch = new();
        TestResult result = null;

        // check if arguments are set for data driven tests
        arguments ??= Arguments;

        using (LogMessageListener listener = new(TestMethodOptions.CaptureDebugTraces))
        {
            watch.Start();
            try
            {
                result = IsTimeoutSet
                    ? ExecuteInternalWithTimeout(arguments)
                    : ExecuteInternal(arguments, CancellationToken.None, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                // Handle logs & debug traces.
                watch.Stop();

                if (result != null)
                {
                    result.Duration = watch.Elapsed;
                    result.DebugTrace = listener.GetAndClearDebugTrace();
                    result.LogOutput = listener.GetAndClearStandardOutput();
                    result.LogError = listener.GetAndClearStandardError();
                    result.TestContextMessages = TestMethodOptions.TestContext.GetAndClearDiagnosticMessages();
                    result.ResultFiles = TestMethodOptions.TestContext.GetResultFiles();
                }
            }
        }

        return result;
    }

    internal void SetArguments(object[] arguments)
    {
        if (arguments == null)
        {
            _arguments = null;
        }
        else
        {
            _arguments = ResolveArguments(arguments);
        }
    }

    internal object[] ResolveArguments(object[] arguments)
    {
        ParameterInfo[] parameterInfos = TestMethod.GetParameters();
        int requiredParameterCount = 0;
        bool hasParamsValue = false;
        object paramsValues = null;
        foreach (var parameter in parameterInfos)
        {
            // If this is a params array parameter, create an instance to
            // populate with any extra values provided. Don't increment
            // required parameter count - params arguments are not actually required
            if (parameter.GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
            {
                hasParamsValue = true;
                break;
            }

            // Count required parameters from method
            if (!parameter.IsOptional)
            {
                requiredParameterCount++;
            }
        }

        // If all the parameters are required, we have fewer arguments
        // supplied than required, or more arguments than the method takes
        // and it doesn't have a params parameter don't try and resolve anything
        if (requiredParameterCount == parameterInfos.Length ||
            arguments.Length < requiredParameterCount ||
            (!hasParamsValue && arguments.Length > parameterInfos.Length))
        {
            return arguments;
        }

        object[] newParameters = new object[parameterInfos.Length];
        for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            // We have reached the end of the regular parameters and any additional
            // values will go in a params array
            if (argumentIndex >= parameterInfos.Length - 1 && hasParamsValue)
            {
                // If this is the params parameter, instantiate a new object of that type
                if (argumentIndex == parameterInfos.Length - 1)
                {
                    paramsValues = Activator.CreateInstance(parameterInfos[argumentIndex].ParameterType, new object[] { arguments.Length - argumentIndex });
                    newParameters[argumentIndex] = paramsValues;
                }

                // The params parameters is an array but the type is not known
                // set the values as a generic array
                if (paramsValues is Array paramsArray)
                {
                    paramsArray.SetValue(arguments[argumentIndex], argumentIndex - (parameterInfos.Length - 1));
                }
            }
            else
            {
                newParameters[argumentIndex] = arguments[argumentIndex];
            }
        }

        // If arguments supplied are less than total possible arguments set
        // the values supplied to the default values for those parameters
        for (int parameterNotProvidedIndex = arguments.Length; parameterNotProvidedIndex < parameterInfos.Length; parameterNotProvidedIndex++)
        {
            // If this is the params parameters, set it to an empty
            // array of that type as DefaultValue is DBNull
            if (hasParamsValue && parameterNotProvidedIndex == parameterInfos.Length - 1)
            {
                newParameters[parameterNotProvidedIndex] = Activator.CreateInstance(parameterInfos[parameterNotProvidedIndex].ParameterType, 0);
            }
            else
            {
                newParameters[parameterNotProvidedIndex] = parameterInfos[parameterNotProvidedIndex].DefaultValue;
            }
        }

        return newParameters;
    }

    /// <summary>
    /// Execute test without timeout.
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the method.</param>
    /// <returns>The result of the execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternal(object[] arguments, CancellationToken testRunCancellationToken, CancellationToken cleanupCancellationToken)
    {
        Debug.Assert(TestMethod != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

        var result = new TestResult();

        // TODO remove dry violation with TestMethodRunner
        var classInstance = await CreateTestClassInstance(result, testRunCancellationToken);
        var testContextSetup = false;
        bool isExceptionThrown = false;
        bool hasTestInitializePassed = false;
        Exception testRunnerException = null;

        try
        {
            try
            {
                if (classInstance != null && SetTestContext(classInstance, result))
                {
                    // For any failure after this point, we must run TestCleanup
                    testContextSetup = true;

                    if (await TryRunTestInitializeMethod(classInstance, result, testRunCancellationToken)
                            .WithCancellation(testRunCancellationToken))
                    {
                        hasTestInitializePassed = true;
                        await TestMethod.InvokeAsTask(classInstance, testRunCancellationToken, arguments)
                            .WithCancellation(testRunCancellationToken);
                        result.Outcome = UTF.UnitTestOutcome.Passed;
                    }
                }
            }
            catch (Exception ex)
            {
                isExceptionThrown = true;

                if (IsExpectedException(ex, result))
                {
                    // Expected Exception was thrown, so Pass the test
                    result.Outcome = UTF.UnitTestOutcome.Passed;
                }
                else
                {
                    // This block should not throw. If it needs to throw, then handling of
                    // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                    result.TestFailureException ??= HandleMethodException(
                        ex,
                        TestClassName,
                        TestMethodName);
                }

                if (result.Outcome != UTF.UnitTestOutcome.Passed)
                {
                    result.Outcome = ex is UTF.AssertInconclusiveException || ex.InnerException is UTF.AssertInconclusiveException
                        ? UTF.UnitTestOutcome.Inconclusive
                        : UTF.UnitTestOutcome.Failed;
                }
            }

            // if we get here, the test method did not throw the exception
            // if the user specified that the test was going to throw an exception, and
            // it did not, we should fail the test
            // We only perform this check if the test initialize passes and the test method is actually run.
            if (hasTestInitializePassed && !isExceptionThrown && TestMethodOptions.ExpectedException != null)
            {
                result.TestFailureException = new TestFailedException(
                    ObjectModelUnitTestOutcome.Failed,
                    TestMethodOptions.ExpectedException.NoExceptionMessage);
                result.Outcome = UTF.UnitTestOutcome.Failed;
            }
        }
        catch (Exception exception)
        {
            testRunnerException = exception;
        }

        // Set the current tests outcome before cleanup so it can be used in the cleanup logic.
        TestMethodOptions.TestContext.SetOutcome(result.Outcome);

        // TestCleanup can potentially be a long running operation which shouldn't ideally be in a finally block.
        // Pulling it out so extension writers can abort custom cleanups if need be. Having this in a finally block
        // does not allow a thread abort exception to be raised within the block but throws one after finally is executed
        // crashing the process. This was blocking writing an extension for Dynamic TimeoutContext in VSO.
        if (classInstance != null && testContextSetup && !cleanupCancellationToken.IsCancellationRequested)
        {
            await RunTestCleanupMethod(classInstance, result, cleanupCancellationToken);
        }

        if (testRunnerException != null)
        {
            throw testRunnerException;
        }

        return result;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private bool IsExpectedException(Exception ex, TestResult result)
    {
        Exception realException = GetRealException(ex);

        // if the user specified an expected exception, we need to check if this
        // exception was thrown. If it was thrown, we should pass the test. In
        // case a different exception was thrown, the test is seen as failure
        if (TestMethodOptions.ExpectedException != null)
        {
            Exception exceptionFromVerify;
            try
            {
                // If the expected exception attribute's Verify method returns, then it
                // considers this exception as expected, so the test passed
                TestMethodOptions.ExpectedException.Verify(realException);
                return true;
            }
            catch (Exception verifyEx)
            {
                var isTargetInvocationError = verifyEx is TargetInvocationException;
                if (isTargetInvocationError && verifyEx.InnerException != null)
                {
                    exceptionFromVerify = verifyEx.InnerException;
                }
                else
                {
                    // Verify threw an exception, so the expected exception attribute does not
                    // consider this exception to be expected. Include the exception message in
                    // the test result.
                    exceptionFromVerify = verifyEx;
                }
            }

            // See if the verification exception (thrown by the expected exception
            // attribute's Verify method) is an AssertInconclusiveException. If so, set
            // the test outcome to Inconclusive.
            result.TestFailureException = new TestFailedException(
                exceptionFromVerify is UTF.AssertInconclusiveException
                    ? ObjectModelUnitTestOutcome.Inconclusive
                    : ObjectModelUnitTestOutcome.Failed,
                exceptionFromVerify.TryGetMessage(),
                realException.TryGetStackTraceInformation());
            return false;
        }
        else
        {
            return false;
        }
    }

    private static Exception GetRealException(Exception ex)
    {
        if (ex is TargetInvocationException)
        {
            Debug.Assert(ex.InnerException != null, "Inner exception of TargetInvocationException is null. This should occur because we should have caught this case above.");

            // Our reflected call will typically always get back a TargetInvocationException
            // containing the real exception thrown by the test method as its inner exception
            return ex.InnerException;
        }
        else
        {
            return ex;
        }
    }

    /// <summary>
    /// Handles the exception that is thrown by a test method. The exception can either
    /// be expected or not expected
    /// </summary>
    /// <param name="ex">Exception that was thrown</param>
    /// <param name="className">The class name.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>Test framework exception with details.</returns>
    private static Exception HandleMethodException(Exception ex, string className, string methodName)
    {
        Debug.Assert(ex != null, "exception should not be null.");

        var isTargetInvocationException = ex is TargetInvocationException;
        if (isTargetInvocationException && ex.InnerException == null)
        {
            var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
            return new TestFailedException(ObjectModelUnitTestOutcome.Error, errorMessage);
        }

        // Get the real exception thrown by the test method
        Exception realException = GetRealException(ex);
        if (realException.TryGetUnitTestAssertException(out var outcome, out var exceptionMessage, out var exceptionStackTraceInfo))
        {
            return new TestFailedException(outcome.ToUnitTestOutcome(), exceptionMessage, exceptionStackTraceInfo, realException);
        }
        else
        {
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestMethodThrows,
                className,
                methodName,
                StackTraceHelper.GetExceptionMessage(realException));

            // Handle special case of UI objects in TestMethod to suggest UITestMethod
            if (realException.HResult == -2147417842)
            {
                errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_WrongThread, errorMessage);
            }

            StackTraceInformation stackTrace = null;

            // For ThreadAbortException (that can be thrown only by aborting a thread as there's no public constructor)
            // there's no inner exception and exception itself contains reflection-related stack trace
            // (_RuntimeMethodHandle.InvokeMethodFast <- _RuntimeMethodHandle.Invoke <- UnitTestExecuter.RunTestMethod)
            // which has no meaningful info for the user. Thus, we do not show call stack for ThreadAbortException.
            if (realException.GetType().Name != "ThreadAbortException")
            {
                stackTrace = StackTraceHelper.GetStackTraceInformation(realException);
            }

            return new TestFailedException(ObjectModelUnitTestOutcome.Failed, errorMessage, stackTrace, realException);
        }
    }

    /// <summary>
    /// Runs TestCleanup methods of parent TestClass and base classes.
    /// </summary>
    /// <param name="classInstance">Instance of TestClass.</param>
    /// <param name="result">Instance of TestResult.</param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task RunTestCleanupMethod(object classInstance, TestResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(classInstance != null, "classInstance != null");
        Debug.Assert(result != null, "result != null");

        var testCleanupMethod = Parent.TestCleanupMethod;
        try
        {
            try
            {
                // Test cleanups are called in the order of discovery
                // Current TestClass -> Parent -> Grandparent
                if (testCleanupMethod is not null)
                {
                    await testCleanupMethod.InvokeAsTask(classInstance, cancellationToken, null)
                        .WithCancellation(cancellationToken);
                }

                var baseTestCleanupQueue = new Queue<MethodInfo>(Parent.BaseTestCleanupMethodsQueue);
                while (baseTestCleanupQueue.Count > 0)
                {
                    testCleanupMethod = baseTestCleanupQueue.Dequeue();
                    if (testCleanupMethod is not null)
                    {
                        await testCleanupMethod.InvokeAsTask(classInstance, cancellationToken, null)
                            .WithCancellation(cancellationToken);
                    }
                }
            }
            finally
            {
                if (classInstance is IDisposable disposable)
                {
                    await Task.Run(disposable.Dispose, cancellationToken)
                        .WithCancellation(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            var cleanupError = new StringBuilder();
            var cleanupStackTrace = new StringBuilder();
            if (result.TestFailureException is TestFailedException testFailureException)
            {
                if (!string.IsNullOrEmpty(testFailureException.Message))
                {
                    cleanupError.Append(testFailureException.Message);
                    cleanupError.AppendLine();
                }

                if (!string.IsNullOrEmpty(testFailureException.StackTraceInformation?.ErrorStackTrace))
                {
                    cleanupStackTrace.Append(testFailureException.StackTraceInformation.ErrorStackTrace);
                    cleanupStackTrace.Append(Environment.NewLine);
                    cleanupStackTrace.Append(Resource.UTA_CleanupStackTrace);
                    cleanupStackTrace.Append(Environment.NewLine);
                }
            }

            Exception realException = ex.GetInnerExceptionOrDefault();

            if (testCleanupMethod != null)
            {
                // Do not use StackTraceHelper.GetExceptionMessage(realException) as it prefixes the message with the exception type name.
                cleanupError.AppendFormat(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_CleanupMethodThrows,
                    TestClassName,
                    testCleanupMethod.Name,
                    realException.GetType().ToString(),
                    realException.TryGetMessage());
            }
            else
            {
                // Use StackTraceHelper.GetExceptionMessage(realException) to get the message prefixed with the exception type name.
                cleanupError.AppendFormat(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_CleanupMethodThrowsGeneralError,
                    TestClassName,
                    StackTraceHelper.GetExceptionMessage(realException));
            }

            StackTraceInformation cleanupStackTraceInfo = null;
            var realExceptionStackTraceInfo = realException.TryGetStackTraceInformation();
            if (realExceptionStackTraceInfo != null)
            {
                cleanupStackTrace.Append(realExceptionStackTraceInfo.ErrorStackTrace);
                cleanupStackTraceInfo ??= realExceptionStackTraceInfo;
            }

            var finalStackTraceInfo = cleanupStackTraceInfo != null
                ? new StackTraceInformation(
                    cleanupStackTrace.ToString(),
                    cleanupStackTraceInfo.ErrorFilePath,
                    cleanupStackTraceInfo.ErrorLineNumber,
                    cleanupStackTraceInfo.ErrorColumnNumber)
                : new StackTraceInformation(cleanupStackTrace.ToString());

            result.Outcome = result.Outcome.GetMoreImportantOutcome(realException is AssertInconclusiveException ? UTF.UnitTestOutcome.Inconclusive : UTF.UnitTestOutcome.Failed);
            result.TestFailureException = new TestFailedException(result.Outcome.ToUnitTestOutcome(), cleanupError.ToString(), finalStackTraceInfo, realException);
        }
    }

    /// <summary>
    /// Runs TestInitialize methods of parent TestClass and the base classes.
    /// </summary>
    /// <param name="classInstance">Instance of TestClass.</param>
    /// <param name="result">Instance of TestResult.</param>
    /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<bool> TryRunTestInitializeMethod(object classInstance, TestResult result, CancellationToken cancellationToken)
    {
        Debug.Assert(classInstance != null, "classInstance != null");
        Debug.Assert(result != null, "result != null");

        MethodInfo testInitializeMethod = null;
        try
        {
            // TestInitialize methods for base classes are called in reverse order of discovery
            // Grandparent -> Parent -> Child TestClass
            var baseTestInitializeStack = new Stack<MethodInfo>(Parent.BaseTestInitializeMethodsQueue);
            while (baseTestInitializeStack.Count > 0)
            {
                testInitializeMethod = baseTestInitializeStack.Pop();
                if (testInitializeMethod is not null)
                {
                    await testInitializeMethod.InvokeAsTask(classInstance, cancellationToken, null);
                }
            }

            testInitializeMethod = Parent.TestInitializeMethod;
            if (testInitializeMethod is not null)
            {
                await testInitializeMethod.InvokeAsTask(classInstance, cancellationToken, null);
            }

            return true;
        }
        catch (Exception ex)
        {
            var realException = ex.GetInnerExceptionOrDefault();

            // Prefix the exception message with the exception type name as prefix when exception is not assert exception.
            var exceptionMessage = realException is UnitTestAssertException
                ? realException.TryGetMessage()
                : StackTraceHelper.GetExceptionMessage(realException);
            var errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_InitMethodThrows,
                TestClassName,
                testInitializeMethod?.Name,
                exceptionMessage);
            var stackTrace = StackTraceHelper.GetStackTraceInformation(realException);

            result.Outcome = realException is AssertInconclusiveException ? UTF.UnitTestOutcome.Inconclusive : UTF.UnitTestOutcome.Failed;
            result.TestFailureException = new TestFailedException(
                result.Outcome.ToUnitTestOutcome(),
                errorMessage,
                stackTrace,
                realException);
        }

        return false;
    }

    /// <summary>
    /// Sets the <see cref="TestContext"/> on <see cref="classInstance"/>.
    /// </summary>
    /// <param name="classInstance">
    /// Reference to instance of TestClass.
    /// </param>
    /// <param name="result">
    /// Reference to instance of <see cref="TestResult"/>.
    /// </param>
    /// <returns>
    /// True if there no exceptions during set context operation.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private bool SetTestContext(object classInstance, TestResult result)
    {
        Debug.Assert(classInstance != null, "classInstance != null");
        Debug.Assert(result != null, "result != null");

        try
        {
            if (Parent.TestContextProperty != null && Parent.TestContextProperty.CanWrite)
            {
                Parent.TestContextProperty.SetValue(classInstance, TestMethodOptions.TestContext);
            }

            return true;
        }
        catch (Exception ex)
        {
            var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(ex.GetInnerExceptionOrDefault());
            var errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestContextSetError,
                TestClassName,
                StackTraceHelper.GetExceptionMessage(ex.GetInnerExceptionOrDefault()));

            result.Outcome = UTF.UnitTestOutcome.Failed;
            result.TestFailureException = new TestFailedException(ObjectModelUnitTestOutcome.Failed, errorMessage, stackTraceInfo);
        }

        return false;
    }

    /// <summary>
    /// Creates an instance of TestClass. The TestMethod is invoked on this instance.
    /// </summary>
    /// <param name="result">
    /// Reference to the <see cref="TestResult"/> for this TestMethod.
    /// Outcome and TestFailureException are updated based on instance creation.
    /// </param>
    /// <returns>
    /// An instance of the TestClass. Returns null if there are errors during class instantiation.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<object> CreateTestClassInstance(TestResult result, CancellationToken cancellationToken)
    {
        object classInstance = null;
        try
        {
            classInstance = await Task.Run(() => Parent.Constructor.Invoke(null), cancellationToken)
                .WithCancellation(cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex == null)
            {
                // It seems that ex can be null in some rare cases when initialization fails in native code.
                // Get our own exception with a stack trace to satisfy GetStackTraceInformation.
                try
                {
                    throw new InvalidOperationException(Resource.UTA_UserCodeThrewNullValueException);
                }
                catch (Exception exception)
                {
                    ex = exception;
                }
            }

            // In most cases, exception will be TargetInvocationException with real exception wrapped
            // in the InnerException; or user code throws an exception.
            // It also seems that in rare cases the ex can be null.
            var actualException = ex.InnerException ?? ex;
            var exceptionMessage = StackTraceHelper.GetExceptionMessage(actualException);
            var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(actualException);

            var errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_InstanceCreationError,
                TestClassName,
                exceptionMessage);

            result.Outcome = UTF.UnitTestOutcome.Failed;
            result.TestFailureException = new TestFailedException(ObjectModelUnitTestOutcome.Failed, errorMessage, stackTraceInfo);
        }

        return classInstance;
    }

    /// <summary>
    /// Execute test with a timeout
    /// </summary>
    /// <param name="arguments">The arguments to be passed.</param>
    /// <returns>The result of execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private TestResult ExecuteInternalWithTimeout(object[] arguments)
    {
        Debug.Assert(IsTimeoutSet, "Timeout should be set");

        TestResult result = null;
        Exception failure = null;
        // This is used to synchronize the timeout thread and the test thread. In case of cancellation or timeout,
        // we will exit the call to ThreadOperations.Execute but we want to make sure that the test thread has exited.
        // This allows us to ensure that the required cleanup is done (in respect to cleanup timeout).
        ManualResetEvent testRunCompletion = new(false);
        CancellationToken testRunCancellationToken = TestMethodOptions.TestContext.Context.CancellationTokenSource.Token;
        CancellationTokenSource cleanupTokenSource = TestMethodOptions.TestContext.Context.CleanupCancellationTokenSource;

        if (PlatformServiceProvider.Instance.ThreadOperations.Execute(ExecuteAsyncAction, TestMethodOptions.TimeoutContext.Timeout, testRunCancellationToken))
        {
            if (failure != null)
            {
                throw failure;
            }

            return result;
        }
        else
        {
            // Timed out or canceled
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName);
            if (TestMethodOptions.TestContext.Context.CancellationTokenSource.IsCancellationRequested)
            {
                errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName);
            }
            else
            {
                // Cancel the token source as test has timed out
                TestMethodOptions.TestContext.Context.CancellationTokenSource.Cancel();
            }

            // Test execution was canceled so we can trigger the "grace period" for cleanup.
            cleanupTokenSource.CancelAfter(TestMethodOptions.TimeoutContext.CleanupTimeout);

            // We don't need to pass a timeout to this call to WaitOne because we know that implementation of ExecuteInternal
            // guarantees that we will exit in a timely fashion.
            testRunCompletion.WaitOne();

            TestResult timeoutResult = new() { Outcome = UTF.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(ObjectModelUnitTestOutcome.Timeout, errorMessage) };
            return timeoutResult;
        }

        // Local functions
        void ExecuteAsyncAction()
        {
            try
            {
                result = ExecuteInternal(arguments, testRunCancellationToken, cleanupTokenSource.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            testRunCompletion.Set();
        }
    }

    private static class EmptyHolder<T>
    {
        internal static readonly T[] Array = System.Array.Empty<T>();
    }
}
