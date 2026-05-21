// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.AI;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

[TestClass]
public sealed class ChatClientProviderExtensionsTests
{
    [TestMethod]
    public async Task GetChatClientAsync_WhenProviderIsUnavailable_ReturnsNull()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new UnavailableChatClientProvider());

        IChatClient? chatClient = await serviceProvider.GetChatClientAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(chatClient);
    }

    private sealed class UnavailableChatClientProvider : IChatClientProvider
    {
        public bool IsAvailable => false;

        public bool HasToolsCapability => false;

        public string ModelName => "Unavailable";

        public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("CreateChatClientAsync should not be called for unavailable providers.");
    }
}

#pragma warning restore TPEXP
