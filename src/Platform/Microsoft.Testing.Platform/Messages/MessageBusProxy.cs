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

    public override async Task InitAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.InitAsync().ConfigureAwait(false);
    }

    public void SetBuiltMessageBus(BaseMessageBus messageBus)
    {
        Guard.NotNull(messageBus);
        _messageBus = messageBus;
    }

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

    public override async Task DisableAsync()
    {
        EnsureMessageBusAvailable();
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
