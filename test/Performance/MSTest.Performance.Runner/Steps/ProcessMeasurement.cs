// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace MSTest.Performance.Runner.Steps;

internal static class ProcessMeasurement
{
    private static readonly TimeSpan SamplingInterval = TimeSpan.FromMilliseconds(10);

    public static async Task<TimeSpan> WaitForExitAndSampleTotalProcessorTimeAsync(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            await process.WaitForExitAsync();
            return process.TotalProcessorTime;
        }

        // Unix removes process metrics when a child is reaped, so retain the latest live sample.
        // The final CPU slice between the last sample and exit cannot be observed through Process.
        TimeSpan totalProcessorTime = TimeSpan.Zero;
        TryUpdateTotalProcessorTime(process, ref totalProcessorTime);

        Task waitForExitTask = process.WaitForExitAsync();
        while (!waitForExitTask.IsCompleted)
        {
            await Task.WhenAny(waitForExitTask, Task.Delay(SamplingInterval));
            TryUpdateTotalProcessorTime(process, ref totalProcessorTime);
        }

        await waitForExitTask;
        return totalProcessorTime;
    }

    private static void TryUpdateTotalProcessorTime(Process process, ref TimeSpan totalProcessorTime)
    {
        try
        {
            totalProcessorTime = process.TotalProcessorTime;
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // Retain the last sample because Unix removes process metrics after reaping.
        }
        catch (Win32Exception) when (process.HasExited)
        {
            // Retain the last sample because Unix removes process metrics after reaping.
        }
    }
}
