// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTFUnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestClassInfo object.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage)]
#endif
public class TestClassInfo
{
    /// <summary>
    /// Test context property name.
    /// </summary>
    private const string TestContextPropertyName = "TestContext";

    private readonly Lock _testClassExecuteSyncObject = new();

    private TestResult? _classInitializeResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestClassInfo"/> class.
    /// </summary>
    /// <param name="type">Underlying test class type.</param>
    /// <param name="constructor">Constructor for the test class.</param>
    /// <param name="isParameterlessConstructor">Whether or not the test class constructor has no parameters.</param>
    /// <param name="classAttribute">Test class attribute.</param>
    /// <param name="parent">Parent assembly info.</param>
    internal TestClassInfo(
        Type type,
        ConstructorInfo constructor,
        bool isParameterlessConstructor,
        TestClassAttribute classAttribute,
        TestAssemblyInfo parent)
    {
        ClassType = type;
        Constructor = constructor;
        IsParameterlessConstructor = isParameterlessConstructor;
        TestContextProperty = ResolveTestContext(type);
        Parent = parent;
        ClassAttribute = classAttribute;
    }

    /// <summary>
    /// Gets the class attribute.
    /// </summary>
    public TestClassAttribute ClassAttribute { get; }

    /// <summary>
    /// Gets the class type.
    /// </summary>
    public Type ClassType { get; }

    /// <summary>
    /// Gets the constructor.
    /// </summary>
    public ConstructorInfo Constructor { get; }

    internal bool IsParameterlessConstructor { get; }

    /// <summary>
    /// Gets the test context property.
    /// </summary>
    public PropertyInfo? TestContextProperty { get; }

    /// <summary>
    /// Gets the parent <see cref="TestAssemblyInfo"/>.
    /// </summary>
    public TestAssemblyInfo Parent { get; }

    /// <summary>
    /// Gets the class initialize method.
    /// </summary>
    public MethodInfo? ClassInitializeMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the timeout for the class initialize methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> ClassInitializeMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the class cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> ClassCleanupMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the test initialize methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestInitializeMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets the timeout for the test cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestCleanupMethodTimeoutMilliseconds { get; } = [];

    /// <summary>
    /// Gets a value indicating whether class initialize has executed.
    /// </summary>
    public bool IsClassInitializeExecuted { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether class cleanup has executed.
    /// </summary>
    public bool IsClassCleanupExecuted { get; internal set; }

    /// <summary>
    /// Gets a stack of class cleanup methods to be executed.
    /// </summary>
    [Obsolete("API will be dropped in v4")]
    public Stack<MethodInfo> BaseClassCleanupMethodsStack { get; } = new();

    internal List<MethodInfo> BaseClassInitMethods { get; } = [];

    internal List<MethodInfo> BaseClassCleanupMethods { get; } = [];

    /// <summary>
    /// Gets the exception thrown during <see cref="ClassInitializeAttribute"/> method invocation.
    /// </summary>
    public Exception? ClassInitializationException { get; internal set; }

    /// <summary>
    /// Gets the exception thrown during <see cref="ClassCleanupAttribute"/> method invocation.
    /// </summary>
    public Exception? ClassCleanupException { get; internal set; }

    /// <summary>
    /// Gets the class cleanup method.
    /// </summary>
    public MethodInfo? ClassCleanupMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this class info has a executable cleanup method.
    /// </summary>
    public bool HasExecutableCleanupMethod
    {
        get
        {
            // If class has a cleanup method, then it is executable.
            if (ClassCleanupMethod is not null)
            {
                return true;
            }

            // Otherwise, if any base cleanups were pushed to the stack we need to run them
            return BaseClassCleanupMethods.Count != 0;
        }
    }

    /// <summary>
    /// Gets a tuples' queue of class initialize/cleanup methods to call for this type.
    /// </summary>
    [Obsolete("API will be dropped in v4")]
    public Queue<Tuple<MethodInfo?, MethodInfo?>> BaseClassInitAndCleanupMethods { get; } = new();

    /// <summary>
    /// Gets the test initialize method.
    /// </summary>
    public MethodInfo? TestInitializeMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the test cleanup method.
    /// </summary>
    public MethodInfo? TestCleanupMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    internal ExecutionContext? ExecutionContext { get; set; }

    /// <summary>
    /// Gets a queue of test initialize methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestInitializeMethodsQueue { get; } = new();

    /// <summary>
    /// Gets a queue of test cleanup methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestCleanupMethodsQueue { get; } = new();

    /// <summary>
    /// Runs the class initialize method.
    /// </summary>
    /// <param name="testContext"> The test context. </param>
    /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
    public void RunClassInitialize(TestContext testContext)
    {
        // If no class initialize and no base class initialize, return
        if (ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
        {
            DebugEx.Assert(false, "Caller shouldn't call us if nothing to execute");
            IsClassInitializeExecuted = true;
            return;
        }

        if (testContext == null)
        {
            // TODO: Change this exception type to ArgumentNullException
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new NullReferenceException(Resource.TestContextIsNull);
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        MethodInfo? initializeMethod = null;
        string? failedClassInitializeMethodName = string.Empty;

        // If class initialization is not done, then do it.
        DebugEx.Assert(!IsClassInitializeExecuted, "Caller shouldn't call us if it was executed.");
        if (!IsClassInitializeExecuted)
        {
            try
            {
                // We have discovered the methods from bottom (most derived) to top (less derived) but we want to execute
                // from top to bottom.
                for (int i = BaseClassInitMethods.Count - 1; i >= 0; i--)
                {
                    initializeMethod = BaseClassInitMethods[i];
                    ClassInitializationException = InvokeInitializeMethod(initializeMethod, testContext);
                    if (ClassInitializationException is not null)
                    {
                        break;
                    }
                }

                if (ClassInitializationException is null)
                {
                    initializeMethod = ClassInitializeMethod;
                    ClassInitializationException = InvokeInitializeMethod(ClassInitializeMethod, testContext);
                }
            }
            catch (Exception ex)
            {
                ClassInitializationException = ex;
                failedClassInitializeMethodName = initializeMethod?.Name ?? ClassInitializeMethod?.Name;
            }
            finally
            {
                IsClassInitializeExecuted = true;
            }
        }

        // If classInitialization was successful, then don't do anything
        if (ClassInitializationException == null)
        {
            return;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (ClassInitializationException is TestFailedException)
        {
            throw ClassInitializationException;
        }

        // Fail the current test if it was a failure.
        Exception realException = ClassInitializationException.GetRealException();

        UTFUnitTestOutcome outcome = realException is AssertInconclusiveException ? UTFUnitTestOutcome.Inconclusive : UTFUnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetFormattedExceptionMessage(realException) as it prefixes the message with the exception type name.
        string exceptionMessage = realException.TryGetMessage();
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassInitMethodThrows,
            ClassType.FullName,
            failedClassInitializeMethodName,
            realException.GetType().ToString(),
            exceptionMessage);
        StackTraceInformation? exceptionStackTraceInfo = realException.GetStackTraceInformation();

        var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
        ClassInitializationException = testFailedException;

        throw testFailedException;
    }

    private TestResult? TryGetClonedCachedClassInitializeResult()
        // Historically, we were not caching class initialize result, and were always going through the logic in GetResultOrRunClassInitialize.
        // When caching is introduced, we found out that using the cached instance can change the behavior in some cases. For example,
        // if you have Console.WriteLine in class initialize, those will be present on the TestResult.
        // Before caching was introduced, these logs will be only in the first class initialize result (attached to the first test run in class)
        // By re-using the cached instance, it's now part of all tests.
        // To preserve the original behavior, we clone the cached instance so we keep only the information we are sure should be reused.
        => _classInitializeResult is null
            ? null
            : new()
            {
                Outcome = _classInitializeResult.Outcome,
                IgnoreReason = _classInitializeResult.IgnoreReason,
                TestFailureException = _classInitializeResult.TestFailureException,
            };

    internal TestResult GetResultOrRunClassInitialize(ITestContext testContext, string? initializationLogs, string? initializationErrorLogs, string? initializationTrace, string? initializationTestContextMessages)
    {
        TestResult? clonedInitializeResult = TryGetClonedCachedClassInitializeResult();

        // Optimization: If we already ran before and know the result, return it.
        if (clonedInitializeResult is not null)
        {
            DebugEx.Assert(IsClassInitializeExecuted, "Class initialize result should be available if and only if class initialize was executed");
            return clonedInitializeResult;
        }

        // For optimization purposes, return right away if there is nothing to execute.
        // For STA, this avoids starting a thread when we know it will do nothing.
        // But we still return early even not STA.
        if (ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
        {
            IsClassInitializeExecuted = true;
            return _classInitializeResult = new() { Outcome = TestTools.UnitTesting.UnitTestOutcome.Passed };
        }

        // At this point, maybe class initialize was executed by another thread such
        // that TryGetClonedCachedClassInitializeResult would return non-null.
        // Now, we need to check again, but under a lock.
        // Note that we are duplicating the logic above.
        // We could keep the logic in lock only and not duplicate, but we don't want to pay
        // the lock cost unnecessarily for a common case.
        // We also need to lock to avoid concurrency issues and guarantee that class init is called only once.
        lock (_testClassExecuteSyncObject)
        {
            clonedInitializeResult = TryGetClonedCachedClassInitializeResult();

            // Optimization: If we already ran before and know the result, return it.
            if (clonedInitializeResult is not null)
            {
                DebugEx.Assert(IsClassInitializeExecuted, "Class initialize result should be available if and only if class initialize was executed");
                return clonedInitializeResult;
            }

            DebugEx.Assert(!IsClassInitializeExecuted, "If class initialize was executed, we should have been in the previous if were we have a result available.");

            bool isSTATestClass = ClassAttribute is STATestClassAttribute;
            bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isSTATestClass
                && isWindowsOS
                && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                var result = new TestResult
                {
                    Outcome = TestTools.UnitTesting.UnitTestOutcome.Error,
                    IgnoreReason = "MSTest STATestClass ClassInitialize didn't complete",
                };

                Thread entryPointThread = new(() => result = DoRun())
                {
                    Name = "MSTest STATestClass ClassInitialize",
                };

                entryPointThread.SetApartmentState(ApartmentState.STA);
                entryPointThread.Start();

                try
                {
                    entryPointThread.Join();
                    return result;
                }
                catch (Exception ex)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(ex.ToString());
                    return new TestResult
                    {
                        TestFailureException = new TestFailedException(UTFUnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()),
                        Outcome = UTFUnitTestOutcome.Error,
                    };
                }
            }
            else
            {
                // If the requested apartment state is STA and the OS is not Windows, then warn the user.
                if (!isWindowsOS && isSTATestClass)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.STAIsOnlySupportedOnWindowsWarning);
                }

                return DoRun();
            }
        }

        // Local functions
        TestResult DoRun()
        {
            var result = new TestResult
            {
                Outcome = TestTools.UnitTesting.UnitTestOutcome.Passed,
            };

            try
            {
                // This runs the ClassInitialize methods only once but saves the
                RunClassInitialize(testContext.Context);
            }
            catch (TestFailedException ex)
            {
                result = new TestResult { TestFailureException = ex, Outcome = ex.Outcome };
            }
            catch (Exception ex)
            {
                result = new TestResult
                {
                    TestFailureException = new TestFailedException(UTFUnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()),
                    Outcome = UTFUnitTestOutcome.Error,
                };
            }
            finally
            {
                // Assembly initialize and class initialize logs are pre-pended to the first result.
                var testContextImpl = testContext as TestContextImplementation;
                result.LogOutput = initializationLogs + testContextImpl?.GetOut();
                result.LogError = initializationErrorLogs + testContextImpl?.GetErr();
                result.DebugTrace = initializationTrace; // TODO: DebugTrace
                result.TestContextMessages = initializationTestContextMessages + testContext.GetAndClearDiagnosticMessages();
            }

            _classInitializeResult = result;
            return result;
        }
    }

    private TestFailedException? InvokeInitializeMethod(MethodInfo? methodInfo, TestContext testContext)
    {
        if (methodInfo is null)
        {
            return null;
        }

        TimeoutInfo? timeout = null;
        if (ClassInitializeMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () =>
            {
                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                // It's safer to reset it before the capture.
                using (TestContextImplementation.SetCurrentTestContext(testContext as TestContextImplementation))
                {
                    methodInfo.InvokeAsSynchronousTask(null, testContext);
                }

                // **After** we have executed the class initialize, we save the current context.
                // This context will contain async locals set by the class initialize method.
                ExecutionContext = ExecutionContext.Capture();
            },
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            ExecutionContext ?? Parent?.ExecutionContext,
            Resource.ClassInitializeWasCancelled,
            Resource.ClassInitializeTimedOut);

        return result;
    }

    /// <summary>
    /// Run class cleanup methods.
    /// </summary>
    /// <param name="classCleanupLifecycle">The current lifecycle position that ClassCleanup is executing from.</param>
    /// <returns>
    /// Any exception that can be thrown as part of a class cleanup as warning messages.
    /// </returns>
    [Obsolete("API will be dropped in v4")]
    public string? RunClassCleanup(ClassCleanupBehavior classCleanupLifecycle = ClassCleanupBehavior.EndOfAssembly)
    {
        if (ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
        {
            return null;
        }

        if (IsClassCleanupExecuted)
        {
            return null;
        }

        MethodInfo? classCleanupMethod = null;
        lock (_testClassExecuteSyncObject)
        {
            if (IsClassCleanupExecuted)
            {
                return null;
            }

            if (IsClassInitializeExecuted || ClassInitializeMethod is null)
            {
                try
                {
                    classCleanupMethod = ClassCleanupMethod;
                    ClassCleanupException = classCleanupMethod is not null ? InvokeCleanupMethod(classCleanupMethod, null!) : null;
                    var baseClassCleanupQueue = new Queue<MethodInfo>(BaseClassCleanupMethods);
                    while (baseClassCleanupQueue.Count > 0 && ClassCleanupException is null)
                    {
                        classCleanupMethod = baseClassCleanupQueue.Dequeue();
                        ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, null!);
                    }

                    IsClassCleanupExecuted = ClassCleanupException is null;
                }
                catch (Exception exception)
                {
                    ClassCleanupException = exception;
                }
            }
        }

        // If ClassCleanup was successful, then don't do anything
        if (ClassCleanupException == null)
        {
            return null;
        }

        Exception realException = ClassCleanupException.GetRealException();

        // special case AssertFailedException to trim off part of the stack trace
        string errorMessage = realException is AssertFailedException or AssertInconclusiveException
            ? realException.Message
            : realException.GetFormattedExceptionMessage();

        StackTraceInformation? exceptionStackTraceInfo = realException.TryGetStackTraceInformation();

        errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassCleanupMethodWasUnsuccesful,
            classCleanupMethod!.DeclaringType!.Name,
            classCleanupMethod.Name,
            errorMessage,
            exceptionStackTraceInfo?.ErrorStackTrace);

        if (classCleanupLifecycle == ClassCleanupBehavior.EndOfClass)
        {
            var testFailedException = new TestFailedException(UTFUnitTestOutcome.Failed, errorMessage, exceptionStackTraceInfo);
            ClassCleanupException = testFailedException;
            throw testFailedException;
        }

        return errorMessage;
    }

    /// <summary>
    /// Execute current and base class cleanups.
    /// </summary>
    /// <remarks>
    /// This is a replacement for RunClassCleanup but as we are on a bug fix version, we do not want to change
    /// the public API, hence this method.
    /// </remarks>
    internal TestFailedException? ExecuteClassCleanup(TestContext testContext)
    {
        if ((ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
            || IsClassCleanupExecuted)
        {
            return null;
        }

        MethodInfo? classCleanupMethod = ClassCleanupMethod;

        lock (_testClassExecuteSyncObject)
        {
            if (IsClassCleanupExecuted
                // If ClassInitialize method has not been executed, then we should not execute ClassCleanup
                // Note that if there is no ClassInitialze method at all, we will still set
                // IsClassInitializeExecuted to true in RunClassInitialize
                // IsClassInitializeExecuted can be false if all tests in the class are ignored.
                || !IsClassInitializeExecuted)
            {
                return null;
            }

            try
            {
                if (classCleanupMethod is not null)
                {
                    if (!classCleanupMethod.DeclaringType!.IsIgnored(out _))
                    {
                        ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, testContext);
                    }
                }

                if (ClassCleanupException is null)
                {
                    for (int i = 0; i < BaseClassCleanupMethods.Count; i++)
                    {
                        classCleanupMethod = BaseClassCleanupMethods[i];
                        if (!classCleanupMethod.DeclaringType!.IsIgnored(out _))
                        {
                            ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, testContext);
                            if (ClassCleanupException is not null)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                ClassCleanupException = exception;
            }
            finally
            {
                IsClassCleanupExecuted = true;
            }
        }

        // If ClassCleanup was successful, then don't do anything
        if (ClassCleanupException == null)
        {
            return null;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (ClassCleanupException is TestFailedException classCleanupEx)
        {
            return classCleanupEx;
        }

        Exception realException = ClassCleanupException.GetRealException();

        // special case AssertFailedException to trim off part of the stack trace
        string errorMessage = realException is AssertFailedException or AssertInconclusiveException
            ? realException.Message
            : realException.GetFormattedExceptionMessage();

        StackTraceInformation? exceptionStackTraceInfo = realException.TryGetStackTraceInformation();

        var testFailedException = new TestFailedException(
            UTFUnitTestOutcome.Failed,
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_ClassCleanupMethodWasUnsuccesful,
                classCleanupMethod!.DeclaringType!.Name,
                classCleanupMethod.Name,
                errorMessage,
                exceptionStackTraceInfo?.ErrorStackTrace),
            exceptionStackTraceInfo,
            realException);
        ClassCleanupException = testFailedException;

        return testFailedException;
    }

    internal void RunClassCleanup(ITestContext testContext, ClassCleanupManager classCleanupManager, TestMethodInfo testMethodInfo, TestMethod testMethod, TestResult[] results)
    {
        DebugEx.Assert(testMethodInfo.Parent == this, "Parent of testMethodInfo should be this TestClassInfo.");

        classCleanupManager.MarkTestComplete(testMethodInfo, testMethod, out bool shouldRunEndOfClassCleanup);
        if (!shouldRunEndOfClassCleanup)
        {
            return;
        }

        if ((ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
                || IsClassCleanupExecuted)
        {
            // DoRun will already do nothing for this condition. So, we gain a bit of performance.
            return;
        }

        bool isSTATestClass = ClassAttribute is STATestClassAttribute;
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isSTATestClass
            && isWindowsOS
            && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            Thread entryPointThread = new(DoRun)
            {
                Name = "MSTest STATestClass ClassCleanup",
            };

            entryPointThread.SetApartmentState(ApartmentState.STA);
            entryPointThread.Start();

            try
            {
                entryPointThread.Join();
            }
            catch (Exception ex)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(ex.ToString());
            }
        }
        else
        {
            // If the requested apartment state is STA and the OS is not Windows, then warn the user.
            if (!isWindowsOS && isSTATestClass)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.STAIsOnlySupportedOnWindowsWarning);
            }

            DoRun();
        }

        // Local functions
        void DoRun()
        {
            try
            {
                TestFailedException? ex = ExecuteClassCleanup(testContext.Context);
                if (ex is not null && results.Length > 0)
                {
#pragma warning disable IDE0056 // Use index operator
                    TestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    lastResult.Outcome = TestTools.UnitTesting.UnitTestOutcome.Error;
                    lastResult.TestFailureException = ex;
                }
            }
            finally
            {
                if (results.Length > 0)
                {
#pragma warning disable IDE0056 // Use index operator
                    TestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    var testContextImpl = testContext as TestContextImplementation;
                    lastResult.LogOutput += testContextImpl?.GetOut();
                    lastResult.LogError += testContextImpl?.GetErr();
                    // TODO: DebugTrace
                    lastResult.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
                }
            }
        }
    }

    private TestFailedException? InvokeCleanupMethod(MethodInfo methodInfo, TestContext testContext)
    {
        TimeoutInfo? timeout = null;
        if (ClassCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        TestFailedException? result = FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () =>
            {
                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                // It's safer to reset it before the capture.
                using (TestContextImplementation.SetCurrentTestContext(testContext as TestContextImplementation))
                {
                    if (methodInfo.GetParameters().Length == 0)
                    {
                        methodInfo.InvokeAsSynchronousTask(null);
                    }
                    else
                    {
                        methodInfo.InvokeAsSynchronousTask(null, testContext);
                    }
                }

                // **After** we have executed the class cleanup, we save the current context.
                // This context will contain async locals set by the current class cleanup method.
                // This is essential to propagate async locals between multiple class cleanup methods.
                ExecutionContext = ExecutionContext.Capture();
            },
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            ExecutionContext ?? Parent.ExecutionContext,
            Resource.ClassCleanupWasCancelled,
            Resource.ClassCleanupTimedOut);

        return result;
    }

    /// <summary>
    /// Resolves the test context property.
    /// </summary>
    /// <param name="classType"> The class Type. </param>
    /// <returns> The <see cref="PropertyInfo"/> for TestContext property. Null if not defined. </returns>
    private static PropertyInfo? ResolveTestContext(Type classType)
    {
        try
        {
            PropertyInfo? testContextProperty = PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeProperty(classType, TestContextPropertyName, includeNonPublic: false);
            if (testContextProperty == null)
            {
                // that's okay may be the property was not defined
                return null;
            }

            // check if testContextProperty is of correct type
            if (!string.Equals(testContextProperty.PropertyType.FullName, typeof(TestContext).FullName, StringComparison.Ordinal))
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextTypeMismatchLoadError, classType.FullName);
                throw new TypeInspectionException(errorMessage);
            }

            return testContextProperty;
        }
        catch (AmbiguousMatchException ex)
        {
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextLoadError, classType.FullName, ex.Message);
            throw new TypeInspectionException(errorMessage);
        }
    }
}
