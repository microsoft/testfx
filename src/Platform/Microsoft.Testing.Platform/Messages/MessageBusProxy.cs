// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class MessageBusProxy : BaseMessageBus, IMessageBus
{
    private BaseMessageBus? _messageBus;

    public override IDataConsumer[] DataConsumerServices
        => _messageBus is null ? [] : _messageBus.DataConsumerServices;

    public override async Task InitAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.InitAsync();
    }

    public void SetBuiltMessageBus(BaseMessageBus messageBus)
    {
        Guard.NotNull(messageBus);
        _messageBus = messageBus;
    }

    public override async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        EnsureMessageBusAvailable();
        await _messageBus.PublishAsync(dataProducer, data);
    }

    public override async Task DrainDataAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.DrainDataAsync();
    }

    public override async Task DisableAsync()
    {
        EnsureMessageBusAvailable();
        await _messageBus.DisableAsync();
    }

    [MemberNotNull(nameof(_messageBus))]
    private void EnsureMessageBusAvailable()
    {
        if (_messageBus is null)
        {
            throw new InvalidOperationException("The message bus has not been built yet or is no more usable at this stage.");
        }
    }

    public override void Dispose() => _messageBus?.Dispose();
}
