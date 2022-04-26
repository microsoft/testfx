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
        private readonly object assemblyInfoExecuteSyncObject;

        private MethodInfo assemblyCleanupMethod;
        private MethodInfo assemblyInitializeMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestAssemblyInfo"/> class.
        /// </summary>
        /// <param name="assembly">Sets the <see cref="Assembly"/> this class is representing. </param>
        internal TestAssemblyInfo(Assembly assembly)
        {
            assemblyInfoExecuteSyncObject = new object();
            Assembly = assembly;
        }

        /// <summary>
        /// Gets <c>AssemblyInitialize</c> method for the assembly.
        /// </summary>
        public MethodInfo AssemblyInitializeMethod
        {
            get => assemblyInitializeMethod;

            internal set
            {
                if (assemblyInitializeMethod != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyInit, assemblyInitializeMethod.DeclaringType.FullName);
                    throw new TypeInspectionException(message);
                }

                assemblyInitializeMethod = value;
            }
        }

        /// <summary>
        /// Gets <c>AssemblyCleanup</c> method for the assembly.
        /// </summary>
        public MethodInfo AssemblyCleanupMethod
        {
            get => assemblyCleanupMethod;

            internal set
            {
                if (assemblyCleanupMethod != null)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyClean, assemblyCleanupMethod.DeclaringType.FullName);
                    throw new TypeInspectionException(message);
                }

                assemblyCleanupMethod = value;
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
                if (AssemblyCleanupMethod == null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the <see cref="Assembly"/> this class represents.
        /// </summary>
        internal Assembly Assembly { get; }

        /// <summary>
        /// Runs assembly initialize method.
        /// </summary>
        /// <param name="testContext"> The test context. </param>
        /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public void RunAssemblyInitialize(TestContext testContext)
        {
            // No assembly initialize => nothing to do.
            if (AssemblyInitializeMethod == null)
            {
                return;
            }

            if (testContext == null)
            {
                throw new NullReferenceException(Resource.TestContextIsNull);
            }

            // If assembly initialization is not done, then do it.
            if (!IsAssemblyInitializeExecuted)
            {
                // Acquiring a lock is usually a costly operation which does not need to be
                // performed every time if the assembly init is already executed.
                lock (assemblyInfoExecuteSyncObject)
                {
                    // Perform a check again.
                    if (!IsAssemblyInitializeExecuted)
                    {
                        try
                        {
                            AssemblyInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                        }
                        catch (Exception ex)
                        {
                            AssemblyInitializationException = ex;
                        }
                        finally
                        {
                            IsAssemblyInitializeExecuted = true;
                        }
                    }
                }
            }

            // If assemblyInitialization was successful, then don't do anything
            if (AssemblyInitializationException == null)
            {
                return;
            }

            // Cache and return an already created TestFailedException.
            if (AssemblyInitializationException is TestFailedException)
            {
                throw AssemblyInitializationException;
            }

            var realException = AssemblyInitializationException.InnerException
                                ?? AssemblyInitializationException;

            var outcome = UnitTestOutcome.Failed;
            if (!realException.TryGetUnitTestAssertException(out outcome, out var errorMessage, out var stackTraceInfo))
            {
                var exception = realException.GetType().ToString();
                var message = StackTraceHelper.GetExceptionMessage(realException);
                errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_AssemblyInitMethodThrows,
                    AssemblyInitializeMethod.DeclaringType.FullName,
                    AssemblyInitializeMethod.Name,
                    exception,
                    message);
                stackTraceInfo = StackTraceHelper.GetStackTraceInformation(realException);
            }

            var testFailedException = new TestFailedException(outcome, errorMessage, stackTraceInfo);
            AssemblyInitializationException = testFailedException;

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
            if (AssemblyCleanupMethod == null)
            {
                return null;
            }

            lock (assemblyInfoExecuteSyncObject)
            {
                try
                {
                    AssemblyCleanupMethod.InvokeAsSynchronousTask(null);

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
                        AssemblyCleanupMethod.DeclaringType.Name,
                        AssemblyCleanupMethod.Name,
                        errorMessage,
                        StackTraceHelper.GetStackTraceInformation(realException)?.ErrorStackTrace);
                }
            }
        }
    }
}
