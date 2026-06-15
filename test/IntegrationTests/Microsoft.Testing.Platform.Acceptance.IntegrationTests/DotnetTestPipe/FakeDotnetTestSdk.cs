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

        List<RawMessage> received = [];
        Dictionary<byte, string>? receivedHandshake = null;
        Dictionary<byte, string>? sentHandshakeReply = null;
        string? negotiatedVersion = null;

        string pipeArgs = $"--server dotnettestcli --dotnet-test-pipe {osPipeName}";
        string finalArgs = extraArguments is null ? pipeArgs : $"{pipeArgs} {extraArguments}";
        Task<TestHostResult> hostRun = testHost.ExecuteAsync(finalArgs, environmentVariables, cancellationToken: cancellationToken);

        // Wait for the test app to connect.
        await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

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
                sentHandshakeReply = BuildSdkHandshakeReply(selected, isIde);
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

        return new FakeDotnetTestSdkResult(
            hostResult,
            received,
            receivedHandshake,
            sentHandshakeReply,
            negotiatedVersion);
    }

    /// <summary>
    /// Mirrors <c>TestApplication.GetSupportedProtocolVersion</c> on the SDK side: takes the
    /// semicolon-separated list the test app advertised and returns the highest version that is
    /// also in <paramref name="sdkSupportedVersions"/>. Returns <see cref="string.Empty"/> if none
    /// match.
    /// </summary>
    private static string SelectHighestMutuallySupportedVersion(Dictionary<byte, string> handshakeProperties, string sdkSupportedVersions)
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

    private static Dictionary<byte, string> BuildSdkHandshakeReply(string selectedVersion, bool isIde)
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

        return properties;
    }
}
