// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform;

/// <summary>
/// The pre-launch transport selected for the 'dotnet test' pipe protocol (a.k.a. dotnettestcli). The wire
/// protocol itself (message/serializer/version contract) is identical regardless of which value is selected -
/// only the duplex channel that carries it differs. See <c>docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md</c>.
/// </summary>
internal enum DotnetTestTransportKind
{
    /// <summary>
    /// The original <see cref="System.IO.Pipes"/>-based transport, selected via <c>--dotnet-test-pipe</c>. This
    /// is the default whenever only that option is present, preserving exact wire and behavioral compatibility.
    /// Not available on <c>browser-wasm</c> or <c>wasi-wasm</c> (no named-pipe support on either runtime).
    /// </summary>
    NamedPipe,

    /// <summary>
    /// The WebSocket-based transport, selected via <c>--dotnet-test-transport websocket</c> together with
    /// <c>--dotnet-test-websocket-endpoint</c> and <c>--dotnet-test-websocket-token</c>. Required on
    /// <c>browser-wasm</c>; optional elsewhere.
    /// </summary>
    WebSocket,
}

internal static class DotnetTestHelper
{
    public static bool HasDotnetTestServerOption(this CommandLineHandler commandLineHandler) =>
        commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverArgs) &&
        serverArgs.Length == 1 &&
        serverArgs[0].Equals(PlatformCommandLineProvider.DotnetTestCliProtocolName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves which transport carries the dotnettestcli protocol for this process, mirroring the validation
    /// already enforced by <see cref="PlatformCommandLineProvider.ValidateCommandLineOptionsAsync"/> (which
    /// guarantees, by the time this runs, that exactly one of the two option groups below is present).
    /// </summary>
    public static bool TryGetDotnetTestTransport(this CommandLineHandler commandLineHandler, out DotnetTestTransportKind transport)
    {
        if (commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestTransportOptionKey, out string[]? transportArgs) &&
            transportArgs is { Length: 1 } &&
            PlatformCommandLineProvider.DotNetTestTransportWebSocketArgument.Equals(transportArgs[0], StringComparison.OrdinalIgnoreCase))
        {
            transport = DotnetTestTransportKind.WebSocket;
            return true;
        }

        if (commandLineHandler.IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey))
        {
            transport = DotnetTestTransportKind.NamedPipe;
            return true;
        }

        transport = default;
        return false;
    }
}
