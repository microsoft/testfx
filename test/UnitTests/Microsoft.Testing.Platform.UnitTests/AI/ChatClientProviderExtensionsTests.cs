// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.AI;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

[TestClass]
public sealed class ChatClientProviderExtensionsTests
{
    [TestMethod]
    public void AddChatClientProvider_WhenBuilderIsNotTestApplicationBuilder_Throws()
    {
        ITestApplicationBuilder builder = new Mock<ITestApplicationBuilder>().Object;

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.AddChatClientProvider(_ => new UnavailableChatClientProvider()));

        Assert.AreEqual("AI extensions only work with builders of type 'Microsoft.Testing.Platform.Builder.TestApplicationBuilder'", exception.Message);
    }

    [TestMethod]
    public void AddChatClientProvider_RegistersProviderWithBuilder()
    {
        TestApplicationBuilder builder = CreateBuilder();
        UnavailableChatClientProvider provider = new();
        ServiceProvider serviceProvider = new();

        builder.AddChatClientProvider(_ => provider);
        builder.ChatClientManager.BuildChatClients(serviceProvider);

        Assert.AreSame(provider, serviceProvider.GetService(typeof(IChatClientProvider)));
    }

    [TestMethod]
    public void AddChatClientProvider_WhenProviderAlreadyRegistered_Throws()
    {
        TestApplicationBuilder builder = CreateBuilder();
        builder.AddChatClientProvider(_ => new UnavailableChatClientProvider());

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.AddChatClientProvider(_ => new UnavailableChatClientProvider()));

        Assert.AreEqual("A chat client provider has already been registered.", exception.Message);
    }

    [TestMethod]
    public async Task GetChatClientAsync_WhenProviderIsUnavailable_ReturnsNull()
    {
        ServiceProvider serviceProvider = new();
        UnavailableChatClientProvider provider = new();
        serviceProvider.AddService(provider);

        IChatClient? chatClient = await serviceProvider.GetChatClientAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(chatClient);
        Assert.AreEqual(1, provider.IsAvailableCallCount);
        Assert.AreEqual(0, provider.CreateChatClientAsyncCallCount);
    }

    private sealed class UnavailableChatClientProvider : IChatClientProvider
    {
        public int IsAvailableCallCount { get; private set; }

        public int CreateChatClientAsyncCallCount { get; private set; }

        public bool IsAvailable
        {
            get
            {
                IsAvailableCallCount++;
                return false;
            }
        }

        public bool HasToolsCapability => false;

        public string ModelName => "Unavailable";

        public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
        {
            CreateChatClientAsyncCallCount++;
            throw new InvalidOperationException("CreateChatClientAsync should not be called for unavailable providers.");
        }
    }

    private static TestApplicationBuilder CreateBuilder()
        => new(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            new Mock<IUnhandledExceptionsHandler>().Object,
            []);
}

#pragma warning restore TPEXP
