// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using Extensions;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using ObjectModel;

    /// <summary>
    /// Defines the TestMethod Info object
    /// </summary>
    public class TestMethodInfo : ITestMethod
    {
        internal TestMethodInfo(
            MethodInfo testMethod,
            int timeout,
            TestMethodAttribute executor,
            TestClassInfo parent,
            ITestContext testContext)
        {
            Debug.Assert(testMethod != null, "TestMethod should not be null");
            Debug.Assert(parent != null, "Parent should not be null");

            this.TestMethod = testMethod;
            this.Timeout = timeout;
            this.Parent = parent;
            this.Executor = executor;
            this.testContext = testContext;
        }

        public TestMethodAttribute Executor { set; get; }

        /// <summary>
        /// TestMethod referred by this object
        /// </summary>
        public MethodInfo TestMethod { get; private set; }

        /// <summary>
        /// Timeout defined on the test method. 
        /// </summary>
        public int Timeout { get; private set; }

        /// <summary>
        /// Parent class Info object
        /// </summary>
        public TestClassInfo Parent { get; internal set; }

        /// <summary>
        /// Specifies whether timeout is set or not
        /// </summary>
        public bool IsTimeoutSet => (this.Timeout != TimeoutWhenNotSet);

        /// <summary>
        /// Reason why test is not runnable
        /// </summary>
        public string NotRunnableReason { get; internal set; }

        /// <summary>
        /// Specifies whether test is runnnable
        /// </summary>
        public bool IsRunnable => string.IsNullOrEmpty(this.NotRunnableReason);

        /// <summary>
        /// Specifies the timeout when it is not set in a test case
        /// </summary>
        public const int TimeoutWhenNotSet = 0;
        private readonly ITestContext testContext;

        #region ITestMethod implementation

        /// <inheritdoc/>
        public ParameterInfo[] ParameterTypes => this.TestMethod.GetParameters();

        /// <inheritdoc/>
        public Type ReturnType => this.TestMethod.ReturnType;

        /// <inheritdoc/>
        public string TestClassName => this.Parent.ClassType.FullName;

        /// <inheritdoc/>
        public string TestMethodName => this.TestMethod.Name;

        /// <inheritdoc/>
        public MethodInfo MethodInfo => this.TestMethod;

        /// <inheritdoc/>
        public Attribute[] GetAllAttributes(bool inherit)
        {
            return ReflectHelper.GetCustomAttributes(this.TestMethod, null, inherit) as Attribute[];
        }

        /// <inheritdoc/>
        public TAttributeType[] GetAttributes<TAttributeType>(bool inherit) where TAttributeType : Attribute
        {
            Attribute[] attributeArray =  ReflectHelper.GetCustomAttributes(this.TestMethod, typeof(TAttributeType), inherit) as Attribute[];

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

        /// <inheritdoc/>
        /// <remarks>
        /// Execute test method. Capture failures, handle async and return result.
        /// </remarks>
        public virtual TestResult Invoke(object[] arguments)
        {
            Stopwatch watch = new Stopwatch();
            TestResult result = null;
            using (LogMessageListener listener = new LogMessageListener(true))
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
                    }
                }
            }

            return result;
        }
        #endregion

        /// <summary>
        /// Execute test without timeout.
        /// </summary>
        private TestResult ExecuteInternal(object[] arguments)
        {
            Debug.Assert(this.TestMethod != null, "UnitTestExecuter.DefaultTestMethodInvoke: testMethod = null.");

            var result = new TestResult();

            // TODO armahapa remove dry violation with TestMethodRunner
            var classInstance = this.CreateTestClassInstance(result);
            var testContextSetup = false;

            try
            {
                if (classInstance != null && this.SetTestContext(classInstance, result))
                {
                    // For any failure after this point, we must run TestCleanup
                    testContextSetup = true;

                    if (this.RunTestInitializeMethod(classInstance, result))
                    {
                        this.TestMethod.InvokeAsSynchronousTask(classInstance, arguments);
                        result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Passed;
                    }
                }
            }
#if TODO
            catch (ThreadAbortException ex)
            {
                // Cancel the Abort caused by the test method. This abort is caused by the user code 
                // and will allow Adapter gracefuly finish the test method call.
                //
                // Ideally we probably we should do it all the time because we want to abort just the user code
                // not our but currently the code is tuned such it will cause unexpected behaviour (like different
                // outcome)

                // Also we may consider doing something similar for StackOverflow exception
                // (it has similar behaviour as ThreadAbortException as being automatically rethrown after
                // in the catch () block.
                Thread.ResetAbort();

                result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Unknown;
                result.TestFailureException = ex;
            }
#endif
            catch (Exception ex)
            {
                // This block should not throw. If it needs to throw, then handling of
                // ThreadAbortException will need to be revisited. See comment in RunTestMethod.
                result.TestFailureException = this.HandleMethodException(
                    ex,
                    this.TestClassName,
                    this.TestMethodName);

                if (ex.InnerException != null && ex.InnerException is AssertInconclusiveException)
                {
                    result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Inconclusive;
                }
                else
                {
                    result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                }
            }
            finally
            {
                if (classInstance != null && testContextSetup)
                {
                    this.RunTestCleanupMethod(classInstance, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Handles the exception that is thrown by a test method. The exception can either
        /// be expected or not expected
        /// </summary>
        /// <param name="ex">Exception that was thrown</param>
        /// <param name="className">The class name.</param>
        /// <param name="methodName">The method name.</param>
        private Exception HandleMethodException(Exception ex, string className, string methodName)
        {
            Debug.Assert(ex != null);

            var isTargetInvocationException = ex is TargetInvocationException;
            if (isTargetInvocationException && ex.InnerException == null)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, className, methodName);
                return new TestFailedException(UnitTestOutcome.Error, errorMessage);
            }

            // Get the real exception thrown by the test method
            Exception realException;
            if (isTargetInvocationException)
            {
                Debug.Assert(ex.InnerException != null, "Inner exception of TargetInvocationException is null. This should occur because we should have caught this case above.");

                // Our reflected call will typically always get back a TargetInvocationException
                // containing the real exception thrown by the test method as its inner exception
                realException = ex.InnerException;
            }
            else
            {
                realException = ex;
            }
            
            if (realException is UnitTestAssertException)
            {
                return new TestFailedException(realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed,
                                              realException.TryGetMessage(),
                                              realException.TryGetStackTraceInformation(),
                                              realException);
            }
            else
            {
                var errorMessage = string.Empty;

                // Handle special case of UI objects in TestMethod to suggest UITestMethod
                if (realException.HResult == -2147417842)
                {
                    errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_WrongThread, string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows,
                        className, methodName, StackTraceHelper.GetExceptionMessage(realException)));
                }
                else
                {
                    errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows,
                        className, methodName, StackTraceHelper.GetExceptionMessage(realException));
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

                    while (this.Parent.BaseTestCleanupMethodsQueue.Count > 0)
                    {
                        testCleanupMethod = this.Parent.BaseTestCleanupMethodsQueue.Dequeue();
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
                // FIXME case where TestMethod throws and TestCleanup throws as well, merge the stack traces!
                var stackTraceInformation = StackTraceHelper.GetStackTraceInformation(ex.GetInnerExceptionOrDefault());
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_CleanupMethodThrows,
                    this.TestClassName,
                    testCleanupMethod?.Name,
                    StackTraceHelper.GetExceptionMessage(ex.GetInnerExceptionOrDefault()));
                result.Outcome = TestTools.UnitTesting.UnitTestOutcome.Failed;
                result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInformation);
            }
        }

        /// <summary>
        /// Runs TestInitialize methods of parent TestClass and the base classes.
        /// </summary>
        /// <param name="classInstance">Instance of TestClass.</param>
        /// <param name="result">Instance of TestResult.</param>
        /// <returns>True if the TestInitialize method(s) did not throw an exception.</returns>
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
                if (innerException is UnitTestAssertException)
                {
                    result.Outcome = innerException is AssertInconclusiveException
                                         ? TestTools.UnitTesting.UnitTestOutcome.Inconclusive
                                         : TestTools.UnitTesting.UnitTestOutcome.Failed;

                    result.TestFailureException = new TestFailedException(
                        UnitTestOutcome.Failed,
                        innerException.TryGetMessage(),
                        innerException.TryGetStackTraceInformation());
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
        private bool SetTestContext(object classInstance, TestResult result)
        {
            Debug.Assert(classInstance != null, "classInstance != null");
            Debug.Assert(result != null, "result != null");

            try
            {
                if (this.Parent.TestContextProperty != null && this.Parent.TestContextProperty.CanWrite)
                {
                    this.Parent.TestContextProperty.SetValue(classInstance, this.testContext);
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
                // in the InnerException.
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
        /// Execute test which has a timeout
        /// </summary>        
        private TestResult ExecuteInternalWithTimeout(object[] arguments)
        {
            Debug.Assert(this.IsTimeoutSet, "Timeout should be set");

            // Using our own thread to control the apartment state as otherwise the apartment state of a threadpool thread is always MTA. 
            // bug: 321922
            TestResult result = null;
            Exception failure = null;

#if TODO
            Thread executionThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    result = this.ExecuteInternal(arguments);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }));
            executionThread.IsBackground = true;
            executionThread.Name = "MSAppContainerTest Thread";

            executionThread.SetApartmentState(Thread.CurrentThread.GetApartmentState());
            executionThread.Start();
            if (executionThread.Join(this.Timeout))
            {
                if (failure != null)
                {
                    throw failure;
                }

                Debug.Assert(result != null, "no timeout, no failure result should not be null");
                return result;
            }
            else
            {
                // Timed out
                try
                {
                    // Abort test thread after timeout.
                    executionThread.Abort();
                }
                catch (ThreadStateException)
                {
                    // Catch and discard ThreadStateException. If Abort is called on a thread that has been suspended, 
                    // a ThreadStateException is thrown in the thread that called Abort, 
                    // and AbortRequested is added to the ThreadState property of the thread being aborted. 
                    // A ThreadAbortException is not thrown in the suspended thread until Resume is called.
                }

                // If the method times out, then 
                //
                // 1. If the test is stuck, then we can get CannotUnloadAppDomain exception. 
                //
                // Which are handled as follows: - 
                //
                // For #1, we are now restarting the execution process if adapter fails to unload app-domain.
                //
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, this.TestMethodName);
                TestResult timeoutResult = new TestResult() { Outcome = TestTools.UnitTesting.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };
                timeoutResult.Duration = TimeSpan.FromMilliseconds(this.Timeout);
                return timeoutResult;
            }
#else
#if WINDOWS
            IAsyncAction asyncAction;
            asyncAction = ThreadPool.RunAsync(new WorkItemHandler((IAsyncAction) => result = ExecuteInternal(arguments)), WorkItemPriority.Normal, WorkItemOptions.TimeSliced);
            Task executionTask = asyncAction.AsTask();
#else
            var tokenSource = new CancellationTokenSource();
            Task executionTask = Task.Factory.StartNew(() => result = this.ExecuteInternal(arguments), tokenSource.Token);
#endif

            if (executionTask.Wait(this.Timeout))
            {
                if (failure != null)
                {
                    throw failure;
                }

                Debug.Assert(result != null, "no timeout, no failure result should not be null");
                return result;
            }
            else
            {
                // Timed out
                try
                {
                    // Abort test thread after timeout.
#if WINDOWS
                    asyncAction.Cancel();
#else
                    tokenSource.Cancel();
#endif
                }
                catch (Exception)
                {
                }
                finally
                {
#if !WINDOWS
                    tokenSource.Dispose();
#endif
                }

                // If the method times out, then 
                //
                // 1. If the test is stuck, then we can get CannotUnloadAppDomain exception. 
                //
                // Which are handled as follows: - 
                //
                // For #1, we are now restarting the execution process if adapter fails to unload app-domain.
                //
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, this.TestMethodName);
                TestResult timeoutResult = new TestResult() { Outcome = TestTools.UnitTesting.UnitTestOutcome.Timeout, TestFailureException = new TestFailedException(UnitTestOutcome.Timeout, errorMessage) };
                timeoutResult.Duration = TimeSpan.FromMilliseconds(this.Timeout);
                return timeoutResult;
            }
#endif
        }

    }
}
