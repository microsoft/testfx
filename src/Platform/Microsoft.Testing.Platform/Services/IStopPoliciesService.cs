// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal interface IStopPoliciesService
{
    bool IsMaxFailedTestsTriggered { get; }

    bool IsAbortTriggered { get; }

    bool IsDeadlineTriggered { get; }

    /// <summary>
    /// Gets a value indicating whether test execution has finished, meaning the test framework invoker has
    /// returned and only reporting and teardown remain.
    /// </summary>
    /// <remarks>
    /// This is the gate that stops a deadline elapsing during reporting from marking an already-finished run as
    /// deadline-truncated. Server mode resets the gate before each execution request because the service is
    /// application-scoped.
    /// </remarks>
    bool IsTestExecutionCompleted { get; }

    /// <summary>
    /// Records that a test execution request is starting.
    /// </summary>
    /// <remarks>
    /// Server mode reuses this application-scoped service across requests. A discovery request can therefore
    /// complete before the run request starts, and the run must clear the completion gate before arming its
    /// deadline.
    /// </remarks>
    void NotifyTestExecutionStarting();

    /// <summary>
    /// Records that test execution has finished. Called by the host the moment the test framework invoker
    /// returns, before any reporting or message-bus draining starts.
    /// </summary>
    void NotifyTestExecutionCompleted();

    /// <summary>
    /// Clears the deadline-triggered outcome recorded by <see cref="ExecuteDeadlineCallbacksAsync"/>.
    /// </summary>
    /// <remarks>
    /// The deadline outcome has to be recorded before the graceful stop is requested, because the stop can let
    /// the framework finalize the session (and compute the exit code) immediately. When the stop request is then
    /// rejected, nothing was truncated on the platform's account, so the outcome has to be taken back: otherwise
    /// a run that goes on to execute every test still reports
    /// <see cref="Helpers.ExitCode.TestExecutionStoppedAtDeadline"/>.
    /// </remarks>
    void RevertDeadlineTriggered();

    Task RegisterOnMaxFailedTestsCallbackAsync(Func<int, CancellationToken, Task> callback);

    Task RegisterOnAbortCallbackAsync(Func<Task> callback);

    Task RegisterOnDeadlineCallbackAsync(Func<Task> callback);

    Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken);

    Task ExecuteAbortCallbacksAsync();

    Task ExecuteDeadlineCallbacksAsync();
}
