// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ObjectModelUnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestClassInfo object.
/// </summary>
public class TestClassInfo
{
    private readonly object _testClassExecuteSyncObject;
    private MethodInfo _classCleanupMethod;
    private MethodInfo _classInitializeMethod;
    private MethodInfo _testCleanupMethod;
    private MethodInfo _testInitializeMethod;

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
        PropertyInfo testContextProperty,
        TestClassAttribute classAttribute,
        TestAssemblyInfo parent)
    {
        Debug.Assert(type != null, "Type should not be null");
        Debug.Assert(constructor != null, "Constructor should not be null");
        Debug.Assert(parent != null, "Parent should not be null");
        Debug.Assert(classAttribute != null, "ClassAttribute should not be null");

        ClassType = type;
        Constructor = constructor;
        TestContextProperty = testContextProperty;
        BaseClassCleanupMethodsStack = new Stack<MethodInfo>();
        BaseClassInitAndCleanupMethods = new Queue<Tuple<MethodInfo, MethodInfo>>();
        BaseTestInitializeMethodsQueue = new Queue<MethodInfo>();
        BaseTestCleanupMethodsQueue = new Queue<MethodInfo>();
        Parent = parent;
        ClassAttribute = classAttribute;
        _testClassExecuteSyncObject = new object();
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

    /// <summary>
    /// Gets the test context property.
    /// </summary>
    public PropertyInfo TestContextProperty { get; }

    /// <summary>
    /// Gets the parent <see cref="TestAssemblyInfo"/>.
    /// </summary>
    public TestAssemblyInfo Parent { get; }

    /// <summary>
    /// Gets the class initialize method.
    /// </summary>
    public MethodInfo ClassInitializeMethod
    {
        get
        {
            return _classInitializeMethod;
        }

        internal set
        {
            if (_classInitializeMethod != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            _classInitializeMethod = value;
        }
    }

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
    public Stack<MethodInfo> BaseClassCleanupMethodsStack { get; internal set; }

    /// <summary>
    /// Gets the exception thrown during <see cref="ClassInitializeAttribute"/> method invocation.
    /// </summary>
    public Exception ClassInitializationException { get; internal set; }

    /// <summary>
    /// Gets the exception thrown during <see cref="ClassCleanupAttribute"/> method invocation.
    /// </summary>
    public Exception ClassCleanupException { get; internal set; }

    /// <summary>
    /// Gets the class cleanup method.
    /// </summary>
    public MethodInfo ClassCleanupMethod
    {
        get
        {
            return _classCleanupMethod;
        }

        internal set
        {
            if (_classCleanupMethod != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            _classCleanupMethod = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this class info has a executable cleanup method.
    /// </summary>
    public bool HasExecutableCleanupMethod
    {
        get
        {
            if (BaseClassCleanupMethodsStack.Any())
            {
                // If any base cleanups were pushed to the stack we need to run them
                return true;
            }

            // If no class cleanup, then continue with the next one.
            if (ClassCleanupMethod == null)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets a tuples' queue of class initialize/cleanup methods to call for this type.
    /// </summary>
    public Queue<Tuple<MethodInfo, MethodInfo>> BaseClassInitAndCleanupMethods { get; }

    /// <summary>
    /// Gets the test initialize method.
    /// </summary>
    public MethodInfo TestInitializeMethod
    {
        get
        {
            return _testInitializeMethod;
        }

        internal set
        {
            if (_testInitializeMethod != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiInit, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            _testInitializeMethod = value;
        }
    }

    /// <summary>
    /// Gets the test cleanup method.
    /// </summary>
    public MethodInfo TestCleanupMethod
    {
        get
        {
            return _testCleanupMethod;
        }

        internal set
        {
            if (_testCleanupMethod != null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClean, ClassType.FullName);
                throw new TypeInspectionException(message);
            }

            _testCleanupMethod = value;
        }
    }

    /// <summary>
    /// Gets a queue of test initialize methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestInitializeMethodsQueue { get; }

    /// <summary>
    /// Gets a queue of test cleanup methods to call for this type.
    /// </summary>
    public Queue<MethodInfo> BaseTestCleanupMethodsQueue { get; }

    /// <summary>
    /// Runs the class initialize method.
    /// </summary>
    /// <param name="testContext"> The test context. </param>
    /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public void RunClassInitialize(TestContext testContext)
    {
        // If no class initialize and no base class initialize, return
        if (ClassInitializeMethod is null && !BaseClassInitAndCleanupMethods.Any(p => p.Item1 != null))
        {
            return;
        }

        if (testContext == null)
        {
            throw new NullReferenceException(Resource.TestContextIsNull);
        }

        MethodInfo initializeMethod = null;
        string failedClassInitializeMethodName = string.Empty;

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
                        // ClassInitialize methods for base classes are called in reverse order of discovery
                        // Base -> Child TestClass
                        var baseClassInitializeStack = new Stack<Tuple<MethodInfo, MethodInfo>>(
                                BaseClassInitAndCleanupMethods.Where(p => p.Item1 != null));

                        while (baseClassInitializeStack.Count > 0)
                        {
                            var baseInitCleanupMethods = baseClassInitializeStack.Pop();
                            initializeMethod = baseInitCleanupMethods.Item1;
                            initializeMethod?.InvokeAsSynchronousTask(null, testContext);

                            if (baseInitCleanupMethods.Item2 != null)
                            {
                                BaseClassCleanupMethodsStack.Push(baseInitCleanupMethods.Item2);
                            }
                        }

                        initializeMethod = null;

                        if (_classInitializeMethod != null)
                        {
                            ClassInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        ClassInitializationException = ex;
                        failedClassInitializeMethodName = initializeMethod?.Name ?? ClassInitializeMethod.Name;
                    }
                    finally
                    {
                        IsClassInitializeExecuted = true;
                        Debug.Assert(testContext is ITestContext, "Test context doesn't implement ITestContext.");
                        (testContext as ITestContext)?.ClearDiagnosticMessages();
                    }
                }
            }
        }

        // If classInitialization was successful, then don't do anything
        if (ClassInitializationException == null)
        {
            return;
        }

        if (ClassInitializationException is TestFailedException)
        {
            throw ClassInitializationException;
        }

        // Fail the current test if it was a failure.
        var realException = ClassInitializationException.InnerException ?? ClassInitializationException;

        var outcome = realException is AssertInconclusiveException ? ObjectModelUnitTestOutcome.Inconclusive : ObjectModelUnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetExceptionMessage(realException) as it prefixes the message with the exception type name.
        var exceptionMessage = realException.TryGetMessage();
        var errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_ClassInitMethodThrows,
            ClassType.FullName,
            failedClassInitializeMethodName,
            realException.GetType().ToString(),
            exceptionMessage);
        var exceptionStackTraceInfo = StackTraceHelper.GetStackTraceInformation(realException);

        var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
        ClassInitializationException = testFailedException;

        throw testFailedException;
    }

    /// <summary>
    /// Run class cleanup methods.
    /// </summary>
    /// <param name="classCleanupLifecycle">The current lifecycle position that ClassCleanup is executing from.</param>
    /// <returns>
    /// Any exception that can be thrown as part of a class cleanup as warning messages.
    /// </returns>
    public string RunClassCleanup(ClassCleanupBehavior classCleanupLifecycle = ClassCleanupBehavior.EndOfAssembly)
    {
        if (ClassCleanupMethod is null && BaseClassInitAndCleanupMethods.All(p => p.Item2 == null))
        {
            return null;
        }

        if (IsClassCleanupExecuted)
        {
            return null;
        }

        lock (_testClassExecuteSyncObject)
        {
            if (IsClassCleanupExecuted)
            {
                return null;
            }

            if (IsClassInitializeExecuted || ClassInitializeMethod is null)
            {
                MethodInfo classCleanupMethod = null;

                try
                {
                    classCleanupMethod = ClassCleanupMethod;
                    classCleanupMethod?.InvokeAsSynchronousTask(null);
                    var baseClassCleanupQueue = new Queue<MethodInfo>(BaseClassCleanupMethodsStack);
                    while (baseClassCleanupQueue.Count > 0)
                    {
                        classCleanupMethod = baseClassCleanupQueue.Dequeue();
                        classCleanupMethod?.InvokeAsSynchronousTask(null);
                    }

                    IsClassCleanupExecuted = true;

                    return null;
                }
                catch (Exception exception)
                {
                    var realException = exception.InnerException ?? exception;
                    ClassCleanupException = realException;

                    string errorMessage;

                    // special case AssertFailedException to trim off part of the stack trace
                    if (realException is AssertFailedException or AssertInconclusiveException)
                    {
                        errorMessage = realException.Message;
                    }
                    else
                    {
                        errorMessage = StackTraceHelper.GetExceptionMessage(realException);
                    }

                    var exceptionStackTraceInfo = realException.TryGetStackTraceInformation();

                    errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_ClassCleanupMethodWasUnsuccesful,
                        classCleanupMethod.DeclaringType.Name,
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
            }
        }

        return null;
    }
}
