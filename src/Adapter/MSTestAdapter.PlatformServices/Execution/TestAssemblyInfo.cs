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
    /// <para>
    /// Reads and writes use <see cref="Volatile"/> because this flag acts as the fast-path
    /// guard that lets callers bypass <see cref="_assemblyInfoExecuteSyncSemaphore"/>. The
    /// release semantics on the publishing write ensure that the prior
    /// <see cref="PostAssemblyInitProperties"/> snapshot publication is also visible to any
    /// reader that observes this flag as <see langword="true"/> on the fast path.
    /// </para>
    /// </summary>
    public bool IsAssemblyInitializeExecuted
    {
        get => Volatile.Read(ref field);
        internal set => Volatile.Write(ref field, value);
    }

    /// <summary>
    /// Gets or sets the assembly initialization exception.
    /// </summary>
    public TestFailedException? AssemblyInitializationException { get; internal set; }

    /// <summary>
    /// Gets a snapshot of <see cref="TestContext.Properties"/> captured after the
    /// <c>AssemblyInitialize</c> method completes. Used to flow properties set during
    /// <c>AssemblyInitialize</c> into subsequent contexts (class init, test execution,
    /// class cleanup, assembly cleanup). <see langword="null"/> if no
    /// <c>AssemblyInitialize</c> method was registered or it has not yet executed
    /// successfully.
    /// <para>
    /// The snapshot is shallow: reference-type values stored in the bag are shared (aliased)
    /// across every context the snapshot is merged into. Mutations of those reference-type
    /// instances are visible everywhere.
    /// </para>
    /// <para>
    /// Class-init properties are intentionally NOT included by callers when seeding the
    /// assembly-cleanup context, because <c>AssemblyCleanup</c> is assembly-scoped and runs
    /// once across many classes; including a single class's snapshot would be arbitrary.
    /// </para>
    /// <para>
    /// Reads and writes use <see cref="Volatile"/> so that callers on the
    /// <see cref="IsAssemblyInitializeExecuted"/> fast path (which intentionally bypasses
    /// <see cref="_assemblyInfoExecuteSyncSemaphore"/>) safely observe the snapshot published
    /// by the thread that ran <c>AssemblyInitialize</c>. The publishing thread writes this
    /// snapshot before writing <see cref="IsAssemblyInitializeExecuted"/>, and both writes go
    /// through <see cref="Volatile"/>, so any reader that observes
    /// <see cref="IsAssemblyInitializeExecuted"/> as <see langword="true"/> is guaranteed to
    /// also see the published snapshot.
    /// </para>
    /// </summary>
    internal IReadOnlyDictionary<string, object?>? PostAssemblyInitProperties
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

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

                                // The `is` check is defensive: this method is part of an internal
                                // but mockable surface, so unit tests can legitimately pass a
                                // mocked TestContext. Production callers always pass a
                                // TestContextImplementation.
                                if (testContext is TestContextImplementation testContextImpl)
                                {
                                    // Capture a snapshot of TestContext.Properties so that values
                                    // set during AssemblyInitialize flow to subsequent contexts
                                    // (class init, test execution, class cleanup, assembly cleanup).
                                    // PostAssemblyInitProperties uses Volatile.Read/Write so that
                                    // callers on the IsAssemblyInitializeExecuted fast path
                                    // (which bypasses _assemblyInfoExecuteSyncSemaphore) safely
                                    // observe the published snapshot.
                                    PostAssemblyInitProperties = testContextImpl.CaptureLifecycleProperties();
                                }
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
