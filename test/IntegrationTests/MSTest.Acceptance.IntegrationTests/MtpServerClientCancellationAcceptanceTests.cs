// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Diagnostics;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

using serverclient::Microsoft.Testing.Platform.ServerMode.Client;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end acceptance test proving that cancelling <see cref="IMtpServerClient.RunTestsAsync(CancellationToken)"/>
/// on the source-only MTP server-mode client (<c>Microsoft.Testing.Platform.ServerMode.Client.Sources</c>) propagates
/// all the way into a real, executing MSTest test through <c>TestContext.CancellationToken</c>, and that the
/// server/client shut down cleanly afterwards.
/// <para>
/// Unit tests already prove that cancelling the client's <c>RunTestsAsync</c> call emits a <c>$/cancelRequest</c>
/// notification. This test closes the gap by launching a real MSTest MTP application in server mode, running a
/// deliberately long-running cooperative test, cancelling only once the server confirms (via an <c>in-progress</c>
/// test-node update) that the test has actually started, and then asserting that the test itself observed
/// <c>TestContext.CancellationToken</c> - as opposed to merely observing that the client-side task was canceled or
/// that <c>$/cancelRequest</c> was sent.
/// </para>
/// <para>
/// The test's own observation is proven through a marker file the test method writes from inside its
/// <c>catch (OperationCanceledException)</c> block, deliberately independent of the server-mode test-node-update
/// channel: once a run is hard-canceled via <c>$/cancelRequest</c>, the platform's server-mode reporting consumer
/// is designed to stop relaying further test-node updates for that run (see
/// <c>AsyncConsumerDataProcessor.ConsumeAsync</c>), so the final "this test was canceled" node update is not
/// guaranteed to reach the client. That is expected platform behavior for a hard cancel (as opposed to the
/// cooperative <c>IGracefulStopTestExecutionCapability</c> path), not a propagation gap, so this test does not rely
/// on it.
/// </para>
/// </summary>
[TestClass]
public sealed class MtpServerClientCancellationAcceptanceTests : AcceptanceTestBase<MtpServerClientCancellationAcceptanceTests.TestAssetFixture>
{
    private const string AssetName = "MtpServerClientCancellationAsset";
    private const string ExpectedTestDisplayName = "LongRunningCancellableTest";
    private const string SignalFileEnvironmentVariable = "MTP_CANCELLATION_SIGNAL_FILE";
    private const string BodyStartedSignalFileEnvironmentVariable = "MTP_BODY_STARTED_SIGNAL_FILE";
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(60);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunTestsAsync_WhenCanceledAfterTestStarts_TestObservesTestContextCancellationAndShutsDownCleanly()
    {
        CancellationToken testTimeoutToken = TestContext.CancellationToken;
        string source = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent).FullName;

        string signalFilePath = Path.Combine(Path.GetTempPath(), $"mtp-cancellation-signal-{Guid.NewGuid():N}.txt");
        string bodyStartedSignalFilePath = Path.Combine(Path.GetTempPath(), $"mtp-cancellation-body-started-{Guid.NewGuid():N}.txt");
        File.Delete(signalFilePath);
        File.Delete(bodyStartedSignalFilePath);
        try
        {
            // Signaled from the 'in-progress' test-node update, proving the platform recorded the test as
            // started. This alone is not sufficient proof that the test method's body is executing: MSTest
            // publishes 'in-progress' before RunSingleTestAsync, which still runs TestInitialize/constructors
            // before invoking the method (see TestExecutionManager.Runner.cs). bodyStartedSignalFilePath below
            // closes that gap.
            TaskCompletionSource testStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

            List<MtpTestNodeUpdate> allUpdates = [];
            object updatesGate = new();

            using var client = MtpServerClient.Launch(source, CreateOptions(signalFilePath, bodyStartedSignalFilePath));

            // Capture a Process handle for the launched server immediately, while it is guaranteed to still be
            // alive (Launch only returns after the server has connected back). Holding this handle from before
            // exit lets us read its real ExitCode later, proving the server terminated cleanly - as opposed to
            // polling MtpServerClient.ProcessId, which reports 0 for both a graceful exit and a crash: ExitAsync
            // only writes a fire-and-forget notification, so its completion does not prove the peer processed
            // it before dying, and a crash right after would still make ProcessId settle to 0.
            using var serverProcess = Process.GetProcessById(client.ProcessId);
            client.TestNodesUpdated += (_, e) =>
            {
                lock (updatesGate)
                {
                    allUpdates.AddRange(e.Changes);
                }

                foreach (MtpTestNodeUpdate update in e.Changes)
                {
                    if (update.NodeType == "action" && update.DisplayName == ExpectedTestDisplayName && update.ExecutionState == "in-progress")
                    {
                        testStarted.TrySetResult();
                    }
                }
            };

            _ = await client.InitializeAsync(testTimeoutToken);

            using CancellationTokenSource runCancellation = new();
            Task<MtpRunResult> runTask = client.RunTestsAsync(runCancellation.Token);

            // Wait for both: the platform's 'in-progress' node update, and the test method itself signaling
            // (via bodyStartedSignalFilePath, written immediately before it awaits the cancellable delay) that
            // its body has actually reached the point where it awaits TestContext.CancellationToken. Only the
            // second removes the race the first alone leaves open - see the field-level remarks above.
            Task testNodeStartedTask = testStarted.Task.WaitAsync(WaitTimeout, testTimeoutToken);
            Task testBodyStartedTask = WaitForFileAsync(bodyStartedSignalFilePath, WaitTimeout, testTimeoutToken);
            await Task.WhenAll(testNodeStartedTask, testBodyStartedTask);

            await runCancellation.CancelAsync();

            // Canceling the token passed to RunTestsAsync makes the client send $/cancelRequest and cancel the
            // pending request's task client-side. Either the client-side task observes that cancellation, or (if
            // the server's real response happened to win the race) it completes normally; both are acceptable,
            // but neither may hang.
            try
            {
                _ = await runTask.WaitAsync(WaitTimeout, testTimeoutToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == runCancellation.Token)
            {
                // Expected: the pending 'testing/runTests' request was canceled client-side. Filtered on the
                // run's own token so a harness-level testTimeoutToken cancellation (a genuine hang) is never
                // misreported as this expected cancellation.
            }

            // The crux of this E2E test: assert that the executing MSTest test itself observed
            // TestContext.CancellationToken, not merely that the client-side call was canceled or that
            // $/cancelRequest was emitted. The signal file is written by the test method itself, from inside a
            // catch block keyed on that exact token, so its presence is direct proof of observation - independent
            // of whether the platform also relayed a corresponding test-node update back to the client (a hard
            // cancel is not guaranteed to relay one; see the type-level remarks above).
            await WaitForFileAsync(signalFilePath, WaitTimeout, testTimeoutToken);
            string signalContents = await File.ReadAllTextAsync(signalFilePath, testTimeoutToken);
            Assert.Contains(ExpectedTestDisplayName, signalContents, "Expected the signal file to be written by the canceled test method.");

            List<MtpTestNodeUpdate> snapshot;
            lock (updatesGate)
            {
                snapshot = [.. allUpdates];
            }

            Assert.ContainsSingle(
                n => n.NodeType == "action" && n.DisplayName == ExpectedTestDisplayName && n.ExecutionState == "in-progress",
                snapshot,
                $"Expected exactly one 'in-progress' update for '{ExpectedTestDisplayName}'. Collected: {Describe(snapshot)}");

            // Server and client must shut down cleanly and promptly: the server must actually exit on its own
            // in response to 'exit', with a zero exit code - not merely be force-killed by Dispose() below
            // (MtpServerProcess.Dispose kills the process if it is still alive), and not merely have
            // disappeared because it crashed after writing the marker (see the remark on serverProcess above).
            await client.ExitAsync(testTimeoutToken).WaitAsync(WaitTimeout, testTimeoutToken);
            await WaitForProcessExitAsync(serverProcess, WaitTimeout, testTimeoutToken);
            Assert.AreEqual(0, serverProcess.ExitCode, "Expected the server process to exit cleanly (exit code 0) in response to 'exit'.");
        }
        finally
        {
            File.Delete(signalFilePath);
            File.Delete(signalFilePath + ".tmp");
            File.Delete(bodyStartedSignalFilePath);
            File.Delete(bodyStartedSignalFilePath + ".tmp");
        }
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            while (!File.Exists(path))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), timeoutSource.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {timeout} waiting for signal file '{path}' to appear.");
        }
    }

    private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Server process did not exit naturally within {timeout} after 'exit' was sent.");
        }
    }

    private static string Describe(IReadOnlyList<MtpTestNodeUpdate> nodes)
        => nodes.Count == 0
            ? "<none>"
            : string.Join(", ", nodes.Select(n => $"[uid={n.Uid}, name={n.DisplayName}, type={n.NodeType}, state={n.ExecutionState}, error={n.ErrorMessage}]"));

    private static MtpServerClientOptions CreateOptions(string signalFilePath, string bodyStartedSignalFilePath)
    {
        MtpServerClientOptions options = new();

        // Neutralize inherited environment that would otherwise change the child host's behavior (dump
        // generation, telemetry opt-out, banners, ambient LLM/agent detection, ...). The client only adds to
        // the inherited environment, so we overwrite each well-known variable with an empty value; MTP treats
        // an empty value as "not set" for the switches that matter here.
        foreach (string variable in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            options.EnvironmentVariables[variable] = string.Empty;
        }

        string dotnetRoot = $"{RootFinder.Find()}/.dotnet";
        options.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
        options.EnvironmentVariables["DOTNET_INSTALL_DIR"] = dotnetRoot;
        options.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        options.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        // Let unhandled exceptions surface as a non-zero exit code instead of killing the process abruptly,
        // matching the reference server-mode harness.
        options.EnvironmentVariables["TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION"] = "0";

        options.EnvironmentVariables[SignalFileEnvironmentVariable] = signalFilePath;
        options.EnvironmentVariables[BodyStartedSignalFileEnvironmentVariable] = bodyStartedSignalFilePath;

        return options;
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file MtpServerClientCancellationAsset.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LongRunningCancellableTest()
    {
        // Signal that the method body has actually been entered and is about to await the cancellable delay -
        // as opposed to the platform's 'in-progress' test-node update, which is published before
        // RunSingleTestAsync even runs TestInitialize/constructors, let alone this method. Waiting on this
        // signal (rather than only the node update) is what lets the acceptance test cancel with certainty
        // that this delay - not some earlier setup step - is what will observe the cancellation.
        WriteMarkerFile("MTP_BODY_STARTED_SIGNAL_FILE", "body started");

        // Runs until the server-mode client cancels the request. The platform then cancels
        // TestContext.CancellationToken, which this awaited delay observes directly, proving cancellation
        // propagated all the way from the client into the executing test. The catch block below writes a
        // marker file the acceptance test reads back as direct, out-of-band proof of that observation,
        // independent of whether the platform also relays a corresponding test-node update to the client for
        // a hard-canceled run (it is not guaranteed to).
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), TestContext.CancellationToken);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == TestContext.CancellationToken)
        {
            WriteMarkerFile("MTP_CANCELLATION_SIGNAL_FILE", $"{nameof(LongRunningCancellableTest)} observed TestContext.CancellationToken");
            throw;
        }
    }

    private static void WriteMarkerFile(string environmentVariable, string contents)
    {
        string? path = Environment.GetEnvironmentVariable(environmentVariable);
        if (path is null)
        {
            return;
        }

        // Write to a sibling temp file and rename into place so the acceptance test - which polls via
        // File.Exists - never observes an empty or partially written marker file. File.Move onto the final
        // path is atomic on both Windows and Unix.
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path);
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }
}
