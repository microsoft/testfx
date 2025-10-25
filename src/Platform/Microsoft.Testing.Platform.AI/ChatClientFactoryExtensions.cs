// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Extension methods for chat client factory.
/// </summary>
public static class ChatClientFactoryExtensions
{
    /// <summary>
    /// Adds a chat client factory to the test application builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="chatClientFactory">The factory function to create chat client factories.</param>
    public static void AddChatClientFactory(this ITestApplicationBuilder testApplicationBuilder, Func<IServiceProvider, IChatClientFactory> chatClientFactory)
    {
        if (testApplicationBuilder is not TestApplicationBuilder builder)
        {
            throw new InvalidOperationException(PlatformResources.InvalidTestApplicationBuilderTypeForAI);
        }

        builder.ChatClientManager.AddChatClientFactory(chatClientFactory);
    }

    /// <summary>
    /// Gets a chat client from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns an instance of <see cref="IChatClient"/>.</returns>
    public static async Task<IChatClient?> GetChatClientAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        IChatClientFactory? factory = serviceProvider.GetServiceInternal<IChatClientFactory>();
        return factory is null
            ? null
            : await factory.CreateChatClientAsync(cancellationToken).ConfigureAwait(false);
    }
}
