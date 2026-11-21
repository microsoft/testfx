// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestHostManagerTests
{
    [TestMethod]
    public async Task BuildDataConsumers_ReportsRegistrationOrderAcrossSimpleAndCompositeFactories()
    {
        TestHostManager manager = new();
        IDataConsumer first = CreateConsumer("first");
        IDataConsumer second = CreateConsumer("second");
        IDataConsumer third = CreateConsumer("third");
        manager.AddDataConsumer(_ => first);
        manager.AddDataConsumer(new CompositeExtensionFactory<IDataConsumer>(() => second));
        manager.AddDataConsumer(_ => third);

        (IExtension Consumer, int RegistrationOrder)[] built = await manager.BuildDataConsumersAsync(new ServiceProvider(), []);

        Assert.HasCount(3, built);
        Assert.AreEqual(0, GetOrder(built, first));
        Assert.AreEqual(1, GetOrder(built, second));
        Assert.AreEqual(2, GetOrder(built, third));
    }

    [TestMethod]
    public void AddDataConsumer_SameCompositeFactoryTwice_Throws()
    {
        TestHostManager manager = new();
        CompositeExtensionFactory<IDataConsumer> factory = new(() => CreateConsumer("composite"));
        manager.AddDataConsumer(factory);

        Assert.ThrowsExactly<ArgumentException>(() => manager.AddDataConsumer(factory));
    }

    [TestMethod]
    public async Task BuildTestSessionLifetimeHandlers_ReportsRegistrationOrderAcrossSimpleAndCompositeFactories()
    {
        TestHostManager manager = new();
        ITestSessionLifetimeHandler first = CreateSessionHandler("first");
        ITestSessionLifetimeHandler second = CreateSessionHandler("second");
        ITestSessionLifetimeHandler third = CreateSessionHandler("third");
        manager.AddTestSessionLifetimeHandler(_ => first);
        manager.AddTestSessionLifetimeHandler(new CompositeExtensionFactory<ITestSessionLifetimeHandler>(() => second));
        manager.AddTestSessionLifetimeHandler(_ => third);

        (IExtension TestSessionLifetimeHandler, int RegistrationOrder)[] built = await manager.BuildTestSessionLifetimeHandleAsync(new ServiceProvider(), []);

        Assert.HasCount(3, built);
        Assert.AreEqual(0, GetOrder(built, first));
        Assert.AreEqual(1, GetOrder(built, second));
        Assert.AreEqual(2, GetOrder(built, third));
    }

    [TestMethod]
    public void AddTestSessionLifetimeHandler_SameCompositeFactoryTwice_Throws()
    {
        TestHostManager manager = new();
        CompositeExtensionFactory<ITestSessionLifetimeHandler> factory = new(() => CreateSessionHandler("composite"));
        manager.AddTestSessionLifetimeHandler(factory);

        Assert.ThrowsExactly<ArgumentException>(() => manager.AddTestSessionLifetimeHandler(factory));
    }

    private static int GetOrder((IExtension Extension, int RegistrationOrder)[] built, IExtension extension)
        => built.Single(x => ReferenceEquals(x.Extension, extension)).RegistrationOrder;

    private static IDataConsumer CreateConsumer(string uid)
    {
        Mock<IDataConsumer> mock = new();
        mock.SetupGet(x => x.Uid).Returns(uid);
        mock.SetupGet(x => x.DisplayName).Returns(uid);
        mock.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        return mock.Object;
    }

    private static ITestSessionLifetimeHandler CreateSessionHandler(string uid)
    {
        Mock<ITestSessionLifetimeHandler> mock = new();
        mock.SetupGet(x => x.Uid).Returns(uid);
        mock.SetupGet(x => x.DisplayName).Returns(uid);
        mock.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        return mock.Object;
    }
}
