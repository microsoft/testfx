// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerProcessCompatibilityTests
{
    internal const string ReadyMarker = "analyzer-wait-ready";
    private const string ChildProbeVariable = "TESTFX_ANALYZER_PROCESS_CHILD";

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
        string module = typeof(AnalyzerProcessCompatibilityTests).Assembly.Location;
#if NETCOREAPP
        string host = Environment.ProcessPath ?? throw new InvalidOperationException("The current test host path could not be determined.");
        ProcessStartInfo start = new(host);
        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(module);
        }
#else
        ProcessStartInfo start = new(module);
#endif
        start.WorkingDirectory = AppContext.BaseDirectory;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.EnvironmentVariables[ChildProbeVariable] = "1";
        return new Process { StartInfo = start };
    }

    internal static bool TryRunControlledChild()
    {
        if (Environment.GetEnvironmentVariable(ChildProbeVariable) != "1")
        {
            return false;
        }

        // A shell's readiness line does not guarantee that its input command will keep waiting.
        // This owned managed child exits only when the parent closes the input pipe.
        Console.WriteLine(ReadyMarker);
        _ = Console.In.ReadToEnd();
        return true;
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
