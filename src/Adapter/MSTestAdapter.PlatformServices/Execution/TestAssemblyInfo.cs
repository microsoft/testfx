// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines TestAssembly Info object.
/// </summary>
internal sealed class TestAssemblyInfo
{
    internal readonly struct MethodInfoWithTimeoutInfo
    {
        public MethodInfoWithTimeoutInfo(MethodInfo methodInfo, TimeoutInfo? timeoutInfo)
        {
            MethodInfo = methodInfo;
            TimeoutInfo = timeoutInfo;
        }

        public MethodInfo MethodInfo { get; }

        public TimeoutInfo? TimeoutInfo { get; }

        public void Deconstruct(out MethodInfo methodInfo, out TimeoutInfo? timeoutInfo)
        {
            methodInfo = MethodInfo;
            timeoutInfo = TimeoutInfo;
        }
    }

    private readonly Lock _assemblyInfoExecuteSyncObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAssemblyInfo"/> class.
    /// </summary>
    /// <param name="assembly">Sets the <see cref="Assembly"/> this class is representing. </param>
    internal TestAssemblyInfo(Assembly assembly)
        => Assembly = assembly;

    internal List<MethodInfoWithTimeoutInfo> GlobalTestInitializations { get; } = [];

    internal List<MethodInfoWithTimeoutInfo> GlobalTestCleanups { get; } = [];

    /// <summary>
    /// Gets or sets <c>AssemblyInitialize</c> method for the assembly.
    /// </summary>
    public MethodInfo? AssemblyInitializeMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                DebugEx.Assert(field.DeclaringType?.FullName is not null, "AssemblyInitializeMethod.DeclaringType.FullName is null");
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyInit, field.DeclaringType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the AssemblyInitializeMethod timeout.
    /// </summary>
    internal TimeoutInfo? AssemblyInitializeMethodTimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the AssemblyCleanupMethod timeout.
    /// </summary>
    internal TimeoutInfo? AssemblyCleanupMethodTimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets <c>AssemblyCleanup</c> method for the assembly.
    /// </summary>
    public MethodInfo? AssemblyCleanupMethod
    {
        get;
        internal set
        {
            if (field != null)
            {
                DebugEx.Assert(field.DeclaringType?.FullName is not null, "AssemblyCleanupMethod.DeclaringType.FullName is null");
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyClean, field.DeclaringType.FullName);
                throw new TypeInspectionException(message);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether <c>AssemblyInitialize</c> has been executed.
    /// </summary>
    public bool IsAssemblyInitializeExecuted { get; internal set; }

    /// <summary>
    /// Gets or sets the assembly initialization exception.
    /// </summary>
    public Exception? AssemblyInitializationException { get; internal set; }

    /// <summary>
    /// Gets the assembly cleanup exception.
    /// </summary>
    internal Exception? AssemblyCleanupException { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this assembly has an executable <c>AssemblyCleanup</c> method.
    /// </summary>
    public bool HasExecutableCleanupMethod =>
            // If no assembly cleanup, then continue with the next one.
            AssemblyCleanupMethod != null;

    /// <summary>
    /// Gets the <see cref="Assembly"/> this class represents.
    /// </summary>
    internal Assembly Assembly { get; }

    internal ExecutionContext? ExecutionContext { get; set; }

    /// <summary>
    /// Runs assembly initialize method.
    /// </summary>
    /// <param name="testContext"> The test context. </param>
    /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
    public void RunAssemblyInitialize(TestContext testContext)
    {
        // No assembly initialize => nothing to do.
        if (AssemblyInitializeMethod == null)
        {
            IsAssemblyInitializeExecuted = true;
            return;
        }

        if (testContext == null)
        {
            // TODO: This exception should be of type ArgumentNullException
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new NullReferenceException(Resource.TestContextIsNull);
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        // If assembly initialization is not done, then do it.
        if (!IsAssemblyInitializeExecuted)
        {
            // Acquiring a lock is usually a costly operation which does not need to be
            // performed every time if the assembly initialization is already executed.
            lock (_assemblyInfoExecuteSyncObject)
            {
                // Perform a check again.
                if (!IsAssemblyInitializeExecuted)
                {
                    try
                    {
                        AssemblyInitializationException = FixtureMethodRunner.RunWithTimeoutAndCancellation(
                            () =>
                            {
                                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                                // It's safer to reset it before the capture.
                                using (TestContextImplementation.SetCurrentTestContext(testContext as TestContextImplementation))
                                {
                                    AssemblyInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                                }

                                // **After** we have executed the assembly initialize, we save the current context.
                                // This context will contain async locals set by the assembly initialize method.
                                ExecutionContext = ExecutionContext.Capture();
                            },
                            testContext.CancellationTokenSource,
                            AssemblyInitializeMethodTimeoutMilliseconds,
                            AssemblyInitializeMethod,
                            executionContext: ExecutionContext,
                            Resource.AssemblyInitializeWasCancelled,
                            Resource.AssemblyInitializeTimedOut);
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

        // If the exception is already a `TestFailedException` we throw it as-is
        if (AssemblyInitializationException is TestFailedException)
        {
            throw AssemblyInitializationException;
        }

        Exception realException = AssemblyInitializationException.GetRealException();

        UnitTestOutcome outcome = realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetFormattedExceptionMessage(realException) as it prefixes the message with the exception type name.
        string exceptionMessage = realException.TryGetMessage();
        DebugEx.Assert(AssemblyInitializeMethod.DeclaringType?.FullName is not null, "AssemblyInitializeMethod.DeclaringType.FullName is null");
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_AssemblyInitMethodThrows,
            AssemblyInitializeMethod.DeclaringType.FullName,
            AssemblyInitializeMethod.Name,
            realException.GetType().ToString(),
            exceptionMessage);
        StackTraceInformation? exceptionStackTraceInfo = realException.GetStackTraceInformation();

        var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
        AssemblyInitializationException = testFailedException;

        throw testFailedException;
    }

    /// <summary>
    /// Calls the assembly cleanup method in a thread-safe.
    /// </summary>
    /// <remarks>
    /// It is a replacement for RunAssemblyCleanup but as we are in a bug-fix version, we do not want to touch
    /// public API and so we introduced this method.
    /// </remarks>
    internal TestFailedException? ExecuteAssemblyCleanup(TestContext testContext)
    {
        if (AssemblyCleanupMethod == null)
        {
            return null;
        }

        lock (_assemblyInfoExecuteSyncObject)
        {
            try
            {
                AssemblyCleanupException = FixtureMethodRunner.RunWithTimeoutAndCancellation(
                     () =>
                     {
                         // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                         // It's safer to reset it before the capture.
                         using (TestContextImplementation.SetCurrentTestContext(testContext as TestContextImplementation))
                         {
                             if (AssemblyCleanupMethod.GetParameters().Length == 0)
                             {
                                 AssemblyCleanupMethod.InvokeAsSynchronousTask(null);
                             }
                             else
                             {
                                 AssemblyCleanupMethod.InvokeAsSynchronousTask(null, testContext);
                             }
                         }

                         ExecutionContext = ExecutionContext.Capture();
                     },
                     testContext.CancellationTokenSource,
                     AssemblyCleanupMethodTimeoutMilliseconds,
                     AssemblyCleanupMethod,
                     ExecutionContext,
                     Resource.AssemblyCleanupWasCancelled,
                     Resource.AssemblyCleanupTimedOut);
            }
            catch (Exception ex)
            {
                AssemblyCleanupException = ex;
            }
        }

        // If assemblyCleanup was successful, then don't do anything
        if (AssemblyCleanupException is null)
        {
            return null;
        }

        // If the exception is already a `TestFailedException` we throw it as-is
        if (AssemblyCleanupException is TestFailedException assemblyCleanupEx)
        {
            return assemblyCleanupEx;
        }

        Exception realException = AssemblyCleanupException.GetRealException();

        // special case AssertFailedException to trim off part of the stack trace
        string errorMessage = realException is AssertFailedException or AssertInconclusiveException
            ? realException.Message
            : realException.GetFormattedExceptionMessage();

        StackTraceInformation? exceptionStackTraceInfo = realException.GetStackTraceInformation();
        DebugEx.Assert(AssemblyCleanupMethod.DeclaringType?.Name is not null, "AssemblyCleanupMethod.DeclaringType.Name is null");

        return new TestFailedException(
            UnitTestOutcome.Failed,
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_AssemblyCleanupMethodWasUnsuccesful,
                AssemblyCleanupMethod.DeclaringType.Name,
                AssemblyCleanupMethod.Name,
                errorMessage,
                exceptionStackTraceInfo?.ErrorStackTrace),
            exceptionStackTraceInfo,
            realException);
    }
}
