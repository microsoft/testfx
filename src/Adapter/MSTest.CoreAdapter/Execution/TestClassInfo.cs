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
    using Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ObjectModel;
    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

    /// <summary>
    /// Defines the TestClassInfo object
    /// </summary>
    public class TestClassInfo
    {
        private MethodInfo classCleanupMethod;
        private MethodInfo classInitializeMethod;
        private MethodInfo testCleanupMethod;
        private MethodInfo testInitializeMethod;
        private object testClassExecuteSyncObject;

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
            Debug.Assert(classAttribute != null, "ClassAtribute should not be null");

            this.ClassType = type;
            this.Constructor = constructor;
            this.TestContextProperty = testContextProperty;
            this.BaseTestInitializeMethodsQueue = new Queue<MethodInfo>();
            this.BaseTestCleanupMethodsQueue = new Queue<MethodInfo>();
            this.Parent = parent;
            this.ClassAttribute = classAttribute;
            this.testClassExecuteSyncObject = new object();
        }

        /// <summary>
        /// Gets the class attribute.
        /// </summary>
        public TestClassAttribute ClassAttribute { get; private set; }

        /// <summary>
        /// Gets the class type.
        /// </summary>
        public Type ClassType { get; private set; }

        /// <summary>
        /// Gets the constructor.
        /// </summary>
        public ConstructorInfo Constructor { get; private set; }

        /// <summary>
        /// Gets the test context property.
        /// </summary>
        public PropertyInfo TestContextProperty { get; private set; }

        /// <summary>
        /// Gets the parent <see cref="TestAssemblyInfo"/>.
        /// </summary>
        public TestAssemblyInfo Parent { get; private set; }

        /// <summary>
        /// Gets the class initialize method.
        /// </summary>
        public MethodInfo ClassInitializeMethod
        {
            get
            {
                return this.classInitializeMethod;
            }

            internal set
            {
                if (this.classInitializeMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassInit, this.ClassType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.classInitializeMethod = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether is class initialize executed.
        /// </summary>
        public bool IsClassInitializeExecuted { get; internal set; }

        /// <summary>
        /// Gets the exception thrown during <see cref="ClassInitializeAttribute"/> method invocation.
        /// </summary>
        public Exception ClassInitializationException { get; internal set; }

        /// <summary>
        /// Gets the class cleanup method.
        /// </summary>
        public MethodInfo ClassCleanupMethod
        {
            get
            {
                return this.classCleanupMethod;
            }

            internal set
            {
                if (this.classCleanupMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClassClean, this.ClassType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.classCleanupMethod = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this class info has a executable cleanup method.
        /// </summary>
        public bool HasExecutableCleanupMethod
        {
            get
            {
                // If no class cleanup, then continue with the next one.
                if (this.ClassCleanupMethod == null)
                {
                    return false;
                }

                // If class initialization was successful, then only call class cleanup.
                if (this.ClassInitializationException != null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the test initialize method.
        /// </summary>
        public MethodInfo TestInitializeMethod
        {
            get
            {
                return this.testInitializeMethod;
            }

            internal set
            {
                if (this.testInitializeMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiInit, this.ClassType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.testInitializeMethod = value;
            }
        }

        /// <summary>
        /// Gets the test cleanup method.
        /// </summary>
        public MethodInfo TestCleanupMethod
        {
            get
            {
                return this.testCleanupMethod;
            }

            internal set
            {
                if (this.testCleanupMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiClean, this.ClassType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.testCleanupMethod = value;
            }
        }

        /// <summary>
        /// Gets a queue of test initialize methods to call for this type.
        /// </summary>
        public Queue<MethodInfo> BaseTestInitializeMethodsQueue { get; private set; }

        /// <summary>
        /// Gets a queue of test cleanup methods to call for this type.
        /// </summary>
        public Queue<MethodInfo> BaseTestCleanupMethodsQueue { get; private set; }

        /// <summary>
        /// Runs the class initialize method.
        /// </summary>
        /// <param name="testContext"> The test context. </param>
        /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public void RunClassInitialize(TestContext testContext)
        {
            // If no class initialize return
            if (this.ClassInitializeMethod == null)
            {
                return;
            }

            if (testContext == null)
            {
                throw new NullReferenceException(Resource.TestContextIsNull);
            }

            lock (this.testClassExecuteSyncObject)
            {
                // If class initialization is not done, then do it.
                if (!this.IsClassInitializeExecuted)
                {
                    try
                    {
                        this.ClassInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                    }
                    catch (Exception ex)
                    {
                        this.ClassInitializationException = ex;
                    }
                    finally
                    {
                        this.IsClassInitializeExecuted = true;
                    }
                }
            }

            // If classInitialization was successful, then dont do anything
            if (this.ClassInitializationException == null)
            {
                return;
            }

            if (this.ClassInitializationException is TestFailedException)
            {
                throw this.ClassInitializationException;
            }

            // Fail the current test if it was a failure.
            var realException = this.ClassInitializationException.InnerException ?? this.ClassInitializationException;

            var outcome = UnitTestOutcome.Failed;
            string errorMessage = null;
            StackTraceInformation exceptionStackTraceInfo = null;
            if (!realException.TryGetUnitTestAssertException(out outcome, out errorMessage, out exceptionStackTraceInfo))
            {
                errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_ClassInitMethodThrows,
                    this.ClassType.FullName,
                    this.ClassInitializeMethod.Name,
                    realException.GetType().ToString(),
                    StackTraceHelper.GetExceptionMessage(realException));

                exceptionStackTraceInfo = realException.TryGetStackTraceInformation();
            }

            var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo);
            this.ClassInitializationException = testFailedException;

            throw testFailedException;
        }

        /// <summary>
        /// Run class cleanup methods
        /// </summary>
        /// <returns>
        /// Any exception that can be thrown as part of a class cleanup as warning messages.
        /// </returns>
        [SuppressMessageAttribute("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public string RunClassCleanup()
        {
            if (this.ClassCleanupMethod == null)
            {
                return null;
            }

            lock (this.testClassExecuteSyncObject)
            {
                try
                {
                    this.ClassCleanupMethod.InvokeAsSynchronousTask(null);

                    return null;
                }
                catch (Exception exception)
                {
                    var realException = exception.InnerException ?? exception;

                    string errorMessage;

                    // special case AssertFailedException to trim off part of the stack trace
                    if (realException is AssertFailedException ||
                        realException is AssertInconclusiveException)
                    {
                        errorMessage = realException.Message;
                    }
                    else
                    {
                        errorMessage = StackTraceHelper.GetExceptionMessage(realException);
                    }

                    return string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_ClassCleanupMethodWasUnsuccesful,
                        this.ClassType.Name,
                        this.ClassCleanupMethod.Name,
                        errorMessage,
                        StackTraceHelper.GetStackTraceInformation(realException)?.ErrorStackTrace);
                }
            }
        }
    }
}