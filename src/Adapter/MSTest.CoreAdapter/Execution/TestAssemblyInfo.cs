// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;
    using Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ObjectModel;
    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

    /// <summary>
    /// Defines TestAssembly Info object
    /// </summary>
    public class TestAssemblyInfo
    {
        private MethodInfo assemblyCleanupMethod;

        private MethodInfo assemblyInitializeMethod;
        private object assemblyInfoExecuteSyncObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestAssemblyInfo"/> class.
        /// </summary>
        internal TestAssemblyInfo()
        {
            this.assemblyInfoExecuteSyncObject = new object();
        }

        /// <summary>
        /// Gets <c>AssemblyInitialize</c> method for the assembly.
        /// </summary>
        public MethodInfo AssemblyInitializeMethod
        {
            get
            {
                return this.assemblyInitializeMethod;
            }

            internal set
            {
                if (this.assemblyInitializeMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyInit, this.assemblyInitializeMethod.DeclaringType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.assemblyInitializeMethod = value;
            }
        }

        /// <summary>
        /// Gets <c>AssemblyCleanup</c> method for the assembly.
        /// </summary>
        public MethodInfo AssemblyCleanupMethod
        {
            get
            {
                return this.assemblyCleanupMethod;
            }

            internal set
            {
                if (this.assemblyCleanupMethod != null)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyClean, this.assemblyCleanupMethod.DeclaringType.FullName);
                    throw new TypeInspectionException(message);
                }

                this.assemblyCleanupMethod = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether <c>AssemblyInitialize</c> has been executed.
        /// </summary>
        public bool IsAssemblyInitializeExecuted { get; internal set; }

        /// <summary>
        /// Gets the assembly initialization exception.
        /// </summary>
        public Exception AssemblyInitializationException { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this assembly has an executable <c>AssemblyCleanup</c> method.
        /// </summary>
        public bool HasExecutableCleanupMethod
        {
            get
            {
                // If no assembly cleanup, then continue with the next one.
                if (this.AssemblyCleanupMethod == null)
                {
                    return false;
                }

                // If assembly initialization was successful, then only call assembly cleanup.
                if (this.AssemblyInitializationException != null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Runs assembly initialize method.
        /// </summary>
        /// <param name="testContext"> The test context. </param>
        /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public void RunAssemblyInitialize(TestContext testContext)
        {
            // No assembly initialize => nothing to do.
            if (this.AssemblyInitializeMethod == null)
            {
                return;
            }

            if (testContext == null)
            {
                throw new NullReferenceException(Resource.TestContextIsNull);
            }

            lock (this.assemblyInfoExecuteSyncObject)
            {
                // If assembly initialization is not done, then do it.
                if (!this.IsAssemblyInitializeExecuted)
                {
                    try
                    {
                        this.AssemblyInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                    }
                    catch (Exception ex)
                    {
                        this.AssemblyInitializationException = ex;
                    }
                    finally
                    {
                        this.IsAssemblyInitializeExecuted = true;
                    }
                }
            }

            // If assemblyInitialization was successful, then dont do anything
            if (this.AssemblyInitializationException == null)
            {
                return;
            }

            // Cache and return an already created TestFailedException.
            if (this.AssemblyInitializationException is TestFailedException)
            {
                throw this.AssemblyInitializationException;
            }

            var realException = this.AssemblyInitializationException.InnerException
                                ?? this.AssemblyInitializationException;

            var outcome = UnitTestOutcome.Failed;
            string errorMessage = null;
            StackTraceInformation stackTraceInfo = null;
            if (!realException.TryGetUnitTestAssertException(out outcome, out errorMessage, out stackTraceInfo))
            {
                var exception = realException.GetType().ToString();
                var message = StackTraceHelper.GetExceptionMessage(realException);
                errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_AssemblyInitMethodThrows,
                    this.AssemblyInitializeMethod.DeclaringType.FullName,
                    this.AssemblyInitializeMethod.Name,
                    exception,
                    message);
                stackTraceInfo = StackTraceHelper.GetStackTraceInformation(realException);
            }

            var testFailedException = new TestFailedException(outcome, errorMessage, stackTraceInfo);
            this.AssemblyInitializationException = testFailedException;

            throw testFailedException;
        }

        /// <summary>
        /// Run assembly cleanup methods
        /// </summary>
        /// <returns>
        /// Any exception that can be thrown as part of a assembly cleanup as warning messages.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public string RunAssemblyCleanup()
        {
            if (this.AssemblyCleanupMethod == null)
            {
                return null;
            }

            lock (this.assemblyInfoExecuteSyncObject)
            {
                try
                {
                    this.AssemblyCleanupMethod.InvokeAsSynchronousTask(null);

                    return null;
                }
                catch (Exception ex)
                {
                    var realException = ex.InnerException ?? ex;

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
                        Resource.UTA_AssemblyCleanupMethodWasUnsuccesful,
                        this.AssemblyCleanupMethod.DeclaringType.Name,
                        this.AssemblyCleanupMethod.Name,
                        errorMessage,
                        StackTraceHelper.GetStackTraceInformation(realException)?.ErrorStackTrace);
                }
            }
        }
    }
}