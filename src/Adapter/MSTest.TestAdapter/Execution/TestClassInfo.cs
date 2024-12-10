// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ObjectModelUnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestClassInfo object.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class TestClassInfo
{
    private readonly Lock _testClassExecuteSyncObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TestClassInfo"/> class.
    /// </summary>
    /// <param name="type">Underlying test class type.</param>
    /// <param name="constructor">Constructor for the test class.</param>
    /// <param name="testContextProperty">Reference to the <see cref="TestContext"/> property in test class.</param>
    /// <param name="classAttribute">Test class attribute.</param>
    /// <param name="parent">Parent assembly info.</param>
    internal TestClassInfo(
        Type type,
        ConstructorInfo constructor,
        bool isParameterlessConstructor,
        PropertyInfo? testContextProperty,
        TestClassAttribute classAttribute,
        TestAssemblyInfo parent)
    {
        ClassType = type;
        Constructor = constructor;
        IsParameterlessConstructor = isParameterlessConstructor;
        TestContextProperty = testContextProperty;
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
    internal Dictionary<MethodInfo, TimeoutInfo> ClassInitializeMethodTimeoutMilliseconds { get; } = new();

    /// <summary>
    /// Gets the timeout for the class cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> ClassCleanupMethodTimeoutMilliseconds { get; } = new();

    /// <summary>
    /// Gets the timeout for the test initialize methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestInitializeMethodTimeoutMilliseconds { get; } = new();

    /// <summary>
    /// Gets the timeout for the test cleanup methods.
    /// We can use a dictionary because the MethodInfo is unique in an inheritance hierarchy.
    /// </summary>
    internal Dictionary<MethodInfo, TimeoutInfo> TestCleanupMethodTimeoutMilliseconds { get; } = new();

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

    internal List<MethodInfo> BaseClassInitMethods { get; } = new();

    internal List<MethodInfo> BaseClassCleanupMethods { get; } = new();

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
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public void RunClassInitialize(TestContext testContext)
    {
        // If no class initialize and no base class initialize, return
        if (ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
        {
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
        if (!IsClassInitializeExecuted)
        {
            // Acquiring a lock is usually a costly operation which does not need to be
            // performed every time if the class initialization is already executed.
            lock (_testClassExecuteSyncObject)
            {
                // Perform a check again.
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

        ObjectModelUnitTestOutcome outcome = realException is AssertInconclusiveException ? ObjectModelUnitTestOutcome.Inconclusive : ObjectModelUnitTestOutcome.Failed;

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

    internal UnitTestResult GetResultOrRunClassInitialize(ITestContext testContext, string initializationLogs, string initializationErrorLogs, string initializationTrace, string initializationTestContextMessages)
    {
        bool isSTATestClass = AttributeComparer.IsDerived<STATestClassAttribute>(ClassAttribute);
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isSTATestClass
            && isWindowsOS
            && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            // For optimization purposes, we duplicate some of the logic of RunClassInitialize here so we don't need to start
            // a thread for nothing.
            if ((ClassInitializeMethod is null && BaseClassInitMethods.Count == 0)
                || IsClassInitializeExecuted)
            {
                return DoRun();
            }

            UnitTestResult result = new(ObjectModelUnitTestOutcome.Error, "MSTest STATestClass ClassInitialize didn't complete");
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
                return new UnitTestResult(new TestFailedException(ObjectModelUnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()));
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

        // Local functions
        UnitTestResult DoRun()
        {
            UnitTestResult result = new(ObjectModelUnitTestOutcome.Passed, null);

            try
            {
                using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
                try
                {
                    // This runs the ClassInitialize methods only once but saves the
                    RunClassInitialize(testContext.Context);
                }
                finally
                {
                    initializationLogs += logListener.GetAndClearStandardOutput();
                    initializationTrace += logListener.GetAndClearDebugTrace();
                    initializationErrorLogs += logListener.GetAndClearStandardError();
                    initializationTestContextMessages += testContext.GetAndClearDiagnosticMessages();
                }
            }
            catch (TestFailedException ex)
            {
                result = new UnitTestResult(ex);
            }
            catch (Exception ex)
            {
                result = new UnitTestResult(new TestFailedException(ObjectModelUnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()));
            }
            finally
            {
                // Assembly initialize and class initialize logs are pre-pended to the first result.
                result.StandardOut = initializationLogs;
                result.StandardError = initializationErrorLogs;
                result.DebugTrace = initializationTrace;
                result.TestContextMessages = initializationTestContextMessages;
            }

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

        return FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () => methodInfo.InvokeAsSynchronousTask(null, testContext),
            testContext.CancellationTokenSource,
            timeout,
            methodInfo,
            new ClassExecutionContextScope(ClassType),
            Resource.ClassInitializeWasCancelled,
            Resource.ClassInitializeTimedOut);
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
                    ClassCleanupException = classCleanupMethod is not null ? InvokeCleanupMethod(classCleanupMethod, BaseClassCleanupMethods.Count) : null;
                    var baseClassCleanupQueue = new Queue<MethodInfo>(BaseClassCleanupMethods);
                    while (baseClassCleanupQueue.Count > 0 && ClassCleanupException is null)
                    {
                        classCleanupMethod = baseClassCleanupQueue.Dequeue();
                        ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, baseClassCleanupQueue.Count);
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
            var testFailedException = new TestFailedException(ObjectModelUnitTestOutcome.Failed, errorMessage, exceptionStackTraceInfo);
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
    internal void ExecuteClassCleanup()
    {
        if ((ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
            || IsClassCleanupExecuted)
        {
            return;
        }

        MethodInfo? classCleanupMethod = ClassCleanupMethod;

        lock (_testClassExecuteSyncObject)
        {
            if (IsClassCleanupExecuted
                // If ClassInitialize method has not been executed, then we should not execute ClassCleanup
                // Note that if there is no ClassInitialze method at all, we will still set
                // IsClassInitializeExecuted to true in RunClassInitialize
                || !IsClassInitializeExecuted)
            {
                return;
            }

            try
            {
                if (classCleanupMethod is not null)
                {
                    if (!ReflectHelper.Instance.IsNonDerivedAttributeDefined<IgnoreAttribute>(classCleanupMethod.DeclaringType!, false))
                    {
                        ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, remainingCleanupCount: BaseClassCleanupMethods.Count);
                    }
                }

                if (ClassCleanupException is null)
                {
                    for (int i = 0; i < BaseClassCleanupMethods.Count; i++)
                    {
                        classCleanupMethod = BaseClassCleanupMethods[i];
                        if (!ReflectHelper.Instance.IsNonDerivedAttributeDefined<IgnoreAttribute>(classCleanupMethod.DeclaringType!, false))
                        {
                            ClassCleanupException = InvokeCleanupMethod(classCleanupMethod, remainingCleanupCount: BaseClassCleanupMethods.Count - 1 - i);
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
            return;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (ClassCleanupException is TestFailedException)
        {
            throw ClassCleanupException;
        }

        Exception realException = ClassCleanupException.GetRealException();

        // special case AssertFailedException to trim off part of the stack trace
        string errorMessage = realException is AssertFailedException or AssertInconclusiveException
            ? realException.Message
            : realException.GetFormattedExceptionMessage();

        StackTraceInformation? exceptionStackTraceInfo = realException.TryGetStackTraceInformation();

        var testFailedException = new TestFailedException(
            ObjectModelUnitTestOutcome.Failed,
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

        throw testFailedException;
    }

    internal void RunClassCleanup(ITestContext testContext, ClassCleanupManager classCleanupManager, TestMethodInfo testMethodInfo, TestMethod testMethod, UnitTestResult[] results)
    {
        DebugEx.Assert(testMethodInfo.Parent == this, "Parent of testMethodInfo should be this TestClassInfo.");

        classCleanupManager.MarkTestComplete(testMethodInfo, testMethod, out bool shouldRunEndOfClassCleanup);
        if (!shouldRunEndOfClassCleanup)
        {
            return;
        }

        bool isSTATestClass = AttributeComparer.IsDerived<STATestClassAttribute>(ClassAttribute);
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isSTATestClass
            && isWindowsOS
            && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            // For optimization purposes, we duplicate some of the logic of ExecuteClassCleanup here so we don't need to start
            // a thread for nothing.
            if ((ClassCleanupMethod is null && BaseClassCleanupMethods.Count == 0)
                || IsClassCleanupExecuted)
            {
                DoRun();
                return;
            }

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
            string? initializationLogs = string.Empty;
            string? initializationErrorLogs = string.Empty;
            string? initializationTrace = string.Empty;
            string? initializationTestContextMessages = string.Empty;
            try
            {
                using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
                try
                {
                    ExecuteClassCleanup();
                }
                finally
                {
                    initializationLogs = logListener.GetAndClearStandardOutput();
                    initializationErrorLogs = logListener.GetAndClearStandardError();
                    initializationTrace = logListener.GetAndClearDebugTrace();
                    initializationTestContextMessages = testContext.GetAndClearDiagnosticMessages();
                }
            }
            catch (Exception ex)
            {
                if (results.Length > 0)
                {
#pragma warning disable IDE0056 // Use index operator
                    UnitTestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    lastResult.Outcome = ObjectModelUnitTestOutcome.Error;
                    lastResult.ErrorMessage = ex.Message;
                    lastResult.ErrorStackTrace = ex.StackTrace;
                }
            }
            finally
            {
                if (results.Length > 0)
                {
#pragma warning disable IDE0056 // Use index operator
                    UnitTestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    lastResult.StandardOut += initializationLogs;
                    lastResult.StandardError += initializationErrorLogs;
                    lastResult.DebugTrace += initializationTrace;
                    lastResult.TestContextMessages += initializationTestContextMessages;
                }
            }
        }
    }

    private TestFailedException? InvokeCleanupMethod(MethodInfo methodInfo, int remainingCleanupCount)
    {
        TimeoutInfo? timeout = null;
        if (ClassCleanupMethodTimeoutMilliseconds.TryGetValue(methodInfo, out TimeoutInfo localTimeout))
        {
            timeout = localTimeout;
        }

        return FixtureMethodRunner.RunWithTimeoutAndCancellation(
            () => methodInfo.InvokeAsSynchronousTask(null),
            new CancellationTokenSource(),
            timeout,
            methodInfo,
            new ClassExecutionContextScope(ClassType, remainingCleanupCount),
            Resource.ClassCleanupWasCancelled,
            Resource.ClassCleanupTimedOut);
    }
}
