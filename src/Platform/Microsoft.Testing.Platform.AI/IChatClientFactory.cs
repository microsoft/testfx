// Copyright (c) Microsoft Corporation. All rights reserved.
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
    /// <returns>An instance of <see cref="IChatClient"/>.</returns>
    IChatClient CreateChatClient();
}
