// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

internal sealed class ChatClientManager : IChatClientManager
{
    private Func<IServiceProvider, object>? _chatClientProviderFactory;

    public void SetChatClientProviderFactory(Func<IServiceProvider, object> chatClientProviderFactory)
    {
        if (chatClientProviderFactory is null)
        {
            throw new ArgumentNullException(nameof(chatClientProviderFactory));
        }

        if (_chatClientProviderFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.ChatClientProviderAlreadyRegistered);
        }

        _chatClientProviderFactory = chatClientProviderFactory;
    }

    public void InstantiateChatClientProvider(ServiceProvider serviceProvider)
    {
        if (_chatClientProviderFactory is not null)
        {
            serviceProvider.AddService(_chatClientProviderFactory(serviceProvider));
        }
    }
}
