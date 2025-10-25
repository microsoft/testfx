﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;

namespace Microsoft.Testing.Platform.AI;

/// <summary>
/// Factory interface for creating chat clients.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns an instance of <see cref="IChatClient"/>.</returns>
    Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken);
}
