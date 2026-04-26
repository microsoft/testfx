// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines TestAssembly Info object.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - not important to dispose the SemaphoreSlim, we don't access AvailableWaitHandle.
internal sealed class TestAssemblyInfo
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly SemaphoreSlim _assemblyInfoExecuteSyncSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAssemblyInfo"/> class.
    /// </summary>
    /// <param name="assembly">Sets the <see cref="Assembly"/> this class is representing. </param>
    internal TestAssemblyInfo(Assembly assembly)
    {
        Assembly = assembly;
        DiscoversInternals = assembly.GetCustomAttribute<DiscoverInternalsAttribute>() is not null;
    }

    internal bool DiscoversInternals { get; }

    internal List<(MethodInfo Method, TimeoutInfo? TimeoutInfo)> GlobalTestInitializations { get; } = [];

    internal List<(MethodInfo Method, TimeoutInfo? TimeoutInfo)> GlobalTestCleanups { get; } = [];

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
    public TestFailedException? AssemblyInitializationException { get; internal set; }

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
    /// <param name="testContext">The test context.</param>
    /// <returns>
    /// A <see cref="TestResult"/> whose <see cref="TestResult.Outcome"/> is <see cref="UnitTestOutcome.Passed"/>
    /// when the assembly initialization succeeds, or the failure outcome with
    /// <see cref="TestResult.TestFailureException"/> set when the initialization fails.
    /// </returns>
    public async Task<TestResult> RunAssemblyInitializeAsync(TestContext testContext)
    {
        // No assembly initialize => nothing to do.
        if (AssemblyInitializeMethod == null)
        {
            IsAssemblyInitializeExecuted = true;
            return new TestResult { Outcome = UnitTestOutcome.Passed };
        }

        // If assembly initialization is not done, then do it.
        if (!IsAssemblyInitializeExecuted)
        {
            // Acquiring a lock is usually a costly operation which does not need to be
            // performed every time if the assembly initialization is already executed.
            try
            {
                await _assemblyInfoExecuteSyncSemaphore.WaitAsync().ConfigureAwait(false);
                // Perform a check again.
                if (!IsAssemblyInitializeExecuted)
                {
                    try
                    {
                        AssemblyInitializationException = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                            async () =>
                            {
                                // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                                // It's safer to reset it before the capture.
                                using (TestContextImplementation.SetCurrentTestContext(testContext))
                                {
                                    Task? task = AssemblyInitializeMethod.GetInvokeResultAsync(null, testContext);
                                    if (task is not null)
                                    {
                                        await task.ConfigureAwait(false);
                                    }
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
                            Resource.AssemblyInitializeTimedOut).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AssemblyInitializationException = GetTestFailedExceptionFromAssemblyInitializeException(ex, AssemblyInitializeMethod);
                    }
                    finally
                    {
                        IsAssemblyInitializeExecuted = true;
                    }
                }
            }
            finally
            {
                _assemblyInfoExecuteSyncSemaphore.Release();
            }
        }

        // If assemblyInitialization was successful, then don't do anything
        return AssemblyInitializationException is null
            ? new TestResult { Outcome = UnitTestOutcome.Passed }
            : new TestResult { TestFailureException = AssemblyInitializationException, Outcome = AssemblyInitializationException.Outcome };
    }

    private static TestFailedException GetTestFailedExceptionFromAssemblyInitializeException(Exception ex, MethodInfo assemblyInitializeMethod)
    {
        Exception realException = ex.GetRealException();

        UnitTestOutcome outcome = realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetFormattedExceptionMessage(realException) as it prefixes the message with the exception type name.
        string exceptionMessage = realException.TryGetMessage();
        DebugEx.Assert(assemblyInitializeMethod.DeclaringType?.FullName is not null, "AssemblyInitializeMethod.DeclaringType.FullName is null");
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_AssemblyInitMethodThrows,
            assemblyInitializeMethod.DeclaringType.FullName,
            assemblyInitializeMethod.Name,
            realException.GetType().ToString(),
            exceptionMessage);
        StackTraceInformation? exceptionStackTraceInfo = realException.GetStackTraceInformation();

        return new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
    }

    /// <summary>
    /// Calls the assembly cleanup method in a thread-safe.
    /// </summary>
    internal async Task<TestFailedException?> ExecuteAssemblyCleanupAsync(TestContext testContext)
    {
        if (AssemblyCleanupMethod == null)
        {
            return null;
        }

        try
        {
            await _assemblyInfoExecuteSyncSemaphore.WaitAsync().ConfigureAwait(false);
            AssemblyCleanupException = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                 async () =>
                 {
                     // NOTE: It's unclear what the effect is if we reset the current test context before vs after the capture.
                     // It's safer to reset it before the capture.
                     using (TestContextImplementation.SetCurrentTestContext(testContext))
                     {
                         Task? task = AssemblyCleanupMethod.GetParameters().Length == 0
                             ? AssemblyCleanupMethod.GetInvokeResultAsync(null)
                             : AssemblyCleanupMethod.GetInvokeResultAsync(null, testContext);
                         if (task is not null)
                         {
                             await task.ConfigureAwait(false);
                         }
                     }

                     ExecutionContext = ExecutionContext.Capture();
                 },
                 testContext.CancellationTokenSource,
                 AssemblyCleanupMethodTimeoutMilliseconds,
                 AssemblyCleanupMethod,
                 ExecutionContext,
                 Resource.AssemblyCleanupWasCancelled,
                 Resource.AssemblyCleanupTimedOut).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AssemblyCleanupException = ex;
        }
        finally
        {
            _assemblyInfoExecuteSyncSemaphore.Release();
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
