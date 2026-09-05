// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class AbortionTests : AcceptanceTestBase<AbortionTests.TestAssetFixture>
{
    private const string AssetName = "Abort";

    // We retry because sometime the Canceling the session message is not showing up.
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.Console)]
    public async Task AbortWithCTRLPlusC_TestHost_Succeeded(string tfm)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);

        // We don't assert "Canceling the test session" message.
        // Cancellation could happen very first that we didn't have the opportunity to write this message.
        // However, the summary should always be correct and should always indicate that the session was aborted.
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 0, aborted: true);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "The targeted SIGINT test uses the Unix kill API.")]
    public async Task AbortControllerWithSIGINT_AllowsChildCleanupToComplete(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string cleanupMarker = Path.Combine(testHost.DirectoryName, $"cleanup-{Guid.NewGuid():N}.txt");
        string trxFileName = $"abort-{Guid.NewGuid():N}.trx";
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ABORT_CONTROLLER_ONLY"] = "1",
            ["ABORT_CLEANUP_MARKER"] = cleanupMarker,
        };

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {trxFileName}",
            environmentVariables,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);
        Assert.IsTrue(File.Exists(cleanupMarker), $"The child cleanup marker was not written to '{cleanupMarker}'.");
        Assert.AreEqual("cleanup completed", File.ReadAllText(cleanupMarker));
        Assert.HasCount(1, Directory.GetFiles(testHost.DirectoryName, trxFileName, SearchOption.AllDirectories));
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "The targeted SIGINT test uses the Unix kill API.")]
    public async Task AbortControllerWithSecondSIGINT_ForceTerminatesChild(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string cleanupMarker = Path.Combine(testHost.DirectoryName, $"cleanup-{Guid.NewGuid():N}.txt");
        string cleanupStartedMarker = Path.Combine(testHost.DirectoryName, $"cleanup-started-{Guid.NewGuid():N}.txt");
        string childPidMarker = Path.Combine(testHost.DirectoryName, $"child-pid-{Guid.NewGuid():N}.txt");
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ABORT_CONTROLLER_ONLY"] = "1",
            ["ABORT_CONTROLLER_TWICE"] = "1",
            ["ABORT_CLEANUP_MARKER"] = cleanupMarker,
            ["ABORT_CLEANUP_STARTED_MARKER"] = cleanupStartedMarker,
            ["ABORT_CHILD_PID_MARKER"] = childPidMarker,
        };

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--report-trx",
            environmentVariables,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);
        Assert.IsTrue(File.Exists(cleanupStartedMarker), "The second SIGINT must be sent only after child cleanup starts.");
        int childPid = int.Parse(File.ReadAllText(childPidMarker), CultureInfo.InvariantCulture);
        await AssertProcessExitedAsync(childPid, TestContext.CancellationToken);
        Assert.IsFalse(File.Exists(cleanupMarker), "Force cancellation must terminate the child before its delayed cleanup completes.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.Console)]
    public async Task AbortControllerWithCTRLPlusC_AllowsChildCleanupToComplete(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string cleanupMarker = Path.Combine(testHost.DirectoryName, $"cleanup-{Guid.NewGuid():N}.txt");
        string trxFileName = $"abort-{Guid.NewGuid():N}.trx";
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ABORT_CLEANUP_MARKER"] = cleanupMarker,
        };

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {trxFileName}",
            environmentVariables,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);
        Assert.IsTrue(File.Exists(cleanupMarker), $"The child cleanup marker was not written to '{cleanupMarker}'.");
        Assert.AreEqual("cleanup completed", File.ReadAllText(cleanupMarker));
        Assert.HasCount(1, Directory.GetFiles(testHost.DirectoryName, trxFileName, SearchOption.AllDirectories));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file Abort.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using System.Runtime.InteropServices;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? childPidMarker = Environment.GetEnvironmentVariable("ABORT_CHILD_PID_MARKER");
        if (childPidMarker is not null)
        {
            using Process currentProcess = Process.GetCurrentProcess();
            File.WriteAllText(childPidMarker, currentProcess.Id.ToString());
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
        builder.AddTrxReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        _ = Task.Run(() =>
        {
            DummyTestFramework.FireCancel.Wait();

            if (Environment.GetEnvironmentVariable("ABORT_CONTROLLER_ONLY") == "1")
            {
                int optionIndex = Array.IndexOf(args, "--internal-testhostcontroller-pid");
                if (optionIndex < 0 || optionIndex + 1 >= args.Length)
                {
                    throw new Exception("The child process did not receive the test host controller PID.");
                }

                int controllerPid = int.Parse(args[optionIndex + 1]);
                if (kill(controllerPid, 2) != 0)
                {
                    throw new Exception($"kill(SIGINT) failed with errno '{Marshal.GetLastWin32Error()}'.");
                }

                if (Environment.GetEnvironmentVariable("ABORT_CONTROLLER_TWICE") == "1")
                {
                    string cleanupStartedMarker = Environment.GetEnvironmentVariable("ABORT_CLEANUP_STARTED_MARKER")
                        ?? throw new Exception("Missing cleanup-started marker path.");
                    DateTime timeout = DateTime.UtcNow.AddSeconds(15);
                    while (!File.Exists(cleanupStartedMarker) && DateTime.UtcNow < timeout)
                    {
                        Thread.Sleep(10);
                    }

                    if (!File.Exists(cleanupStartedMarker))
                    {
                        throw new Exception("Child cleanup did not start before the second SIGINT timeout.");
                    }

                    if (kill(controllerPid, 2) != 0)
                    {
                        throw new Exception($"Second kill(SIGINT) failed with errno '{Marshal.GetLastWin32Error()}'.");
                    }
                }

                return;
            }

            if (!GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0))
            {
                throw new Exception($"GetLastWin32Error '{Marshal.GetLastWin32Error()}'");
            }
        });
        return await app.RunAsync();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
    [DllImport("libc", SetLastError = true)]
    static extern int kill(int pid, int sig);
    public enum ConsoleCtrlEvent
    {
        CTRL_C = 0,
        CTRL_BREAK = 1,
        CTRL_CLOSE = 2,
        CTRL_LOGOFF = 5,
        CTRL_SHUTDOWN = 6
    }

}

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public static readonly ManualResetEventSlim FireCancel = new ManualResetEventSlim(false);
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // This will trigger pressing CTRL+C that should propagate through the platform
        // and down to us as the context.Cancellation token being canceled.
        // It should happen almost immediately, but we allow 15 seconds for this to happen
        // if it does not happen then the platform does not handle cancellation correctly and
        // the test fails.
        // If it happens, we return a result, and platform should report Aborted exit code and result.
        FireCancel.Set();

        var timeoutTask = Task.Delay(15_000, context.CancellationToken);
        try
        {
            await timeoutTask;
        }
        finally
        {
            string? cleanupMarker = Environment.GetEnvironmentVariable("ABORT_CLEANUP_MARKER");
            if (cleanupMarker is not null)
            {
                string? cleanupStartedMarker = Environment.GetEnvironmentVariable("ABORT_CLEANUP_STARTED_MARKER");
                if (cleanupStartedMarker is not null)
                {
                    File.WriteAllText(cleanupStartedMarker, "cleanup started");
                    await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
                }
                else
                {
                    await Task.Delay(500, CancellationToken.None);
                }

                File.WriteAllText(cleanupMarker, "cleanup completed");
            }
        }

        if (!timeoutTask.IsCanceled)
        {
            throw new Exception("Cancellation was not propagated to the adapter within 15 seconds since CTRL+C.");
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => new ITestFrameworkCapability[] { new TrxReportCapability() };
}

internal class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported => true;

    void ITrxReportCapability.Enable()
    {
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }

    private static async Task AssertProcessExitedAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var child = System.Diagnostics.Process.GetProcessById(processId);
            Task waitForExitTask = child.WaitForExitAsync(cancellationToken);
            Task completedTask = await Task.WhenAny(waitForExitTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
            Assert.AreSame(waitForExitTask, completedTask, $"Child process '{processId}' was still running after forced cancellation.");
            await waitForExitTask;
        }
        catch (ArgumentException)
        {
            // The process already exited before it could be opened.
        }
    }
}
