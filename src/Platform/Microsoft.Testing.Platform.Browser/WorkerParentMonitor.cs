// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class WorkerParentMonitor
{
    private static readonly TimeSpan ParentExitCleanupTimeout = TimeSpan.FromSeconds(10);

    public static void Start(CancellationTokenSource cancellationTokenSource)
    {
        var thread = new Thread(() => MonitorParent(cancellationTokenSource))
        {
            IsBackground = true,
            Name = "MTP browser parent monitor",
        };
        thread.Start();
    }

    private static void MonitorParent(CancellationTokenSource cancellationTokenSource)
    {
        while (Console.In.ReadLine() is { } command)
        {
            if (command == BrowserSupervisor.CancelWorkerCommand)
            {
                TryCancel(cancellationTokenSource);
            }
        }

        TryCancel(cancellationTokenSource);
        if (!OperatingSystem.IsWindows())
        {
            Thread.Sleep(ParentExitCleanupTimeout);
            WorkerProcessContainment.KillCurrentProcessGroup();
        }
    }

    private static void TryCancel(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
