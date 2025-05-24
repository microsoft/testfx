// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFUnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestMethod Info object.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TestMethodInfo : ITestMethod
{
    /// <summary>
    /// Specifies the timeout when it is not set in a test case.
    /// </summary>
    public const int TimeoutWhenNotSet = 0;

    private object? _classInstance;
    private bool _isTestContextSet;
    private bool _isTestCleanupInvoked;

    internal TestMethodInfo(
        MethodInfo testMethod,
        TestClassInfo parent,
        ITestContext testContext)
    {
        DebugEx.Assert(testMethod != null, "TestMethod should not be null");
        DebugEx.Assert(parent != null, "Parent should not be null");

        TestMethod = testMethod;
        Parent = parent;
        TestContext = testContext;
        ExpectedException = ResolveExpectedException();
        RetryAttribute = GetRetryAttribute();
        TimeoutInfo = GetTestTimeout();
        Executor = GetTestMethodAttribute();
    }

    internal TimeoutInfo TimeoutInfo { get; /*For testing only*/set; }

    internal TestMethodAttribute Executor { get; /*For testing only*/set; }

    internal ITestContext TestContext { get; }

    /// <summary>
    /// Gets a value indicating whether timeout is set.
    /// </summary>
    public bool IsTimeoutSet => TimeoutInfo.Timeout != TimeoutWhenNotSet;

    /// <summary>
    /// Gets the reason why the test is not runnable.
    /// </summary>
    public string? NotRunnableReason { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether test is runnable.
    /// </summary>
    public bool IsRunnable => StringEx.IsNullOrEmpty(NotRunnableReason);

    /// <summary>
    /// Gets the parameter types of the test method.
    /// </summary>
    public ParameterInfo[] ParameterTypes => TestMethod.GetParameters();

    /// <summary>
    /// Gets the return type of the test method.
    /// </summary>
    public Type ReturnType => TestMethod.ReturnType;

    /// <summary>
    /// Gets the name of the class declaring the test method.
    /// </summary>
    public string TestClassName => Parent.ClassType.FullName!;

    /// <summary>
    /// Gets the name of the test method.
    /// </summary>
    public string TestMethodName => TestMethod.Name;

    /// <summary>
    /// Gets the methodInfo for test method.
    /// </summary>
    public MethodInfo MethodInfo => TestMethod;

    /// <summary>
    /// Gets the arguments with which test method is invoked.
    /// </summary>
    public object?[]? Arguments { get; private set; }

    /// <summary>
    /// Gets testMethod referred by this object.
    /// </summary>
    internal MethodInfo TestMethod { get; }

    /// <summary>
    /// Gets the parent class Info object.
    /// </summary>
    internal TestClassInfo Parent { get; }

    internal ExpectedExceptionBaseAttribute? ExpectedException { get; set; /*set for testing only*/ }

    internal RetryBaseAttribute? RetryAttribute { get; }

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <param name="inherit">Whether or not getting the attributes that are inherited.</param>
    /// <returns>An array of the attributes.</returns>
    public Attribute[]? GetAllAttributes(bool inherit)
        => ReflectHelper.Instance.GetDerivedAttributes<Attribute>(TestMethod, inherit).ToArray();

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <typeparam name="TAttributeType">The type of the attributes.</typeparam>
    /// <param name="inherit">Whether or not getting the attributes that are inherited.</param>
    /// <returns>An array of the attributes.</returns>
    public TAttributeType[] GetAttributes<TAttributeType>(bool inherit)
        where TAttributeType : Attribute
        => ReflectHelper.Instance.GetDerivedAttributes<TAttributeType>(TestMethod, inherit).ToArray();

    /// <inheritdoc cref="InvokeAsync(object[])" />
    public virtual TestResult Invoke(object?[]? arguments)
        => InvokeAsync(arguments).GetAwaiter().GetResult();

    /// <summary>
    /// Execute test method. Capture failures, handle async and return result.
    /// </summary>
    /// <param name="arguments">
    ///  Arguments to pass to test method. (E.g. For data driven).
    /// </param>
    /// <returns>Result of test method invocation.</returns>
    public virtual async Task<TestResult> InvokeAsync(object?[]? arguments)
    {
        Stopwatch watch = new();
        TestResult? result = null;

        // check if arguments are set for data driven tests
        arguments ??= Arguments;

        LogMessageListener? listener = null;
        watch.Start();

        ExecutionContext? executionContext = Parent.ExecutionContext ?? Parent.Parent.ExecutionContext;

        var tcs = new TaskCompletionSource<object?>();

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        ExecutionContextHelpers.RunOnContext(executionContext, async () =>
        {
            try
            {
                ThreadSafeStringWriter.CleanState();
                listener = new LogMessageListener(MSTestSettings.CurrentSettings.CaptureDebugTraces);

                result = IsTimeoutSet
                    ? await ExecuteInternalWithTimeoutAsync(arguments)
                    : await ExecuteInternalAsync(arguments, null);
                tcs.SetResult(null);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            finally
            {
                // Handle logs & debug traces.
                watch.Stop();

                if (result != null)
                {
                    result.Duration = watch.Elapsed;
                    if (listener is not null)
                    {
                        result.DebugTrace = listener.GetAndClearDebugTrace();
                        result.LogOutput = listener.GetAndClearStandardOutput();
                        result.LogError = listener.GetAndClearStandardError();
                        result.TestContextMessages = TestContext?.GetAndClearDiagnosticMessages();
                        result.ResultFiles = TestContext?.GetResultFiles();
                        listener.Dispose();
                    }
                }
            }
        });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        await tcs.Task;
        return result!;
    }

    internal void SetArguments(object?[]? arguments) => Arguments = arguments == null ? null : ResolveArguments(arguments);

    internal object?[] ResolveArguments(object?[] arguments)
    {
        ParameterInfo[] parametersInfo = TestMethod.GetParameters();
        int requiredParameterCount = 0;
        bool hasParamsValue = false;
        object? paramsValues = null;
        foreach (ParameterInfo parameter in parametersInfo)
        {
            // If this is a params array parameter, create an instance to
            // populate with any extra values provided. Don't increment
            // required parameter count - params arguments are not actually required
            if (parameter.GetCustomAttribute<ParamArrayAttribute>() != null)
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
        if (requiredParameterCount == parametersInfo.Length ||
            arguments.Length < requiredParameterCount ||
            (!hasParamsValue && arguments.Length > parametersInfo.Length))
        {
            return arguments;
        }

        object?[] newParameters = new object[parametersInfo.Length];
        for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            // We have reached the end of the regular parameters and any additional
            // values will go in a params array
            if (argumentIndex >= parametersInfo.Length - 1 && hasParamsValue)
            {
                // If this is the params parameter, instantiate a new object of that type
                if (argumentIndex == parametersInfo.Length - 1)
                {
                    paramsValues = PlatformServiceProvider.Instance.ReflectionOperations.CreateInstance(parametersInfo[argumentIndex].ParameterType, [arguments.Length - argumentIndex]);
                    newParameters[argumentIndex] = paramsValues;
                }

                // The params parameters is an array but the type is not known
                // set the values as a generic array
                if (paramsValues is Array paramsArray)
                {
                    paramsArray.SetValue(arguments[argumentIndex], argumentIndex - (parametersInfo.Length - 1));
                }
            }
            else
            {
                newParameters[argumentIndex] = arguments[argumentIndex];
            }
        }

        // If arguments supplied are less than total possible arguments set
        // the values supplied to the default values for those parameters
        for (int parameterNotProvidedIndex = arguments.Length; parameterNotProvidedIndex < parametersInfo.Length; parameterNotProvidedIndex++)
        {
            // If this is the params parameters, set it to an empty
            // array of that type as DefaultValue is DBNull
            newParameters[parameterNotProvidedIndex] = hasParamsValue && parameterNotProvidedIndex == parametersInfo.Length - 1
                ? PlatformServiceProvider.Instance.ReflectionOperations.CreateInstance(parametersInfo[parameterNotProvidedIndex].ParameterType, [0])
                : parametersInfo[parameterNotProvidedIndex].DefaultValue;
        }

        return newParameters;
    }

    /// <summary>
    /// Gets the test timeout for the test method.
    /// </summary>
    /// <returns> The timeout value if defined in milliseconds. 0 if not defined. </returns>
    private TimeoutInfo GetTestTimeout()
    {
        DebugEx.Assert(TestMethod != null, "TestMethod should be non-null");
        TimeoutAttribute? timeoutAttribute = ReflectHelper.Instance.GetFirstNonDerivedAttributeOrDefault<TimeoutAttribute>(TestMethod, inherit: false);
        if (timeoutAttribute is null)
        {
            return TimeoutInfo.FromTestTimeoutSettings();
        }

        if (!timeoutAttribute.HasCorrectTimeout)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, TestMethod.DeclaringType!.FullName, TestMethod.Name);
            throw new TypeInspectionException(message);
        }

        return TimeoutInfo.FromTimeoutAttribute(timeoutAttribute);
    }

    /// <summary>
    /// Provides the Test Method Extension Attribute of the TestClass.
    /// </summary>
    /// <returns>Test Method Attribute.</returns>
    private TestMethodAttribute GetTestMethodAttribute()
    {
        // Get the derived TestMethod attribute from reflection.
        // It should be non-null as it was already validated by IsValidTestMethod.
        TestMethodAttribute testMethodAttribute = ReflectHelper.Instance.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(TestMethod, inherit: false)!;

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        return Parent.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;
    }

    /// <summary>
    /// Resolves the expected exception attribute. The function will try to
    /// get all the expected exception attributes defined for a testMethod.
    /// </summary>
    /// <returns>
    /// The expected exception attribute found for this test. Null if not found.
    /// </returns>
    private ExpectedExceptionBaseAttribute? ResolveExpectedException()
    {
        IEnumerable<ExpectedExceptionBaseAttribute> expectedExceptions;

        try
        {
            expectedExceptions = ReflectHelper.Instance.GetDerivedAttributes<ExpectedExceptionBaseAttribute>(TestMethod, inherit: true);
        }
        catch (Exception ex)
        {
            // If construction of the attribute throws an exception, indicate that there was an
            // error when trying to run the test
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ExpectedExceptionAttributeConstructionException,
                Parent.ClassType.FullName,
                TestMethod.Name,
                ex.GetFormattedExceptionMessage());
            throw new TypeInspectionException(errorMessage);
        }

        // Verify that there is only one attribute (multiple attributes derived from
        // ExpectedExceptionBaseAttribute are not allowed on a test method)
        // This is needed EVEN IF the attribute doesn't allow multiple.
        // See https://github.com/microsoft/testfx/issues/4331
        if (expectedExceptions.Count() > 1)
        {
            ThrowMultipleAttributesException(nameof(ExpectedExceptionBaseAttribute));
        }

        return expectedExceptions.FirstOrDefault();
    }

    /// <summary>
    /// Gets the number of retries this test method should make in case of failure.
    /// </summary>
    /// <returns>
    /// The number of retries, which is always greater than or equal to 1.
    /// If RetryAttribute is not present, returns 1.
    /// </returns>
    private RetryBaseAttribute? GetRetryAttribute()
    {
        IEnumerable<RetryBaseAttribute> attributes = ReflectHelper.Instance.GetDerivedAttributes<RetryBaseAttribute>(TestMethod, inherit: true);
        using IEnumerator<RetryBaseAttribute> enumerator = attributes.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        RetryBaseAttribute attribute = enumerator.Current;

        if (enumerator.MoveNext())
        {
            ThrowMultipleAttributesException(nameof(RetryBaseAttribute));
        }

        return attribute;
    }

    [DoesNotReturn]
    private void ThrowMultipleAttributesException(string attributeName)
    {
        // Note: even if the given attribute has AllowMultiple = false, we can
        // still reach here if a derived attribute authored by the user re-defines AttributeUsage
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_MultipleAttributesOnTestMethod,
            Parent.ClassType.FullName,
            TestMethod.Name,
            attributeName);
        throw new TypeInspectionException(errorMessage);
    }

    /// <summary>
    /// Execute test without timeout.
    /// </summary>
    /// <param name="arguments">Arguments to be passed to the method.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    /// <returns>The result of the execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternalAsync(object?[]? arguments, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(TestMethod != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

        var result = new TestResult();

        // TODO remove dry violation with TestMethodRunner
        _classInstance = CreateTestClassInstance(result);
        bool isExceptionThrown = false;
        bool hasTestInitializePassed = false;
        Exception? testRunnerException = null;
        _isTestCleanupInvoked = false;
        ExecutionContext? executionContext = null;

        try
        {
            try
            {
                if (_classInstance != null && SetTestContext(_classInstance, result))
                {
                    // For any failure after this point, we must run TestCleanup
                    _isTestContextSet = true;

                    if (RunTestInitializeMethod(_classInstance, result, ref executionContext, timeoutTokenSource))
                    {
                        hasTestInitializePassed = true;

                        if (executionContext is null)
                        {
                            object? invokeResult = TestMethod.GetInvokeResult(_classInstance, arguments);
                            if (invokeResult is Task task)
                            {
                                await task;
                            }
                            else if (invokeResult is ValueTask valueTask)
                            {
                                await valueTask;
                            }
                        }
                        else
                        {
                            var tcs = new TaskCompletionSource<object?>();
                            ExecutionContext? updatedExecutionContext = executionContext;
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                            ExecutionContextHelpers.RunOnContext(executionContext, async () =>
                            {
                                try
                                {
                                    object? invokeResult = TestMethod.GetInvokeResult(_classInstance, arguments);
                                    if (invokeResult is Task task)
                                    {
                                        await task;
                                    }
                                    else if (invokeResult is ValueTask valueTask)
                                    {
                                        await valueTask;
                                    }
                                }
                                catch (Exception e)
                                {
                                    tcs.SetException(e);
                                }
                                finally
                                {
                                    updatedExecutionContext = ExecutionContext.Capture();
                                    tcs.TrySetResult(null);
                                }
                            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

                            await tcs.Task;

                            if (updatedExecutionContext is not null)
                            {
                                executionContext = updatedExecutionContext;
                            }
                        }

                        result.Outcome = UTF.UnitTestOutcome.Passed;
                    }
                }
            }
            catch (Exception ex)
            {
                isExceptionThrown = true;
                Exception realException = GetRealException(ex);

                if (IsExpectedException(realException, result))
                {
                    // Expected Exception was thrown, so Pass the test
                    result.Outcome = UTF.UnitTestOutcome.Passed;
                }
                else if (realException.IsOperationCanceledExceptionFromToken(TestContext!.Context.CancellationTokenSource.Token))
                {
                    result.Outcome = UTF.UnitTestOutcome.Timeout;
                    result.TestFailureException = new TestFailedException(
                        UTFUnitTestOutcome.Timeout,
                        timeoutTokenSource?.Token.IsCancellationRequested == true
                            ? string.Format(
                                CultureInfo.InvariantCulture,
                                Resource.Execution_Test_Timeout,
                                TestMethodName,
                                TimeoutInfo.Timeout)
                            : string.Format(
                                CultureInfo.InvariantCulture,
                                Resource.Execution_Test_Cancelled,
                                TestMethodName));
                }
                else
                {
                    // This block should not throw. If it needs to throw, then handling of
                    // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                    result.TestFailureException ??= HandleMethodException(ex, realException, TestClassName, TestMethodName);
                }

                if (result.Outcome != UTF.UnitTestOutcome.Passed)
                {
                    result.Outcome = ex is AssertInconclusiveException || ex.InnerException is AssertInconclusiveException
                        ? UTF.UnitTestOutcome.Inconclusive
                        : UTF.UnitTestOutcome.Failed;
                }
            }

            // if we get here, the test method did not throw the exception
            // if the user specified that the test was going to throw an exception, and
            // it did not, we should fail the test
            // We only perform this check if the test initialize passes and the test method is actually run.
            if (hasTestInitializePassed && !isExceptionThrown && ExpectedException is { } expectedException)
            {
                result.TestFailureException = new TestFailedException(
                    UTFUnitTestOutcome.Failed,
                    expectedException.NoExceptionMessage);
                result.Outcome = UTF.UnitTestOutcome.Failed;
            }
        }
        catch (Exception exception)
        {
            testRunnerException = exception;
        }

        // Update TestContext with outcome and exception so it can be used in the cleanup logic.
        if (TestContext is { } testContext)
        {
            testContext.SetOutcome(result.Outcome);
            // Uwnrap the exception if it's a TestFailedException
            Exception? realException = result.TestFailureException is TestFailedException
                ? result.TestFailureException.InnerException
                : result.TestFailureException;
            testContext.SetException(realException);
        }

        // TestCleanup can potentially be a long running operation which shouldn't ideally be in a finally block.
        // Pulling it out so extension writers can abort custom cleanups if need be. Having this in a finally block
        // does not allow a thread abort exception to be raised within the block but throws one after finally is executed
        // crashing the process. This was blocking writing an extension for Dynamic Timeout in VSO.
        RunTestCleanupMethod(result, executionContext, timeoutTokenSource);

        return testRunnerException != null ? throw testRunnerException : result;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private bool IsExpectedException(Exception ex, TestResult result)
    {
        // if the user specified an expected exception, we need to check if this
        // exception was thrown. If it was thrown, we should pass the test. In
        // case a different exception was thrown, the test is seen as failure
        if (ExpectedException == null)
        {
            return false;
        }

        Exception exceptionFromVerify;
        try
        {
            // If the expected exception attribute's Verify method returns, then it
            // considers this exception as expected, so the test passed
            ExpectedException.Verify(ex);
            return true;
        }
        catch (Exception verifyEx)
        {
            bool isTargetInvocationError = verifyEx is TargetInvocationException;
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
            exceptionFromVerify is AssertInconclusiveException
                ? UTFUnitTestOutcome.Inconclusive
                : UTFUnitTestOutcome.Failed,
            exceptionFromVerify.TryGetMessage(),
            ex.TryGetStackTraceInformation());
        return false;
    }

    private static Exception GetRealException(Exception ex)
    {
        if (ex is TargetInvocationException)
        {
            DebugEx.Assert(ex.InnerException != null, "Inner exception of TargetInvocationException is null. This should occur because we should have caught this case above.");

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
    /// be expected or not expected.
    /// </summary>
    /// <param name="ex">Exception that was thrown.</param>
    /// <param name="realException">Real exception thrown by the test method.</param>
    /// <param name="className">The class name.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>Test framework exception with details.</returns>
    private static TestFailedException HandleMethodException(Exception ex, Exception realException, string className, string methodName)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        string errorMessage;
        if (ex is TargetInvocationException && ex.InnerException == null)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
            return new TestFailedException(UTFUnitTestOutcome.Error, errorMessage);
        }

        if (ex is TestFailedException testFailedException)
        {
            return testFailedException;
        }

        // If we are in hot reload context and the exception is a MissingMethodException and the first line of the stack
        // trace contains the method name then it's likely that the current method was removed and the test is failing.
        // For cases where the content of the test would throw a MissingMethodException, the first line of the stack trace
        // would not be the test method name, so we can safely assume this is a proper test failure.
        if (ex is MissingMethodException missingMethodException
            && RuntimeContext.IsHotReloadEnabled
            && missingMethodException.StackTrace?.IndexOf(Environment.NewLine, StringComparison.Ordinal) is { } lineReturnIndex
            && lineReturnIndex >= 0
#pragma warning disable IDE0057 // Use range operator
            && missingMethodException.StackTrace.Substring(0, lineReturnIndex).Contains($"{className}.{methodName}"))
#pragma warning restore IDE0057 // Use range operator
        {
            return new TestFailedException(UTFUnitTestOutcome.NotFound, missingMethodException.Message, missingMethodException);
        }

        // Get the real exception thrown by the test method
        if (realException.TryGetUnitTestAssertException(out UTF.UnitTestOutcome outcome, out string? exceptionMessage, out StackTraceInformation? exceptionStackTraceInfo))
        {
            return new TestFailedException(outcome, exceptionMessage, exceptionStackTraceInfo, realException);
        }

        errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestMethodThrows,
            className,
            methodName,
            realException.GetFormattedExceptionMessage());

        // Handle special case of UI objects in TestMethod to suggest UITestMethod
        if (realException.HResult == -2147417842)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_WrongThread, errorMessage);
        }

        StackTraceInformation? stackTrace = null;

        // For ThreadAbortException (that can be thrown only by aborting a thread as there's no public constructor)
        // there's no inner exception and exception itself contains reflection-related stack trace
        // (_RuntimeMethodHandle.InvokeMethodFast <- _RuntimeMethodHandle.Invoke <- UnitTestExecuter.RunTestMethod)
        // which has no meaningful info for the user. Thus, we do not show call stack for ThreadAbortException.
        if (realException.GetType().Name != "ThreadAbortException")
        {
            stackTrace = realException.GetStackTraceInformation();
        }

        return new TestFailedException(UTFUnitTestOutcome.Failed, errorMessage, stackTrace, realException);
    }

    /// <summary>
    /// Runs TestCleanup methods of parent TestClass and base classes.
    /// </summary>
    /// <param name="result">Instance of TestResult.</param>
    /// <param name="executionContext">The execution context to run on.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private void RunTestCleanupMethod(TestResult result, ExecutionContext? executionContext, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(result != null, "result != null");

        if (_classInstance is null || !_isTestContextSet || _isTestCleanupInvoked)
        {
            return;
        }

        _isTestCleanupInvoked = true;
        MethodInfo? testCleanupMethod = Parent.TestCleanupMethod;
        Exception? testCleanupException;
        try
        {
            try
            {
                // Reset the cancellation token source to avoid cancellation of cleanup methods because of the init or test method cancellation.
                TestContext!.Context.CancellationTokenSource = new CancellationTokenSource();

                // If we are running with a method timeout, we need to cancel the cleanup when the overall timeout expires. If it already expired, nothing to do.
                if (timeoutTokenSource is { IsCancellationRequested: false })
                {
                    timeoutTokenSource?.Token.Register(TestContext.Context.CancellationTokenSource.Cancel);
                }

                // Test cleanups are called in the order of discovery
                // Current TestClass -> Parent -> Grandparent
                testCleanupException = testCleanupMethod is not null
                    ? InvokeCleanupMethod(testCleanupMethod, _classInstance, ref executionContext, timeoutTokenSource)
                    : null;
                var baseTestCleanupQueue = new Queue<MethodInfo>(Parent.BaseTestCleanupMethodsQueue);
                while (baseTestCleanupQueue.Count > 0 && testCleanupException is null)
                {
                    testCleanupMethod = baseTestCleanupQueue.Dequeue();
                    testCleanupException = InvokeCleanupMethod(testCleanupMethod, _classInstance, ref executionContext, timeoutTokenSource);
                }
            }
            finally
            {
#if NET6_0_OR_GREATER
                if (_classInstance is IAsyncDisposable classInstanceAsAsyncDisposable)
                {
                    // If you implement IAsyncDisposable without calling the DisposeAsync this would result a resource leak.
                    classInstanceAsAsyncDisposable.DisposeAsync().AsTask().Wait();
                }
#endif
                if (_classInstance is IDisposable classInstanceAsDisposable)
                {
                    classInstanceAsDisposable.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            testCleanupException = ex;
        }

        // If testCleanup was successful, then don't do anything
        if (testCleanupException == null)
        {
            return;
        }

        Exception realException = testCleanupException.GetRealException();
        UTF.UnitTestOutcome outcomeFromRealException = realException is AssertInconclusiveException ? UTF.UnitTestOutcome.Inconclusive : UTF.UnitTestOutcome.Failed;
        result.Outcome = result.Outcome.GetMoreImportantOutcome(outcomeFromRealException);

        realException = testCleanupMethod != null
            ? new TestFailedException(
                outcomeFromRealException,
                string.Format(CultureInfo.CurrentCulture, Resource.UTA_CleanupMethodThrows, TestClassName, testCleanupMethod.Name, realException.GetFormattedExceptionMessage()),
                realException.TryGetStackTraceInformation(),
                realException)
            : new TestFailedException(
                outcomeFromRealException,
                string.Format(CultureInfo.CurrentCulture, Resource.UTA_CleanupMethodThrowsGeneralError, TestClassName, realException.GetFormattedExceptionMessage()),
                realException.TryGetStackTraceInformation(),
                realException);

        result.TestFailureException = realException;
    }

    /// <summary>
    /// Runs TestInitialize methods of parent TestClass and the base classes.
    /// </summary>
    /// <param name="classInstance">Instance of TestClass.</param>
    /// <param name="result">Instance of TestResult.</param>
    /// <param name="executionContext">The execution context to run on.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private bool RunTestInitializeMethod(object classInstance, TestResult result, ref ExecutionContext? executionContext, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(classInstance != null, "classInstance != null");
        DebugEx.Assert(result != null, "result != null");

        MethodInfo? testInitializeMethod = null;
        Exception? testInitializeException = null;

        try
        {
            // TestInitialize methods for base classes are called in reverse order of discovery
            // Grandparent -> Parent -> Child TestClass
            var baseTestInitializeStack = new Stack<MethodInfo>(Parent.BaseTestInitializeMethodsQueue);
            while (baseTestInitializeStack.Count > 0)
            {
                testInitializeMethod = baseTestInitializeStack.Pop();
                testInitializeException = testInitializeMethod is not null
                    ? InvokeInitializeMethod(testInitializeMethod, classInstance, ref executionContext, timeoutTokenSource)
                    : null;
                if (testInitializeException is not null)
                {
                    break;
                }
            }

            if (testInitializeException == null)
            {
                testInitializeMethod = Parent.TestInitializeMethod;
                testInitializeException = testInitializeMethod is not null
                    ? InvokeInitializeMethod(testInitializeMethod, classInstance, ref executionContext, timeoutTokenSource)
                    : null;
            }
        }
        catch (Exception ex)
        {
            testInitializeException = ex;
        }

        // If testInitialization was successful, then don't do anything
        if (testInitializeException == null)
        {
            return true;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (testInitializeException is TestFailedException tfe)
        {
            result.Outcome = tfe.Outcome;
            result.TestFailureException = testInitializeException;
            return false;
        }

        Exception realException = testInitializeException.GetRealException();

        // Prefix the exception message with the exception type name as prefix when exception is not assert exception.
        string exceptionMessage = realException is UnitTestAssertException
            ? realException.TryGetMessage()
            : realException.GetFormattedExceptionMessage();
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_InitMethodThrows,
            TestClassName,
            testInitializeMethod?.Name,
            exceptionMessage);
        StackTraceInformation? stackTrace = realException.GetStackTraceInformation();

        result.Outcome = realException is AssertInconclusiveException
            ? UTF.UnitTestOutcome.Inconclusive
            : UTF.UnitTestOutcome.Failed;
        result.TestFailureException = new TestFailedException(
            result.Outcome,
            errorMessage,
            stackTrace,
            realException);

        return false;
    }

    private TestFailedException? InvokeInitializeMethod(MethodInfo methodInfo, object classInstance, ref ExecutionContext? executionContext, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestInitializeMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        int originalThreadId = Environment.CurrentManagedThreadId;
        ExecutionContext? updatedExecutionContext = null;
        TestFailedException? result = FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () =>
            {
                methodInfo.InvokeAsSynchronousTask(classInstance, null);
                if (originalThreadId != Environment.CurrentManagedThreadId)
                {
                    // We ended up running on a different thread, because of use of non-cooperative timeout.
                    // Re-capture the execution context.
                    updatedExecutionContext = ExecutionContext.Capture();
                }
            },
            TestContext!.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            executionContext,
            Resource.TestInitializeWasCancelled,
            Resource.TestInitializeTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout));

        if (updatedExecutionContext != null)
        {
            executionContext = updatedExecutionContext;
        }

        return result;
    }

    private TestFailedException? InvokeCleanupMethod(MethodInfo methodInfo, object classInstance, ref ExecutionContext? executionContext, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        int originalThreadId = Environment.CurrentManagedThreadId;
        ExecutionContext? updatedExecutionContext = null;
        TestFailedException? result = FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () =>
            {
                methodInfo.InvokeAsSynchronousTask(classInstance, null);
                if (originalThreadId != Environment.CurrentManagedThreadId)
                {
                    // We ended up running on a different thread, because of use of non-cooperative timeout.
                    // Re-capture the execution context.
                    updatedExecutionContext = ExecutionContext.Capture();
                }
            },
            TestContext!.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            executionContext,
            Resource.TestCleanupWasCancelled,
            Resource.TestCleanupTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout));

        if (updatedExecutionContext != null)
        {
            executionContext = updatedExecutionContext;
        }

        return result;
    }

    /// <summary>
    /// Sets the <see cref="TestContext"/> on <paramref name="classInstance"/>.
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
        DebugEx.Assert(classInstance != null, "classInstance != null");
        DebugEx.Assert(result != null, "result != null");

        try
        {
            if (Parent.TestContextProperty != null && Parent.TestContextProperty.CanWrite)
            {
                Parent.TestContextProperty.SetValue(classInstance, TestContext);
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.GetRealException();
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestContextSetError,
                TestClassName,
                realException.GetFormattedExceptionMessage());

            result.Outcome = UTF.UnitTestOutcome.Failed;
            StackTraceInformation? stackTraceInfo = realException.GetStackTraceInformation();
            result.TestFailureException = new TestFailedException(UTFUnitTestOutcome.Failed, errorMessage, stackTraceInfo);
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
    private object? CreateTestClassInstance(TestResult result)
    {
        object? classInstance = null;
        try
        {
            classInstance = Parent.Constructor.Invoke(Parent.IsParameterlessConstructor ? null : [TestContext]);
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
            Exception realException = ex.GetRealException();

            if (realException.IsOperationCanceledExceptionFromToken(TestContext!.Context.CancellationTokenSource.Token))
            {
                result.Outcome = UTF.UnitTestOutcome.Timeout;
                result.TestFailureException = new TestFailedException(UTFUnitTestOutcome.Timeout, string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout));
            }
            else
            {
                string exceptionMessage = realException.GetFormattedExceptionMessage();
                StackTraceInformation? stackTraceInfo = realException.GetStackTraceInformation();

                string errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_InstanceCreationError,
                    TestClassName,
                    exceptionMessage);

                result.Outcome = UTF.UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UTFUnitTestOutcome.Failed, errorMessage, stackTraceInfo);
            }
        }

        return classInstance;
    }

    /// <summary>
    /// Execute test with a timeout.
    /// </summary>
    /// <param name="arguments">The arguments to be passed.</param>
    /// <returns>The result of execution.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task<TestResult> ExecuteInternalWithTimeoutAsync(object?[]? arguments)
    {
        DebugEx.Assert(IsTimeoutSet, "Timeout should be set");

        if (TimeoutInfo.CooperativeCancellation)
        {
            CancellationTokenSource? timeoutTokenSource = null;
            try
            {
                timeoutTokenSource = new(TimeoutInfo.Timeout);
                timeoutTokenSource.Token.Register(TestContext!.Context.CancellationTokenSource.Cancel);
                if (timeoutTokenSource.Token.IsCancellationRequested)
                {
                    return new()
                    {
                        Outcome = UTF.UnitTestOutcome.Timeout,
                        TestFailureException = new TestFailedException(
                            UTFUnitTestOutcome.Timeout,
                            string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)),
                    };
                }

                try
                {
                    return await ExecuteInternalAsync(arguments, timeoutTokenSource);
                }
                catch (OperationCanceledException)
                {
                    // Ideally we would like to check that the token of the exception matches cancellationTokenSource but TestContext
                    // instances are not well defined so we have to handle the exception entirely.
                    return new()
                    {
                        Outcome = UTF.UnitTestOutcome.Timeout,
                        TestFailureException = new TestFailedException(
                            UTFUnitTestOutcome.Timeout,
                            timeoutTokenSource.Token.IsCancellationRequested
                                ? string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout)
                                : string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName)),
                    };
                }
            }
            finally
            {
                timeoutTokenSource?.Dispose();
                timeoutTokenSource = null;
            }
        }

        TestResult? result = null;
        Exception? failure = null;
        ExecutionContext? executionContext = null;

        if (PlatformServiceProvider.Instance.ThreadOperations.Execute(ExecuteAsyncAction, TimeoutInfo.Timeout, TestContext!.Context.CancellationTokenSource.Token))
        {
            if (failure != null)
            {
                throw failure;
            }

            DebugEx.Assert(result is not null, "result is not null");

            // It's possible that some failures happened and that the cleanup wasn't executed, so we need to run it here.
            // The method already checks if the cleanup was already executed.
            RunTestCleanupMethod(result, executionContext, null);
            return result;
        }

        // Timed out or canceled
        string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, TimeoutInfo.Timeout);
        if (TestContext.Context.CancellationTokenSource.IsCancellationRequested)
        {
            errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName);
        }
        else
        {
            // Cancel the token source as test has timed out
            await TestContext.Context.CancellationTokenSource.CancelAsync();
        }

        TestResult timeoutResult = new() { Outcome = UTF.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UTFUnitTestOutcome.Timeout, errorMessage) };

        // TODO: execution context propagation here may still not be accurate.
        // if test init was successfully executed by ExecuteAsyncAction, but then the test itself timed out or cancelled,
        // then at this point we will run the cleanup on an execution context that doesn't have any state set by the test initialize.

        // We don't know when the cancellation happened so it's possible that the cleanup wasn't executed, so we need to run it here.
        // The method already checks if the cleanup was already executed.
        RunTestCleanupMethod(timeoutResult, executionContext, null);
        return timeoutResult;

        // Local functions
        void ExecuteAsyncAction()
        {
            try
            {
                // TODO: Avoid blocking.
                // This used to always happen, but now is moved to the code path where there is a Timeout on the test method.
                // The GetAwaiter().GetResult() call here can be a source of deadlocks, especially for UWP/WinUI.
                // When the test method has `await`s with ConfigureAwait(true) (which is the default), the continuation is
                // dispatched back to the SynchronizationContext which offloads the work to the UI thread.
                // However, the GetAwaiter().GetResult() here will block the current thread which is also the UI thread.
                // So, the continuations will not be able, thus this task never completes.
                result = ExecuteInternalAsync(arguments, null).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }
    }
}
