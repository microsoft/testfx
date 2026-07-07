// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal interface IPushOnlyProtocol : IDisposable
{
    bool IsServerMode { get; }

    // True once the SDK advertised a reverse "server control" pipe during the handshake, meaning it can push
    // server-initiated signals (e.g. session cancellation) to the test host. Only meaningful after a successful
    // IsCompatibleProtocolAsync call.
    bool IsServerControlChannelSupported { get; }

    Task AfterCommonServiceSetupAsync();

    Task HelpInvokedAsync();

    Task<bool> IsCompatibleProtocolAsync(string testHostType, IReadOnlyDictionary<byte, string>? additionalHandshakeProperties = null);

    // Opens the reverse "server control" channel (when supported) and parks a long-poll on it. The provided
    // reaction runs exactly once when the SDK signals a session cancellation (or when the control pipe drops).
    Task StartServerControlChannelAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync);

    Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync();

    Task OnExitAsync();
}
