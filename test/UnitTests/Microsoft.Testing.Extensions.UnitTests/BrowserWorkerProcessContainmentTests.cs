// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using Microsoft.Testing.Platform.Browser;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class BrowserWorkerProcessContainmentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WindowsJob_CloseKillsWorkerAndSpawnedChild()
    {
        string directory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-job-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, "pids.txt");
        string script = Path.Combine(directory, "worker.ps1");
        File.WriteAllText(
            script,
            $$"""
            $null = [Console]::In.ReadLine()
            $child = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList '-NoProfile', '-Command', 'Start-Sleep -Seconds 300' -PassThru
            "$PID`n$($child.Id)" | Set-Content -LiteralPath '{{marker}}'
            Start-Sleep -Seconds 300
            """);

        using var containment = WorkerProcessContainment.Create();
        using Process worker = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new AssertFailedException("The containment worker did not start.");

        int childProcessId = 0;
        try
        {
            containment.Attach(worker);
            await worker.StandardInput.WriteLineAsync(
                "start".AsMemory(),
                TestContext.CancellationToken);
            await worker.StandardInput.FlushAsync(TestContext.CancellationToken);

            using var markerTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.CancellationToken);
            markerTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (!File.Exists(marker))
            {
                await Task.Delay(50, markerTimeout.Token);
            }

            string[] processIds = File.ReadAllLines(marker);
            Assert.HasCount(2, processIds);
            Assert.AreEqual(worker.Id, int.Parse(processIds[0], CultureInfo.InvariantCulture));
            childProcessId = int.Parse(processIds[1], CultureInfo.InvariantCulture);

            containment.Dispose();

            await AssertProcessExitedAsync(worker.Id, TestContext.CancellationToken);
            await AssertProcessExitedAsync(childProcessId, TestContext.CancellationToken);
        }
        finally
        {
            TryKill(worker.Id);
            TryKill(childProcessId);
        }
    }

    private static async Task AssertProcessExitedAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (IsProcessRunning(processId))
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryKill(int processId)
    {
        if (processId == 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
        }
    }
}
#endif
