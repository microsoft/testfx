// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.AI.Resources;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Extension methods for chat client provider.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class ChatClientProviderExtensions
{
    /// <summary>
    /// Adds a chat client provider to the test application builder.
    /// </summary>
    /// <remarks>
    /// Only one chat client provider can be registered per test application.
    /// </remarks>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="chatClientProvider">The factory function to create chat client providers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="testApplicationBuilder"/> or <paramref name="chatClientProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the builder does not support AI extensions or a chat client provider has already been registered.</exception>
    public static void AddChatClientProvider(this ITestApplicationBuilder testApplicationBuilder, Func<IServiceProvider, IChatClientProvider> chatClientProvider)
    {
        _ = testApplicationBuilder ?? throw new ArgumentNullException(nameof(testApplicationBuilder));
        _ = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));

        if (testApplicationBuilder is not TestApplicationBuilder builder)
        {
            throw new InvalidOperationException(AIExtensionResources.InvalidTestApplicationBuilderTypeForAI);
        }

        builder.ChatClientManager.AddChatClientProvider(chatClientProvider);
    }

    /// <summary>
    /// Gets a chat client from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns an instance of <see cref="IChatClient"/>, or <see langword="null"/> when no available provider is registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public static async Task<IChatClient?> GetChatClientAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        var provider = (IChatClientProvider?)serviceProvider.GetService(typeof(IChatClientProvider));
        return provider is null || !provider.IsAvailable
            ? null
            : await provider.CreateChatClientAsync(cancellationToken).ConfigureAwait(false);
    }
}
