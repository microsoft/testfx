// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
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

        private object[] arguments;

        internal TestMethodInfo(
            MethodInfo testMethod,
            TestClassInfo parent,
            TestMethodOptions testmethodOptions)
        {
            Debug.Assert(testMethod != null, "TestMethod should not be null");
            Debug.Assert(parent != null, "Parent should not be null");

            this.TestMethod = testMethod;
            this.Parent = parent;
            this.TestMethodOptions = testmethodOptions;
        }

        /// <summary>
        /// Gets a value indicating whether timeout is set.
        /// </summary>
        public bool IsTimeoutSet => this.TestMethodOptions.Timeout != TimeoutWhenNotSet;

        /// <summary>
        /// Gets the reason why the test is not runnable
        /// </summary>
        public string NotRunnableReason { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether test is runnnable
        /// </summary>
        public bool IsRunnable => string.IsNullOrEmpty(this.NotRunnableReason);

        public ParameterInfo[] ParameterTypes => this.TestMethod.GetParameters();

        public Type ReturnType => this.TestMethod.ReturnType;

        public string TestClassName => this.Parent.ClassType.FullName;

        public string TestMethodName => this.TestMethod.Name;

        public MethodInfo MethodInfo => this.TestMethod;

        public object[] Arguments => this.arguments;

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
            return ReflectHelper.GetCustomAttributes(this.TestMethod, inherit) as Attribute[];
        }

        public TAttributeType[] GetAttributes<TAttributeType>(bool inherit)
            where TAttributeType : Attribute
        {
            Attribute[] attributeArray = ReflectHelper.GetCustomAttributes(this.TestMethod, typeof(TAttributeType), inherit);

            TAttributeType[] tAttributeArray = attributeArray as TAttributeType[];
            if (tAttributeArray != null)
            {
                return tAttributeArray;
            }

            List<TAttributeType> tAttributeList = new List<TAttributeType>();
            if (attributeArray != null)
            {
                foreach (Attribute attribute in attributeArray)
                {
                    TAttributeType tAttribute = attribute as TAttributeType;
                    if (tAttribute != null)
                    {
                        tAttributeList.Add(tAttribute);
                    }
                }
            }

            return tAttributeList.ToArray();
        }

        /// <summary>
        /// Execute test method. Capture failures, handle async and return result.
        /// </summary>
        /// <param name="arguments">
        ///  Arguments to pass to test method. (E.g. For data driven)
        /// </param>
        /// <returns>Result of test method invocation.</returns>
        public virtual TestResult Invoke(object[] arguments)
        {
            Stopwatch watch = new Stopwatch();
            TestResult result = null;

            // check if arguments are set for data driven tests
            if (arguments == null)
            {
                arguments = this.Arguments;
            }

            using (LogMessageListener listener = new LogMessageListener(this.TestMethodOptions.CaptureDebugTraces))
            {
                watch.Start();
                try
                {
                    if (this.IsTimeoutSet)
                    {
                        result = this.ExecuteInternalWithTimeout(arguments);
                    }
                    else
                    {
                        result = this.ExecuteInternal(arguments);
                    }
                }
                finally
                {
                    // Handle logs & debug traces.
                    watch.Stop();

                    if (result != null)
                    {
                        result.Duration = watch.Elapsed;
                        result.DebugTrace = listener.DebugTrace;
                        result.LogOutput = listener.StandardOutput;
                        result.LogError = listener.StandardError;
                        result.TestContextMessages = this.TestMethodOptions.TestContext.GetAndClearDiagnosticMessages();
                        result.ResultFiles = this.TestMethodOptions.TestContext.GetResultFiles();
                    }
                }
            }

            return result;
        }

        internal void SetArguments(object[] arguments)
        {
            if (arguments == null)
            {
                this.arguments = null;
            }
            else
            {
                this.arguments = this.ResolveArguments(arguments);
            }
        }

        internal object[] ResolveArguments(object[] arguments)
        {
            ParameterInfo[] parameterInfos = this.TestMethod.GetParameters();
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
            // and it doesn't have a params paramenter don't try and resolve anything
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
        private TestResult ExecuteInternal(object[] arguments)
        {
            Debug.Assert(this.TestMethod != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

            var result = new TestResult();

            // TODO remove dry violation with TestMethodRunner
            var classInstance = this.CreateTestClassInstance(result);
            var testContextSetup = false;
            bool isExceptionThrown = false;
            bool hasTestInitializePassed = false;
            Exception testRunnerException = null;

            try
            {
                try
                {
                    if (classInstance != null && this.SetTestContext(classInstance, result))
                    {
                        // For any failure after this point, we must run TestCleanup
                        testContextSetup = true;

                        if (this.RunTestInitializeMethod(classInstance, result))
                        {
                            hasTestInitializePassed = true;
                            PlatformServiceProvider.Instance.ThreadOperations.ExecuteWithAbortSafety(
                                () => this.TestMethod.InvokeAsSynchronousTask(classInstance, arguments));
                            result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Passed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    isExceptionThrown = true;

                    if (this.IsExpectedException(ex, result))
                    {
                        // Expected Exception was thrown, so Pass the test
                        result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Passed;
                    }
                    else if (result.TestFailureException == null)
                    {
                        // This block should not throw. If it needs to throw, then handling of
                        // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                        result.TestFailureException = this.HandleMethodException(
                            ex,
                            this.TestClassName,
                            this.TestMethodName);
                    }

                    if (result.Outcome != TestTools.UnitTesting.UnitTestOutcome.Passed)
                    {
                        if (ex is UTF.AssertInconclusiveException || ex.InnerException is UTF.AssertInconclusiveException)
                        {
                            result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Inconclusive;
                        }
                        else
                        {
                            result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                        }
                    }
                }

                // if we get here, the test method did not throw the exception
                // if the user specified that the test was going to throw an exception, and
                // it did not, we should fail the test
                // We only perform this check if the test initialize passes and the test method is actually run.
                if (hasTestInitializePassed && !isExceptionThrown && this.TestMethodOptions.ExpectedException != null)
                {
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Failed,
                        this.TestMethodOptions.ExpectedException.NoExceptionMessage);
                    result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                }
            }
            catch (Exception exception)
            {
                testRunnerException = exception;
            }

            // Set the current tests outcome before cleanup so it can be used in the cleanup logic.
            this.TestMethodOptions.TestContext.SetOutcome(result.Outcome);

            // TestCleanup can potentially be a long running operation which should'nt ideally be in a finally block.
            // Pulling it out so extension writers can abort custom cleanups if need be. Having this in a finally block
            // does not allow a threadabort exception to be raised within the block but throws one after finally is executed
            // crashing the process. This was blocking writing an extension for Dynamic Timeout in VSO.
            if (classInstance != null && testContextSetup)
            {
                this.RunTestCleanupMethod(classInstance, result);
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
            Exception realException = this.GetRealException(ex);

            // if the user specified an expected exception, we need to check if this
            // exception was thrown. If it was thrown, we should pass the test. In
            // case a different exception was thrown, the test is seen as failure
            if (this.TestMethodOptions.ExpectedException != null)
            {
                Exception exceptionFromVerify;
                try
                {
                    // If the expected exception attribute's Verify method returns, then it
                    // considers this exception as expected, so the test passed
                    this.TestMethodOptions.ExpectedException.Verify(realException);
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
                    exceptionFromVerify is UTF.AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed,
                                              exceptionFromVerify.TryGetMessage(),
                                              realException.TryGetStackTraceInformation());
                return false;
            }
            else
            {
                return false;
            }
        }

        private Exception GetRealException(Exception ex)
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
        private Exception HandleMethodException(Exception ex, string className, string methodName)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            var isTargetInvocationException = ex is TargetInvocationException;
            if (isTargetInvocationException && ex.InnerException == null)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
                return new TestFailedException(UnitTestOutcome.Error, errorMessage);
            }

            // Get the real exception thrown by the test method
            Exception realException = this.GetRealException(ex);
            string exceptionMessage = null;
            StackTraceInformation exceptionStackTraceInfo = null;
            var outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;

            if (realException.TryGetUnitTestAssertException(out outcome, out exceptionMessage, out exceptionStackTraceInfo))
            {
                return new TestFailedException(outcome.ToUnitTestOutcome(), exceptionMessage, exceptionStackTraceInfo, realException);
            }
            else
            {
                string errorMessage;

                // Handle special case of UI objects in TestMethod to suggest UITestMethod
                if (realException.HResult == -2147417842)
                {
                    errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_WrongThread,
                        string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows, className, methodName, StackTraceHelper.GetExceptionMessage(realException)));
                }
                else
                {
                    errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_TestMethodThrows,
                        className,
                        methodName,
                        StackTraceHelper.GetExceptionMessage(realException));
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

                return new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace, realException);
            }
        }

        /// <summary>
        /// Runs TestCleanup methods of parent TestClass and base classes.
        /// </summary>
        /// <param name="classInstance">Instance of TestClass.</param>
        /// <param name="result">Instance of TestResult.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private void RunTestCleanupMethod(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            var testCleanupMethod = this.Parent.TestCleanupMethod;
            try
            {
                try
                {
                    // Test cleanups are called in the order of discovery
                    // Current TestClass -> Parent -> Grandparent
                    testCleanupMethod?.InvokeAsSynchronousTask(classInstance, null);
                    var baseTestCleanupQueue = new Queue<MethodInfo>(this.Parent.BaseTestCleanupMethodsQueue);
                    while (baseTestCleanupQueue.Count > 0)
                    {
                        testCleanupMethod = baseTestCleanupQueue.Dequeue();
                        testCleanupMethod?.InvokeAsSynchronousTask(classInstance, null);
                    }
                }
                finally
                {
                    (classInstance as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                var cleanupOutcome = UTF.UnitTestOutcome.Failed;
                var cleanupError = new StringBuilder();
                var cleanupStackTrace = new StringBuilder();
                StackTraceInformation cleanupStackTraceInfo = null;

                TestFailedException testFailureException = result.TestFailureException as TestFailedException;
                testFailureException.TryGetTestFailureExceptionMessageAndStackTrace(cleanupError, cleanupStackTrace);

                if (cleanupStackTrace.Length > 0)
                {
                    cleanupStackTrace.Append(Resource.UTA_CleanupStackTrace);
                    cleanupStackTrace.Append(Environment.NewLine);
                }

                Exception realException = ex.GetInnerExceptionOrDefault();
                string exceptionMessage = null;
                StackTraceInformation realExceptionStackTraceInfo = null;

                // special case UnitTestAssertException to trim off part of the stack trace
                if (!realException.TryGetUnitTestAssertException(out cleanupOutcome, out exceptionMessage, out realExceptionStackTraceInfo))
                {
                    cleanupOutcome = UTF.UnitTestOutcome.Failed;
                    exceptionMessage = this.GetTestCleanUpExceptionMessage(testCleanupMethod, realException);
                    realExceptionStackTraceInfo = realException.TryGetStackTraceInformation();
                }

                cleanupError.Append(exceptionMessage);
                if (realExceptionStackTraceInfo != null)
                {
                    cleanupStackTrace.Append(realExceptionStackTraceInfo.ErrorStackTrace);
                    cleanupStackTraceInfo = cleanupStackTraceInfo ?? realExceptionStackTraceInfo;
                }

                UTF.UnitTestOutcome outcome = testFailureException == null ? cleanupOutcome : cleanupOutcome.GetMoreImportantOutcome(result.Outcome);
                StackTraceInformation finalStackTraceInfo = cleanupStackTraceInfo != null ?
                                new StackTraceInformation(
                                    cleanupStackTrace.ToString(),
                                    cleanupStackTraceInfo.ErrorFilePath,
                                    cleanupStackTraceInfo.ErrorLineNumber,
                                    cleanupStackTraceInfo.ErrorColumnNumber) :
                                new StackTraceInformation(cleanupStackTrace.ToString());

                result.Outcome = outcome;
                result.TestFailureException = new TestFailedException(outcome.ToUnitTestOutcome(), cleanupError.ToString(), finalStackTraceInfo);
            }
        }

        private string GetTestCleanUpExceptionMessage(MethodInfo testCleanupMethod, Exception exception)
        {
            if (testCleanupMethod != null)
            {
                return string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.UTA_CleanupMethodThrows,
                            this.TestClassName,
                            testCleanupMethod?.Name,
                            exception.GetType().ToString(),
                            StackTraceHelper.GetExceptionMessage(exception));
            }
            else
            {
                return string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.UTA_CleanupMethodThrowsGeneralError,
                            this.TestClassName,
                            StackTraceHelper.GetExceptionMessage(exception));
            }
        }

        /// <summary>
        /// Runs TestInitialize methods of parent TestClass and the base classes.
        /// </summary>
        /// <param name="classInstance">Instance of TestClass.</param>
        /// <param name="result">Instance of TestResult.</param>
        /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private bool RunTestInitializeMethod(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            MethodInfo testInitializeMethod = null;
            try
            {
                // TestInitialize methods for base classes are called in reverse order of discovery
                // Grandparent -> Parent -> Child TestClass
                var baseTestInitializeStack = new Stack<MethodInfo>(this.Parent.BaseTestInitializeMethodsQueue);
                while (baseTestInitializeStack.Count > 0)
                {
                    testInitializeMethod = baseTestInitializeStack.Pop();
                    testInitializeMethod?.InvokeAsSynchronousTask(classInstance, null);
                }

                testInitializeMethod = this.Parent.TestInitializeMethod;
                testInitializeMethod?.InvokeAsSynchronousTask(classInstance, null);

                return true;
            }
            catch (Exception ex)
            {
                var innerException = ex.GetInnerExceptionOrDefault();
                string exceptionMessage = null;
                StackTraceInformation exceptionStackTraceInfo = null;
                var outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;

                if (innerException.TryGetUnitTestAssertException(out outcome, out exceptionMessage, out exceptionStackTraceInfo))
                {
                    result.Outcome = outcome;
                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Failed,
                        exceptionMessage,
                        exceptionStackTraceInfo);
                }
                else
                {
                    var stackTrace = StackTraceHelper.GetStackTraceInformation(innerException);
                    var errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_InitMethodThrows,
                        this.TestClassName,
                        testInitializeMethod?.Name,
                        StackTraceHelper.GetExceptionMessage(innerException));

                    result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                    result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTrace);
                }
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
                if (this.Parent.TestContextProperty != null && this.Parent.TestContextProperty.CanWrite)
                {
                    this.Parent.TestContextProperty.SetValue(classInstance, this.TestMethodOptions.TestContext);
                }

                return true;
            }
            catch (Exception ex)
            {
                var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(ex.GetInnerExceptionOrDefault());
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_TestContextSetError,
                    this.TestClassName,
                    StackTraceHelper.GetExceptionMessage(ex.GetInnerExceptionOrDefault()));

                result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInfo);
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
        private object CreateTestClassInstance(TestResult result)
        {
            object classInstance = null;
            try
            {
                classInstance = this.Parent.Constructor.Invoke(null);
            }
            catch (Exception ex)
            {
                // In most cases, exception will be TargetInvocationException with real exception wrapped
                // in the InnerException; or user code throws an exception
                var actualException = ex.InnerException ?? ex;
                var exceptionMessage = StackTraceHelper.GetExceptionMessage(actualException);
                var stackTraceInfo = StackTraceHelper.GetStackTraceInformation(actualException);
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_InstanceCreationError,
                    this.TestClassName,
                    exceptionMessage);

                result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInfo);
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
            Debug.Assert(this.IsTimeoutSet, "Timeout should be set");

            TestResult result = null;
            Exception failure = null;

            void executeAsyncAction()
            {
                try
                {
                    result = this.ExecuteInternal(arguments);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }

            CancellationToken cancelToken = this.TestMethodOptions.TestContext.Context.CancellationTokenSource.Token;
            if (PlatformServiceProvider.Instance.ThreadOperations.Execute(executeAsyncAction, this.TestMethodOptions.Timeout, cancelToken))
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
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, this.TestMethodName);
                if (this.TestMethodOptions.TestContext.Context.CancellationTokenSource.IsCancellationRequested)
                {
                    errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, this.TestMethodName);
                }
                else
                {
                    // Cancel the token source as test has timedout
                    this.TestMethodOptions.TestContext.Context.CancellationTokenSource.Cancel();
                }

                TestResult timeoutResult = new TestResult() { Outcome = TestTools.UnitTesting.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };
                return timeoutResult;
            }
        }
    }
}
