// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Extension methods for AI-related functionality.
/// </summary>
public static class AIExtensions
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
    /// <returns>An instance of <see cref="IChatClient"/>.</returns>
    public static IChatClient? GetChatClient(this IServiceProvider serviceProvider)
        => serviceProvider.GetServiceInternal<IChatClientFactory>()?.CreateChatClient();
}
