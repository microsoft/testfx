// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestGracefulStopTestExecutionCapability : IGracefulStopTestExecutionResultCapability
{
    private static readonly object Sync = new();
    private static ExecutionState s_executionState;

    private MSTestGracefulStopTestExecutionCapability()
    {
    }

    public static MSTestGracefulStopTestExecutionCapability Instance { get; } = new();

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        lock (Sync)
        {
            PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryStopTestExecutionAsync(CancellationToken cancellationToken)
        => Task.FromResult(TryRequestGracefulStop());

    internal static void NotifyTestExecutionPending()
    {
        lock (Sync)
        {
            PlatformServiceProvider.Instance.IsGracefulStopRequested = false;
            s_executionState = ExecutionState.Pending;
        }
    }

    internal static void NotifyTestExecutionStarting()
    {
        lock (Sync)
        {
            s_executionState = ExecutionState.Active;
        }
    }

    internal static void NotifyTestExecutionCompleted()
    {
        lock (Sync)
        {
            s_executionState = ExecutionState.Completed;
        }
    }

    private static bool TryRequestGracefulStop()
    {
        lock (Sync)
        {
            if (s_executionState == ExecutionState.Completed
                || PlatformServiceProvider.Instance.IsGracefulStopRequested)
            {
                return false;
            }

            PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
            return true;
        }
    }

    private enum ExecutionState
    {
        Pending,
        Active,
        Completed,
    }
}
#endif
