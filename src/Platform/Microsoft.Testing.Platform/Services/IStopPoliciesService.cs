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
    /// deadline-truncated. It is monotonic: it only ever goes from <see langword="false"/> to
    /// <see langword="true"/>, so it can be read without synchronization.
    /// </remarks>
    bool IsTestExecutionCompleted { get; }

    /// <summary>
    /// Records that test execution has finished. Called by the host the moment the test framework invoker
    /// returns, before any reporting or message-bus draining starts.
    /// </summary>
    void NotifyTestExecutionCompleted();

    Task RegisterOnMaxFailedTestsCallbackAsync(Func<int, CancellationToken, Task> callback);

    Task RegisterOnAbortCallbackAsync(Func<Task> callback);

    Task RegisterOnDeadlineCallbackAsync(Func<Task> callback);

    Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken);

    Task ExecuteAbortCallbacksAsync();

    Task ExecuteDeadlineCallbacksAsync();
}
