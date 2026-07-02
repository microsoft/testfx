// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// In-process stand-in for the <c>dotnet test</c> SDK side of the
/// <c>--server dotnettestcli --dotnet-test-pipe</c> protocol. Listens on a named pipe, performs
/// the same handshake negotiation the real SDK uses today, captures every message the test app
/// sends, and returns a <see cref="FakeDotnetTestSdkResult"/>.
/// <para>
/// Built on a hand-rolled wire reader (<see cref="DotnetTestPipeProtocol"/>) so that this harness
/// is independent of testfx internal types and exercises the wire format itself.
/// </para>
/// </summary>
internal static class FakeDotnetTestSdk
{
    /// <summary>Default fake-SDK advertised protocol version. Mirrors today's
    /// <c>ProtocolConstants.SupportedVersions</c> on the SDK side.</summary>
    public const string DefaultSupportedProtocolVersions = "1.0.0";

    /// <summary>
    /// Spins up a fake SDK pipe server, runs <paramref name="testHost"/> against it, and returns
    /// everything observed during the run.
    /// </summary>
    public static async Task<FakeDotnetTestSdkResult> RunAsync(
        global::Microsoft.Testing.TestInfrastructure.TestHost testHost,
        string? extraArguments = null,
        Dictionary<string, string?>? environmentVariables = null,
        string supportedProtocolVersions = DefaultSupportedProtocolVersions,
        bool isIde = false,
        bool advertiseServerControlPipe = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testHost);

        string pipeId = Guid.NewGuid().ToString("N");
        string osPipeName = DotnetTestPipeProtocol.GetPipeName(pipeId);

        // CurrentUserOnly hardens the pipe ACL so only the current user can connect. It is
        // available here because this harness only targets .NET (Core) ($(NetCurrent)).
        PipeOptions options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

        // .NET Framework's NamedPipeServerStream takes the pipe name without leading \\.\pipe\
        // — Windows adds it automatically. On Unix, .NET 6+ uses Unix domain sockets at the given
        // path (matching what NamedPipeServer.GetPipeName produces with Path.Combine("/tmp", name)).
        using NamedPipeServerStream stream = new(osPipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, options);

        // Optional reverse "server control" pipe: when advertised, the test app connects back and parks a
        // WaitForServerControlRequest that we complete with a CancelSession, exercising server-initiated
        // session cancellation (issue #8691). Declared with 'await using' so the OS handle is released reliably
        // on every exit path (including exceptions), and remains a no-op when the feature is not advertised.
        string? controlOsPipeName = advertiseServerControlPipe ? DotnetTestPipeProtocol.GetPipeName(Guid.NewGuid().ToString("N")) : null;
        await using NamedPipeServerStream? controlStream = controlOsPipeName is null
            ? null
            : new(controlOsPipeName, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, options);

        List<RawMessage> received = [];
        Dictionary<byte, string>? receivedHandshake = null;
        Dictionary<byte, string>? sentHandshakeReply = null;
        string? negotiatedVersion = null;
        var controlObservations = new ServerControlObservations();

        string pipeArgs = $"--server dotnettestcli --dotnet-test-pipe {osPipeName}";
        string finalArgs = extraArguments is null ? pipeArgs : $"{pipeArgs} {extraArguments}";
        Task<TestHostResult> hostRun = testHost.ExecuteAsync(finalArgs, environmentVariables, cancellationToken: cancellationToken);

        // Wait for the test app to connect.
        await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Accept the control connection and push the cancel signal concurrently with the data read loop.
        Task? controlTask = controlStream is null
            ? null
            : Task.Run(() => DriveServerControlAsync(controlStream, controlObservations, cancellationToken), cancellationToken);

        // Read frames until the peer disconnects.
        while (true)
        {
            RawMessage? frame = await DotnetTestPipeProtocol.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                break;
            }

            received.Add(frame);

            if (frame.SerializerId == DotnetTestPipeProtocol.SerializerIds.HandshakeMessage)
            {
                receivedHandshake = DotnetTestPipeProtocol.DecodeHandshakeBody(frame.Body);
                string selected = SelectHighestMutuallySupportedVersion(receivedHandshake, supportedProtocolVersions);
                negotiatedVersion = selected;
                sentHandshakeReply = BuildSdkHandshakeReply(selected, isIde, controlOsPipeName);
                byte[] replyBody = DotnetTestPipeProtocol.EncodeHandshakeBody(sentHandshakeReply);
                await DotnetTestPipeProtocol.WriteFrameAsync(stream, DotnetTestPipeProtocol.SerializerIds.HandshakeMessage, replyBody, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For every other request the SDK replies with a VoidResponse (empty body).
                await DotnetTestPipeProtocol.WriteFrameAsync(stream, DotnetTestPipeProtocol.SerializerIds.VoidResponse, body: [], cancellationToken).ConfigureAwait(false);
            }
        }

        TestHostResult hostResult = await hostRun.ConfigureAwait(false);

        if (controlTask is not null)
        {
            try
            {
                await controlTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort: the control interaction is observed via controlObservations; don't let its
                // teardown fail the harness.
            }
        }

        return new FakeDotnetTestSdkResult(
            hostResult,
            received,
            receivedHandshake,
            sentHandshakeReply,
            negotiatedVersion,
            controlObservations.Connected,
            controlObservations.CancelSent);
    }

    /// <summary>
    /// Accepts the reverse control-pipe connection, reads the single parked
    /// <see cref="DotnetTestPipeProtocol.SerializerIds.WaitForServerControlRequest"/>, and completes it with a
    /// <see cref="DotnetTestPipeProtocol.ServerControlKinds.CancelSession"/> message.
    /// </summary>
    private static async Task DriveServerControlAsync(NamedPipeServerStream controlStream, ServerControlObservations observations, CancellationToken cancellationToken)
    {
        await controlStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        observations.Connected = true;

        RawMessage? request = await DotnetTestPipeProtocol.ReadFrameAsync(controlStream, cancellationToken).ConfigureAwait(false);
        if (request is null || request.SerializerId != DotnetTestPipeProtocol.SerializerIds.WaitForServerControlRequest)
        {
            return;
        }

        byte[] body = DotnetTestPipeProtocol.EncodeServerControlMessageBody(DotnetTestPipeProtocol.ServerControlKinds.CancelSession);
        await DotnetTestPipeProtocol.WriteFrameAsync(controlStream, DotnetTestPipeProtocol.SerializerIds.ServerControlMessage, body, cancellationToken).ConfigureAwait(false);
        observations.CancelSent = true;
    }

    private sealed class ServerControlObservations
    {
        public bool Connected { get; set; }

        public bool CancelSent { get; set; }
    }

    /// <summary>
    /// Mirrors <c>TestApplication.GetSupportedProtocolVersion</c> on the SDK side: takes the
    /// semicolon-separated list the test app advertised and returns the highest version that is
    /// also in <paramref name="sdkSupportedVersions"/>. Returns <see cref="string.Empty"/> if none
    /// match.
    /// </summary>
    internal static string SelectHighestMutuallySupportedVersion(Dictionary<byte, string> handshakeProperties, string sdkSupportedVersions)
    {
        if (!handshakeProperties.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.SupportedProtocolVersions, out string? appVersions)
            || string.IsNullOrWhiteSpace(appVersions))
        {
            return string.Empty;
        }

        HashSet<string> sdkSet = new(
            sdkSupportedVersions.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(static raw => raw.Trim()),
            StringComparer.Ordinal);

        string? best = null;
        Version? bestParsed = null;
        foreach (string candidate in appVersions.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(static raw => raw.Trim()))
        {
            if (!sdkSet.Contains(candidate))
            {
                continue;
            }

            if (!Version.TryParse(candidate, out Version? parsed))
            {
                best ??= candidate;
                continue;
            }

            if (bestParsed is null || parsed > bestParsed)
            {
                best = candidate;
                bestParsed = parsed;
            }
        }

        return best ?? string.Empty;
    }

    private static Dictionary<byte, string> BuildSdkHandshakeReply(string selectedVersion, bool isIde, string? serverControlPipeName = null)
    {
        Dictionary<byte, string> properties = new(capacity: 6)
        {
            { DotnetTestPipeProtocol.HandshakeProperties.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { DotnetTestPipeProtocol.HandshakeProperties.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { DotnetTestPipeProtocol.HandshakeProperties.Framework, RuntimeInformation.FrameworkDescription },
            { DotnetTestPipeProtocol.HandshakeProperties.OS, RuntimeInformation.OSDescription },
            { DotnetTestPipeProtocol.HandshakeProperties.SupportedProtocolVersions, selectedVersion },
        };

        if (isIde)
        {
            properties.Add(DotnetTestPipeProtocol.HandshakeProperties.IsIDE, bool.TrueString);
        }

        if (!string.IsNullOrEmpty(serverControlPipeName))
        {
            properties.Add(DotnetTestPipeProtocol.HandshakeProperties.ServerControlPipeName, serverControlPipeName);
        }

        return properties;
    }
}
