// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class MessageBusProxy : BaseMessageBus, IMessageBus
{
    private BaseMessageBus? _messageBus;

    public override IDataConsumer[] DataConsumerServices
        => _messageBus is null ? [] : _messageBus.DataConsumerServices;

    public override IEnumerable<IDataConsumer> ConsumersStillRunning
        => _messageBus is null ? [] : _messageBus.ConsumersStillRunning;

    public override async Task InitAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.InitAsync().ConfigureAwait(false);
    }

    public void SetBuiltMessageBus(BaseMessageBus messageBus)
        => _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

    public override async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        EnsureMessageBusAvailable();
        await _messageBus.PublishAsync(dataProducer, data).ConfigureAwait(false);
    }

    public override async Task DrainDataAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.DrainDataAsync().ConfigureAwait(false);
    }

    // Unlike the other members we don't require the concrete bus to be available here. Disabling is part of the
    // shutdown handshake the host performs unconditionally (including when the run was canceled or failed before
    // the bus was ever built), and an unbuilt bus has nothing to disable. This mirrors Dispose and
    // DataConsumerServices, which are already tolerant of a null bus.
    public override async Task DisableAsync()
    {
        if (_messageBus is null)
        {
            return;
        }

        await _messageBus.DisableAsync().ConfigureAwait(false);
    }

    [MemberNotNull(nameof(_messageBus))]
    private void EnsureMessageBusAvailable()
    {
        if (_messageBus is null)
        {
            throw new InvalidOperationException(Resources.PlatformResources.MessageBusNotReady);
        }
    }

    public override void Dispose() => _messageBus?.Dispose();
}
