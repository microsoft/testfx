// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal interface IStopPoliciesService
{
    bool IsMaxFailedTestsTriggered { get; }

    bool IsAbortTriggered { get; }

    bool IsDeadlineTriggered { get; }

    Task RegisterOnMaxFailedTestsCallbackAsync(Func<int, CancellationToken, Task> callback);

    Task RegisterOnAbortCallbackAsync(Func<Task> callback);

    Task RegisterOnDeadlineCallbackAsync(Func<Task> callback);

    Task ExecuteMaxFailedTestsCallbacksAsync(int maxFailedTests, CancellationToken cancellationToken);

    Task ExecuteAbortCallbacksAsync();

    Task ExecuteDeadlineCallbacksAsync();

    /// <summary>
    /// Undoes the verdict set by <see cref="ExecuteDeadlineCallbacksAsync"/> when the graceful stop it was
    /// meant to precede could not be requested, so a run that was never truncated does not report
    /// <see cref="Helpers.ExitCode.TestExecutionStoppedAtDeadline"/>. Callbacks that already ran are not
    /// undone; only the verdict is, and the callbacks stay one-shot: a later
    /// <see cref="ExecuteDeadlineCallbacksAsync"/> still does nothing, and a later
    /// <see cref="RegisterOnDeadlineCallbackAsync"/> is still invoked immediately rather than queued.
    /// </summary>
    void RevertDeadlineTrigger();
}
