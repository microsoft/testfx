// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

internal sealed class ChatClientManager : IChatClientManager
{
    private Func<IServiceProvider, object>? _chatClientFactory;

    public void AddChatClientFactory(Func<IServiceProvider, object> chatClientFactory)
    {
        if (chatClientFactory is null)
        {
            throw new ArgumentNullException(nameof(chatClientFactory));
        }

        if (_chatClientFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.ChatClientFactoryAlreadyRegistered);
        }

        _chatClientFactory = chatClientFactory;
    }

    public void RegisterChatClientFactory(ServiceProvider serviceProvider)
    {
        if (_chatClientFactory is not null)
        {
            serviceProvider.AddService(_chatClientFactory(serviceProvider));
        }
    }
}
