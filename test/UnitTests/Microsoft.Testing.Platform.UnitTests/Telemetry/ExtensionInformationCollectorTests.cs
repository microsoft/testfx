// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionInformationCollectorTests
{
    [TestMethod]
    public async Task CollectAndSerializeToJsonAsync_IncludesExtensionsRegisteredOnlyAsMessageBusDataConsumers()
    {
        Mock<IDataConsumer> consumer = new();
        Mock<IExtension> extension = consumer.As<IExtension>();
        extension.SetupGet(e => e.Uid).Returns("consumer-extension");
        extension.SetupGet(e => e.Version).Returns("4.5.6");
        extension.Setup(e => e.IsEnabledAsync()).ReturnsAsync(true);
        Mock<BaseMessageBus> bus = new();
        bus.SetupGet(b => b.DataConsumerServices).Returns([consumer.Object]);
        MessageBusProxy proxy = new();
        proxy.SetBuiltMessageBus(bus.Object);
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(proxy);

        string? json = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(serviceProvider);

        if (json is null)
        {
            Assert.Inconclusive("Extension information is not collected on this platform.");
        }

        Assert.AreEqual(
            $"[{{\"Uid\":\"{Sha256Hasher.HashWithNormalizedCasing("consumer-extension")}\",\"Version\":\"4.5.6\",\"Enabled\":true}}]",
            json.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty));
    }
}
