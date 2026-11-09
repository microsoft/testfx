// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerProcessCompatibilityTests
{
    internal const string ReadyMarker = "analyzer-wait-ready";

    // Allow process startup and teardown on loaded CI workers; successful completion uses a handshake, not elapsed time.
    private const int StageTimeoutMilliseconds = 60_000;

    [TestMethod]
    [DataRow(false, false, DisplayName = "AnalyzerWaitForRunningProcess")]
    [DataRow(true, false, DisplayName = "CodeFixWaitForRunningProcess")]
    [DataRow(false, true, DisplayName = "AnalyzerAlreadyExitedProcess")]
    [DataRow(true, true, DisplayName = "CodeFixAlreadyExitedProcess")]
    public async Task WaitForExit_ReleasedOrAlreadyExitedChild_PreservesTheExitCode(bool codeFix, bool alreadyExited)
    {
        using Process process = CreateControlledChild();
        Assert.IsTrue(process.Start());
        Task<string?>? ready = null;
        Task? waiting = null;
        try
        {
            ready = process.StandardOutput.ReadLineAsync();
            await CompleteStageAsync(ready, "child readiness");
            Assert.AreEqual(ReadyMarker, await ready);
            if (alreadyExited)
            {
                process.StandardInput.Close();
                Assert.IsTrue(process.WaitForExit(StageTimeoutMilliseconds), "The released child did not exit.");
            }

            Type polyfill = ProductReflection.Type(codeFix, "Polyfills.Polyfill");
            waiting = (Task)ProductReflection.Call(polyfill, null, "WaitForExitAsync",
                [typeof(Process), typeof(CancellationToken)], process, new CancellationToken(canceled: alreadyExited))!;
            if (!alreadyExited)
            {
                Assert.IsFalse(waiting.IsCompleted);
                process.StandardInput.Close();
            }

            await CompleteStageAsync(waiting, "product process wait");
            Assert.IsTrue(process.HasExited);
            Assert.AreEqual(17, process.ExitCode);
        }
        finally
        {
            ReleaseAndJoin(process);

            if (waiting is not null)
            {
                await CompleteStageAsync(waiting, "original product wait cleanup");
            }

            if (ready is not null)
            {
                await CompleteStageAsync(ready, "original reader cleanup");
            }
        }
    }

    internal static Process CreateControlledChild()
    {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = windows ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") : "/bin/sh",
                Arguments = windows
                    ? "/d /q /c \"echo analyzer-wait-ready& set /p input=& exit /b 17\""
                    : "-c \"echo analyzer-wait-ready; read input; exit 17\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
    }

    internal static void ReleaseAndJoin(Process process)
    {
        process.StandardInput.Close();
        if (!process.WaitForExit(StageTimeoutMilliseconds))
        {
            process.Kill();
            Assert.IsTrue(process.WaitForExit(StageTimeoutMilliseconds), "The owned child could not be terminated.");
        }
    }

    internal static async Task WaitForCompletionAsync(Task original, string stage)
    {
        using var cancellation = new CancellationTokenSource();
        var timeout = Task.Delay(StageTimeoutMilliseconds, cancellation.Token);
        Task completed = await Task.WhenAny(original, timeout);
        cancellation.Cancel();
        Assert.AreSame(original, completed, $"Timed out waiting for {stage}.");
    }

    private static async Task CompleteStageAsync(Task original, string stage)
    {
        await WaitForCompletionAsync(original, stage);
        await original;
    }
}
