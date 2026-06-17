// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Multi-connection variant of <see cref="FakeDotnetTestSdk"/>. The single-connection harness can
/// only service one peer, which is enough for a plain test host run. Scenarios that involve an
/// orchestrator (e.g. <c>--retry-failed-tests</c>) produce several peers on the same pipe: the
/// orchestrator process itself plus every test host process it spawns. This harness mirrors the
/// real SDK's accept loop (see <c>TestApplication.WaitConnectionAsync</c>) by continuously
/// accepting new <see cref="NamedPipeServerStream"/> instances and servicing each on its own task,
/// performing the same handshake negotiation on every connection.
/// </summary>
internal static class FakeDotnetTestSdkMultiConnection
{
    /// <summary>
    /// Spins up a fake SDK pipe server that accepts multiple connections, runs
    /// <paramref name="testHost"/> against it, and returns every handshake observed across all
    /// connections together with the process-level result.
    /// </summary>
    public static async Task<FakeDotnetTestSdkMultiConnectionResult> RunAsync(
        global::Microsoft.Testing.TestInfrastructure.TestHost testHost,
        string extraArguments,
        Dictionary<string, string?>? environmentVariables = null,
        string supportedProtocolVersions = FakeDotnetTestSdk.DefaultSupportedProtocolVersions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testHost);

        string pipeId = Guid.NewGuid().ToString("N");
        string osPipeName = DotnetTestPipeProtocol.GetPipeName(pipeId);

        var receivedHandshakes = new ConcurrentBag<Dictionary<byte, string>>();
        var connectionTasks = new ConcurrentBag<Task>();

        using var acceptLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task acceptLoop = AcceptConnectionsAsync(osPipeName, supportedProtocolVersions, receivedHandshakes, connectionTasks, acceptLoopCts.Token);

        string pipeArgs = $"--server dotnettestcli --dotnet-test-pipe {osPipeName}";
        string finalArgs = $"{pipeArgs} {extraArguments}";
        TestHostResult hostResult = await testHost.ExecuteAsync(finalArgs, environmentVariables, cancellationToken: cancellationToken).ConfigureAwait(false);

        // The test app (and any child processes it spawned) has exited, so no further connections
        // will be made. Stop accepting and let the in-flight connection handlers drain.
        await acceptLoopCts.CancelAsync().ConfigureAwait(false);
        await SwallowCancellationAsync(acceptLoop).ConfigureAwait(false);
        foreach (Task connectionTask in connectionTasks)
        {
            await SwallowCancellationAsync(connectionTask).ConfigureAwait(false);
        }

        return new FakeDotnetTestSdkMultiConnectionResult(hostResult, [.. receivedHandshakes]);
    }

    private static async Task AcceptConnectionsAsync(
        string osPipeName,
        string supportedProtocolVersions,
        ConcurrentBag<Dictionary<byte, string>> receivedHandshakes,
        ConcurrentBag<Task> connectionTasks,
        CancellationToken cancellationToken)
    {
        PipeOptions options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = new NamedPipeServerStream(
                    osPipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    options);

                await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                connectionTasks.Add(HandleConnectionAsync(stream, supportedProtocolVersions, receivedHandshakes, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: we cancel the accept loop once the test app process exits.
        }
    }

    private static async Task HandleConnectionAsync(
        NamedPipeServerStream stream,
        string supportedProtocolVersions,
        ConcurrentBag<Dictionary<byte, string>> receivedHandshakes,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                RawMessage? frame = await DotnetTestPipeProtocol.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                if (frame.SerializerId == DotnetTestPipeProtocol.SerializerIds.HandshakeMessage)
                {
                    Dictionary<byte, string> handshake = DotnetTestPipeProtocol.DecodeHandshakeBody(frame.Body);
                    receivedHandshakes.Add(handshake);

                    string selected = handshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.SupportedProtocolVersions, out string? appVersions)
                        && appVersions.Split(';', StringSplitOptions.RemoveEmptyEntries).Contains(supportedProtocolVersions)
                            ? supportedProtocolVersions
                            : string.Empty;

                    byte[] replyBody = DotnetTestPipeProtocol.EncodeHandshakeBody(BuildSdkHandshakeReply(selected));
                    await DotnetTestPipeProtocol.WriteFrameAsync(stream, DotnetTestPipeProtocol.SerializerIds.HandshakeMessage, replyBody, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DotnetTestPipeProtocol.WriteFrameAsync(stream, DotnetTestPipeProtocol.SerializerIds.VoidResponse, body: [], cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the harness is torn down.
        }
        catch (IOException)
        {
            // The peer disconnected abruptly; nothing more to read on this connection.
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static Dictionary<byte, string> BuildSdkHandshakeReply(string selectedVersion)
        => new(capacity: 5)
        {
            { DotnetTestPipeProtocol.HandshakeProperties.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { DotnetTestPipeProtocol.HandshakeProperties.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { DotnetTestPipeProtocol.HandshakeProperties.Framework, RuntimeInformation.FrameworkDescription },
            { DotnetTestPipeProtocol.HandshakeProperties.OS, RuntimeInformation.OSDescription },
            { DotnetTestPipeProtocol.HandshakeProperties.SupportedProtocolVersions, selectedVersion },
        };

    private static async Task SwallowCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during teardown.
        }
    }
}

/// <summary>Captures the handshakes observed across all pipe connections plus the process result.</summary>
internal sealed class FakeDotnetTestSdkMultiConnectionResult
{
    public FakeDotnetTestSdkMultiConnectionResult(TestHostResult testHostResult, IReadOnlyList<Dictionary<byte, string>> receivedHandshakes)
    {
        TestHostResult = testHostResult;
        ReceivedHandshakes = receivedHandshakes;
    }

    public TestHostResult TestHostResult { get; }

    public IReadOnlyList<Dictionary<byte, string>> ReceivedHandshakes { get; }

    public IEnumerable<Dictionary<byte, string>> HandshakesWithHostType(string hostType)
        => ReceivedHandshakes.Where(h => h.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.HostType, out string? value) && value == hostType);
}
