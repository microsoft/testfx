// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

internal sealed class ChatClientManager : IChatClientManager
{
    private Func<IServiceProvider, object>? _chatClientProvider;

    public void AddChatClientProvider(Func<IServiceProvider, object> chatClientProvider)
    {
        if (chatClientProvider is null)
        {
            throw new ArgumentNullException(nameof(chatClientProvider));
        }

        if (_chatClientProvider is not null)
        {
            throw new InvalidOperationException(PlatformResources.ChatClientProviderAlreadyRegistered);
        }

        _chatClientProvider = chatClientProvider;
    }

    public void RegisterChatClientProvider(ServiceProvider serviceProvider)
    {
        if (_chatClientProvider is not null)
        {
            serviceProvider.AddService(_chatClientProvider(serviceProvider));
        }
    }
}
