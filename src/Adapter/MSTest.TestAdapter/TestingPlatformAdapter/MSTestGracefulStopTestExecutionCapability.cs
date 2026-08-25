// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestGracefulStopTestExecutionCapability : IGracefulStopTestExecutionResultCapability
{
#pragma warning disable IDE0330 // Use 'System.Threading.Lock' - not available on all target frameworks of this project.
    private static readonly object Sync = new();
#pragma warning restore IDE0330
    private static int s_activeExecutionCount;
    private ExecutionState _executionState;
    private bool _isStopRequested;

    private MSTestGracefulStopTestExecutionCapability()
    {
    }

    public static MSTestGracefulStopTestExecutionCapability Instance { get; } = new();

    internal static MSTestGracefulStopTestExecutionCapability Create() => new();

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        lock (Sync)
        {
            _isStopRequested = true;
            PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryStopTestExecutionAsync(CancellationToken cancellationToken)
        => Task.FromResult(TryRequestGracefulStop());

    internal void NotifyTestExecutionPending()
    {
        lock (Sync)
        {
            _isStopRequested = false;
            _executionState = ExecutionState.Pending;
        }
    }

    internal void NotifyTestExecutionStarting()
    {
        lock (Sync)
        {
            // Preserve a stop accepted while the run was pending. Otherwise this is a new run, so clear
            // the process-wide engine flag left by a previous request, but only when no overlapping run
            // still owns that flag. Discovery never reaches this path.
            if (_isStopRequested)
            {
                PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
            }
            else if (s_activeExecutionCount == 0)
            {
                PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
            }

            RegisterActiveExecution();
            _executionState = ExecutionState.Active;
        }
    }

    internal void NotifyTestExecutionCompleted()
    {
        lock (Sync)
        {
            if (_executionState == ExecutionState.Active)
            {
                UnregisterActiveExecution();
            }

            _executionState = ExecutionState.Completed;
        }
    }

    private bool TryRequestGracefulStop()
    {
        lock (Sync)
        {
            if (_executionState == ExecutionState.Completed || _isStopRequested)
            {
                return false;
            }

            _isStopRequested = true;
            PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
            return true;
        }
    }

    private static void RegisterActiveExecution()
        => s_activeExecutionCount++;

    private static void UnregisterActiveExecution()
        => s_activeExecutionCount--;

    private enum ExecutionState
    {
        Pending,
        Active,
        Completed,
    }
}
#endif
