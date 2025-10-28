// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Provider interface for creating and configuring chat clients.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/experimental")]
public interface IChatClientProvider
{
    /// <summary>
    /// Gets a value indicating whether the provider is available and can be used.
    /// A provider may be registered but not available if required configuration (e.g., environment variables) is missing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether the chat client has tool capability (e.g., MCP tools or functions).
    /// </summary>
    bool HasToolsCapability { get; }

    /// <summary>
    /// Gets the name of the model being used by the chat client.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns an instance of <see cref="IChatClient"/>.</returns>
    Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken);
}
