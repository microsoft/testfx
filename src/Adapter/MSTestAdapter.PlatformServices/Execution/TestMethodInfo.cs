// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFUnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestMethod Info object.
/// </summary>
#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal class TestMethodInfo : ITestMethod
{
    /// <summary>
    /// Specifies the timeout when it is not set in a test case.
    /// </summary>
    public const int TimeoutWhenNotSet = 0;

    private object? _classInstance;
    private bool _isTestContextSet;
    private bool _isTestCleanupInvoked;

    private ExecutionContext? _executionContext;

#if NETFRAMEWORK
    private object? _hostContext;
#endif

    internal TestMethodInfo(
        MethodInfo testMethod,
        TestClassInfo parent,
        ITestContext testContext)
    {
        DebugEx.Assert(testMethod != null, "TestMethod should not be null");
        DebugEx.Assert(parent != null, "Parent should not be null");

        MethodInfo = testMethod;
        Parent = parent;
        TestContext = testContext;
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
    /// Gets or sets the reason why the test is not runnable.
    /// </summary>
    public string? NotRunnableReason { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether test is runnable.
    /// </summary>
    public bool IsRunnable => StringEx.IsNullOrEmpty(NotRunnableReason);

    /// <summary>
    /// Gets the parameter types of the test method.
    /// </summary>
    public ParameterInfo[] ParameterTypes => MethodInfo.GetParameters();

    /// <summary>
    /// Gets the return type of the test method.
    /// </summary>
    public Type ReturnType => MethodInfo.ReturnType;

    /// <summary>
    /// Gets the name of the class declaring the test method.
    /// </summary>
    public string TestClassName => Parent.ClassType.FullName!;

    /// <summary>
    /// Gets the name of the test method.
    /// </summary>
    public string TestMethodName => MethodInfo.Name;

    /// <summary>
    /// Gets the methodInfo for test method.
    /// </summary>
    public MethodInfo MethodInfo { get; }

    /// <summary>
    /// Gets the arguments with which test method is invoked.
    /// </summary>
    public object?[]? Arguments { get; private set; }

    /// <summary>
    /// Gets the parent class Info object.
    /// </summary>
    internal TestClassInfo Parent { get; }

    internal RetryBaseAttribute? RetryAttribute { get; }

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <returns>An array of the attributes.</returns>
    public Attribute[]? GetAllAttributes()
        => [.. ReflectHelper.Instance.GetAttributes<Attribute>(MethodInfo)];

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <typeparam name="TAttributeType">The type of the attributes.</typeparam>
    /// <returns>An array of the attributes.</returns>
    public TAttributeType[] GetAttributes<TAttributeType>()
        where TAttributeType : Attribute
        => [.. ReflectHelper.Instance.GetAttributes<TAttributeType>(MethodInfo)];

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

        watch.Start();

        try
        {
            result = IsTimeoutSet
                ? await ExecuteInternalWithTimeoutAsync(arguments).ConfigureAwait(false)
                : await ExecuteInternalAsync(arguments, null).ConfigureAwait(false);
        }
        finally
        {
            // Handle logs & debug traces.
            watch.Stop();

            if (result != null)
            {
                var testContextImpl = TestContext as TestContextImplementation;
                result.LogOutput = testContextImpl?.GetOut();
                result.LogError = testContextImpl?.GetErr();
                result.DebugTrace = testContextImpl?.GetTrace();
                result.TestContextMessages = TestContext?.GetAndClearDiagnosticMessages();
                result.ResultFiles = TestContext?.GetResultFiles();
                result.Duration = watch.Elapsed;
            }

            _executionContext?.Dispose();
            _executionContext = null;
#if NETFRAMEWORK
            _hostContext = null;
#endif
        }

        return result;
    }

    internal void SetArguments(object?[]? arguments) => Arguments = arguments == null ? null : ResolveArguments(arguments);

    internal object?[] ResolveArguments(object?[] arguments)
    {
        ParameterInfo[] parametersInfo = MethodInfo.GetParameters();
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
        DebugEx.Assert(MethodInfo != null, "TestMethod should be non-null");
        TimeoutAttribute? timeoutAttribute = ReflectHelper.Instance.GetFirstAttributeOrDefault<TimeoutAttribute>(MethodInfo);
        if (timeoutAttribute is null)
        {
            return TimeoutInfo.FromTestTimeoutSettings();
        }

        if (!timeoutAttribute.HasCorrectTimeout)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, MethodInfo.DeclaringType!.FullName, MethodInfo.Name);
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
        TestMethodAttribute testMethodAttribute = ReflectHelper.Instance.GetFirstAttributeOrDefault<TestMethodAttribute>(MethodInfo)!;

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        return Parent.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;
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
        IEnumerable<RetryBaseAttribute> attributes = ReflectHelper.Instance.GetAttributes<RetryBaseAttribute>(MethodInfo);
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
            MethodInfo.Name,
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
        DebugEx.Assert(MethodInfo != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

        var result = new TestResult();

        Exception? testRunnerException = null;
        _isTestCleanupInvoked = false;

        try
        {
            try
            {
                // We invoke global test initialize methods before creating the test class instance.
                // We consider the test class constructor as a "local" initialization.
                // We want to invoke first the global initializations, then local ones, then test method.
                // After that, we invoke local cleanups (including Dispose) and finally global cleanups at last.
                foreach ((MethodInfo method, TimeoutInfo? timeoutInfo) in Parent.Parent.GlobalTestInitializations)
                {
                    await InvokeGlobalInitializeMethodAsync(method, timeoutInfo, timeoutTokenSource).ConfigureAwait(false);
                }

                // TODO remove dry violation with TestMethodRunner
                bool setTestContextSucessful = false;
                if (_executionContext is null)
                {
                    _classInstance = CreateTestClassInstance(result);
                    setTestContextSucessful = _classInstance != null && SetTestContext(_classInstance, result);
                }
                else
                {
                    // The whole ExecuteInternalAsync method is already running on the execution context we got after class init.
                    // However, after we run global test initialize, it will need to capture the execution context (after it has finished).
                    // This is the case when executionContext is not null (this code path).
                    // In this case, we want to ensure the constructor and setting TestContext are both run on the correct execution context.
                    // Also we re-capture the execution context in case constructor or TestContext setter modifies an async local value.
                    ExecutionContextHelpers.RunOnContext(_executionContext, () =>
                    {
                        try
                        {
                            _classInstance = CreateTestClassInstance(result);
                            setTestContextSucessful = _classInstance != null && SetTestContext(_classInstance, result);
                        }
                        finally
                        {
                            _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                            _hostContext = CallContext.HostContext;
#endif
                        }
                    });
                }

                if (setTestContextSucessful)
                {
                    // For any failure after this point, we must run TestCleanup
                    _isTestContextSet = true;

                    if (await RunTestInitializeMethodAsync(_classInstance!, result, timeoutTokenSource).ConfigureAwait(false))
                    {
                        if (_executionContext is null)
                        {
                            Task? invokeResult = MethodInfo.GetInvokeResultAsync(_classInstance, arguments);
                            if (invokeResult is not null)
                            {
                                await invokeResult.ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            var tcs = new TaskCompletionSource<object?>();
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                            ExecutionContextHelpers.RunOnContext(_executionContext, async () =>
                            {
                                try
                                {
#if NETFRAMEWORK
                                    CallContext.HostContext = _hostContext;
#endif
                                    Task? invokeResult = MethodInfo.GetInvokeResultAsync(_classInstance, arguments);
                                    if (invokeResult is not null)
                                    {
                                        await invokeResult.ConfigureAwait(false);
                                    }
                                }
                                catch (Exception e)
                                {
                                    tcs.SetException(e);
                                }
                                finally
                                {
                                    _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                                    _hostContext = CallContext.HostContext;
#endif
                                    tcs.TrySetResult(null);
                                }
                            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

                            await tcs.Task.ConfigureAwait(false);
                        }

                        result.Outcome = UTF.UnitTestOutcome.Passed;
                    }
                }
            }
            catch (Exception ex)
            {
                Exception realException = GetRealException(ex);

                if (realException.IsOperationCanceledExceptionFromToken(TestContext!.Context.CancellationTokenSource.Token))
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
        await RunTestCleanupMethodAsync(result, timeoutTokenSource).ConfigureAwait(false);

        return testRunnerException != null ? throw testRunnerException : result;
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
        if (realException.TryGetUnitTestAssertException(out UTFUnitTestOutcome outcome, out string? exceptionMessage, out StackTraceInformation? exceptionStackTraceInfo))
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
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async Task RunTestCleanupMethodAsync(TestResult result, CancellationTokenSource? timeoutTokenSource)
    {
        DebugEx.Assert(result != null, "result != null");

        if (_classInstance is null || !_isTestContextSet || _isTestCleanupInvoked ||
            // Fast check to see if we can return early.
            // This avoids the code below that allocates CancellationTokenSource
            !HasCleanupsToInvoke())
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
                TestContext.Context.CancellationTokenSource = new CancellationTokenSource();

                // If we are running with a method timeout, we need to cancel the cleanup when the overall timeout expires. If it already expired, nothing to do.
                if (timeoutTokenSource is { IsCancellationRequested: false })
                {
                    timeoutTokenSource?.Token.Register(TestContext.Context.CancellationTokenSource.Cancel);
                }

                // Test cleanups are called in the order of discovery
                // Current TestClass -> Parent -> Grandparent
                testCleanupException = testCleanupMethod is not null
                    ? await InvokeCleanupMethodAsync(testCleanupMethod, _classInstance, timeoutTokenSource).ConfigureAwait(false)
                    : null;
                var baseTestCleanupQueue = new Queue<MethodInfo>(Parent.BaseTestCleanupMethodsQueue);
                while (baseTestCleanupQueue.Count > 0 && testCleanupException is null)
                {
                    testCleanupMethod = baseTestCleanupQueue.Dequeue();
                    testCleanupException = await InvokeCleanupMethodAsync(testCleanupMethod, _classInstance, timeoutTokenSource).ConfigureAwait(false);
                }
            }
            finally
            {
#if NET6_0_OR_GREATER
                if (_classInstance is IAsyncDisposable classInstanceAsAsyncDisposable)
                {
                    // If you implement IAsyncDisposable without calling the DisposeAsync this would result a resource leak.
                    await classInstanceAsAsyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
#endif
                if (_classInstance is IDisposable classInstanceAsDisposable)
                {
                    classInstanceAsDisposable.Dispose();
                }

                foreach ((MethodInfo method, TimeoutInfo? timeoutInfo) in Parent.Parent.GlobalTestCleanups)
                {
                    await InvokeGlobalCleanupMethodAsync(method, timeoutInfo, timeoutTokenSource).ConfigureAwait(false);
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
        UTFUnitTestOutcome outcomeFromRealException = realException is AssertInconclusiveException ? UTF.UnitTestOutcome.Inconclusive : UTF.UnitTestOutcome.Failed;
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

    private bool HasCleanupsToInvoke() =>
        Parent.TestCleanupMethod is not null ||
        Parent.BaseTestCleanupMethodsQueue is { Count: > 0 } ||
        _classInstance is IDisposable ||
#if NET6_0_OR_GREATER
        _classInstance is IAsyncDisposable ||
#endif
        Parent.Parent.GlobalTestCleanups is { Count: > 0 };

    /// <summary>
    /// Runs TestInitialize methods of parent TestClass and the base classes.
    /// </summary>
    /// <param name="classInstance">Instance of TestClass.</param>
    /// <param name="result">Instance of TestResult.</param>
    /// <param name="timeoutTokenSource">The timeout token source.</param>
    /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private async SynchronizationContextPreservingTask<bool> RunTestInitializeMethodAsync(object classInstance, TestResult result, CancellationTokenSource? timeoutTokenSource)
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
                    ? await InvokeInitializeMethodAsync(testInitializeMethod, classInstance, timeoutTokenSource).ConfigureAwait(false)
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
                    ? await InvokeInitializeMethodAsync(testInitializeMethod, classInstance, timeoutTokenSource).ConfigureAwait(false)
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

    private async SynchronizationContextPreservingTask<TestFailedException?> InvokeInitializeMethodAsync(MethodInfo methodInfo, object classInstance, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestInitializeMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(classInstance, null);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            _executionContext,
            Resource.TestInitializeWasCancelled,
            Resource.TestInitializeTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async Task<TestFailedException?> InvokeGlobalInitializeMethodAsync(MethodInfo methodInfo, TimeoutInfo? timeoutInfo, CancellationTokenSource? timeoutTokenSource)
    {
        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(null, [TestContext]);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeoutInfo: timeoutInfo,
            methodInfo,
            _executionContext,
            Resource.TestInitializeWasCancelled,
            Resource.TestInitializeTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async Task<TestFailedException?> InvokeCleanupMethodAsync(MethodInfo methodInfo, object classInstance, CancellationTokenSource? timeoutTokenSource)
    {
        TimeoutInfo? timeout = null;
        if (Parent.TestCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(classInstance, null);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeout,
            methodInfo,
            _executionContext,
            Resource.TestCleanupWasCancelled,
            Resource.TestCleanupTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

        return result;
    }

    private async Task<TestFailedException?> InvokeGlobalCleanupMethodAsync(MethodInfo methodInfo, TimeoutInfo? timeoutInfo, CancellationTokenSource? timeoutTokenSource)
    {
        TestFailedException? result = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            async () =>
            {
#if NETFRAMEWORK
                CallContext.HostContext = _hostContext;
#endif

                Task? task = methodInfo.GetInvokeResultAsync(null, [TestContext]);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }

                _executionContext = ExecutionContext.Capture() ?? _executionContext;
#if NETFRAMEWORK
                _hostContext = CallContext.HostContext;
#endif
            },
            TestContext.Context.CancellationTokenSource,
            timeoutInfo: timeoutInfo,
            methodInfo,
            _executionContext,
            Resource.TestCleanupWasCancelled,
            Resource.TestCleanupTimedOut,
            timeoutTokenSource is null
                ? null
                : (timeoutTokenSource, TimeoutInfo.Timeout)).ConfigureAwait(false);

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

            if (realException.IsOperationCanceledExceptionFromToken(TestContext.Context.CancellationTokenSource.Token))
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
                timeoutTokenSource.Token.Register(TestContext.Context.CancellationTokenSource.Cancel);
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
                    return await ExecuteInternalAsync(arguments, timeoutTokenSource).ConfigureAwait(false);
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

        if (PlatformServiceProvider.Instance.ThreadOperations.Execute(ExecuteAsyncAction, TimeoutInfo.Timeout, TestContext.Context.CancellationTokenSource.Token))
        {
            if (failure != null)
            {
                throw failure;
            }

            DebugEx.Assert(result is not null, "result is not null");
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
            await TestContext.Context.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }

        TestResult timeoutResult = new() { Outcome = UTF.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UTFUnitTestOutcome.Timeout, errorMessage) };

        // TODO: execution context propagation here may still not be accurate.
        // if test init was successfully executed by ExecuteAsyncAction, but then the test itself timed out or cancelled,
        // then at this point we will run the cleanup on an execution context that doesn't have any state set by the test initialize.

        // We don't know when the cancellation happened so it's possible that the cleanup wasn't executed, so we need to run it here.
        // The method already checks if the cleanup was already executed.
        await RunTestCleanupMethodAsync(timeoutResult, null).ConfigureAwait(false);
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
