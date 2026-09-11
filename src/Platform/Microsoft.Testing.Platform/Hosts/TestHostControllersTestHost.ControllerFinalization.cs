// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private CancellationTokenSource EnsureControllerFinalizationCancellationTokenSource()
    {
        var candidate = new CancellationTokenSource();
        CancellationTokenSource? existing =
            Interlocked.CompareExchange(ref _controllerFinalizationCancellationTokenSource, candidate, null);
        if (existing is null)
        {
            return candidate;
        }

        candidate.Dispose();
        return existing;
    }

    private void ArmControllerFinalizationTimeout()
    {
        if (Interlocked.Exchange(ref _controllerFinalizationTimeoutArmed, 1) == 0)
        {
            EnsureControllerFinalizationCancellationTokenSource().CancelAfter(_controllerExtensionFinalizationTimeout);
        }
    }

    private void RegisterControllerFinalizationTransition(CancellationToken applicationCancellationToken)
        => _controllerFinalizationTransitionRegistration = applicationCancellationToken.Register(
            static state => ((TestHostControllersTestHost)state!).ArmControllerFinalizationTimeout(),
            this);

    private void ScheduleFinalizationTimeoutWarning()
    {
        if (Interlocked.Exchange(ref _finalizationTimeoutWarningScheduled, 1) != 0)
        {
            return;
        }

        // The cleanup deadline has expired, so logging cannot be awaited without making the deadline unbounded
        // again. Schedule it independently and observe a later provider fault.
        ObserveBackgroundTask(Task.Run(
            () => _logger.LogWarning(
                $"Test host controller extension finalization exceeded the {_controllerExtensionFinalizationTimeout} cleanup timeout."),
            CancellationToken.None));
    }

    private static void ObserveBackgroundTask(Task task)
        => _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static async Task<bool> TryRunControllerExtensionAsync(
        Func<CancellationToken, Task> finalization,
        CancellationToken cancellationToken)
    {
        try
        {
            // Invoke on the thread pool before applying the external cancellation wrapper. An extension can
            // block synchronously before returning its Task; running the delegate inline would prevent us from
            // ever reaching WithCancellationAsync and make the supposedly bounded cleanup wait unbounded.
            await Task.Run(() => finalization(cancellationToken), CancellationToken.None)
                .WithCancellationAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static void MarkOutputDeviceStillRunning(List<object> servicesStillRunning, ProxyOutputDevice outputDevice)
    {
        if (!servicesStillRunning.Contains(outputDevice))
        {
            servicesStillRunning.Add(outputDevice);
        }

        if (!servicesStillRunning.Contains(outputDevice.OriginalOutputDevice))
        {
            servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
        }
    }
}
