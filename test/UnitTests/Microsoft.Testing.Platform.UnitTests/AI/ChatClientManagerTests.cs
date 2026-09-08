// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.AI;

[TestClass]
public sealed class ChatClientManagerTests
{
    [TestMethod]
    public void AddChatClientProvider_WhenFactoryIsNull_Throws()
    {
        ChatClientManager manager = new();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddChatClientProvider(null!));

        Assert.AreEqual("chatClientProviderFactory", exception.ParamName);
    }

    [TestMethod]
    public void AddChatClientProvider_WhenProviderAlreadyRegistered_Throws()
    {
        ChatClientManager manager = new();
        manager.AddChatClientProvider(_ => new TestChatClientProvider());

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => manager.AddChatClientProvider(_ => new TestChatClientProvider()));

        Assert.AreEqual("A chat client provider has already been registered.", exception.Message);
    }

    [TestMethod]
    public void BuildChatClients_WhenNoProviderIsRegistered_DoesNotAddAProvider()
    {
        ChatClientManager manager = new();
        ServiceProvider serviceProvider = new();

        manager.BuildChatClients(serviceProvider);

        Assert.IsNull(serviceProvider.GetService(typeof(IChatClientProvider)));
    }

    [TestMethod]
    public void BuildChatClients_WhenProviderIsRegistered_InvokesFactoryAndRegistersProvider()
    {
        ChatClientManager manager = new();
        ServiceProvider serviceProvider = new();
        TestChatClientProvider provider = new();
        IServiceProvider? factoryServiceProvider = null;
        int factoryCallCount = 0;
        manager.AddChatClientProvider(serviceProvider =>
        {
            factoryServiceProvider = serviceProvider;
            factoryCallCount++;
            return provider;
        });

        manager.BuildChatClients(serviceProvider);

        Assert.AreEqual(1, factoryCallCount);
        Assert.AreSame(serviceProvider, factoryServiceProvider);
        Assert.AreSame(provider, serviceProvider.GetService(typeof(IChatClientProvider)));
    }

    private sealed class TestChatClientProvider : IChatClientProvider
    {
        public bool IsAvailable => true;

        public bool HasToolsCapability => false;

        public string ModelName => "Test";

        public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
