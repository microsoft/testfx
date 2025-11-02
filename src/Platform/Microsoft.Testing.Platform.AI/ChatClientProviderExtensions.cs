// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Extension methods for chat client provider.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class ChatClientProviderExtensions
{
    /// <summary>
    /// Adds a chat client provider to the test application builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="chatClientProvider">The factory function to create chat client providers.</param>
    public static void AddChatClientProvider(this ITestApplicationBuilder testApplicationBuilder, Func<IServiceProvider, IChatClientProvider> chatClientProvider)
    {
        if (testApplicationBuilder is not TestApplicationBuilder builder)
        {
            throw new InvalidOperationException(PlatformResources.InvalidTestApplicationBuilderTypeForAI);
        }

        builder.ChatClientManager.AddChatClientProvider(chatClientProvider);
    }

    /// <summary>
    /// Gets a chat client from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns an instance of <see cref="IChatClient"/>.</returns>
    public static async Task<IChatClient?> GetChatClientAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        IChatClientProvider? provider = serviceProvider.GetServiceInternal<IChatClientProvider>();
        return provider is null
            ? null
            : await provider.CreateChatClientAsync(cancellationToken).ConfigureAwait(false);
    }
}
