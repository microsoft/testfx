// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.InvocationLifetime;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.InvocationLifetime;

#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.

[TestClass]
public sealed class InvocationLifetimeManagerTests
{
    [TestMethod]
    public void AddInvocationLifetimeHandler_NullFactory_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new InvocationLifetimeManager().AddInvocationLifetimeHandler(null!));

    [TestMethod]
    public void HasHandlers_ReflectsRegistrations()
    {
        InvocationLifetimeManager manager = new();
        Assert.IsFalse(manager.HasHandlers);

        manager.AddInvocationLifetimeHandler(_ => CreateHandler("a", enabled: true).Object);

        Assert.IsTrue(manager.HasHandlers);
    }

    [TestMethod]
    public async Task BuildAsync_SkipsDisabledHandlers_AndInitializesEnabledOnesInOrder()
    {
        InvocationLifetimeManager manager = new();
        IServiceProvider serviceProvider = Mock.Of<IServiceProvider>();
        IServiceProvider? receivedServiceProvider = null;
        Mock<IInvocationLifetimeHandler> first = CreateHandler("a", enabled: true);
        Mock<IInvocationLifetimeHandler> disabled = CreateHandler("b", enabled: false);
        Mock<IInvocationLifetimeHandler> third = CreateHandler("c", enabled: true);
        Mock<IAsyncInitializableExtension> initializable = third.As<IAsyncInitializableExtension>();
        initializable.Setup(h => h.InitializeAsync()).Returns(Task.CompletedTask);
        manager.AddInvocationLifetimeHandler(sp =>
        {
            receivedServiceProvider = sp;
            return first.Object;
        });
        manager.AddInvocationLifetimeHandler(_ => disabled.Object);
        manager.AddInvocationLifetimeHandler(_ => third.Object);

        IReadOnlyList<IInvocationLifetimeHandler> handlers = await manager.BuildAsync(serviceProvider);

        Assert.AreSequenceEqual(new[] { first.Object, third.Object }, handlers);
        Assert.AreSame(serviceProvider, receivedServiceProvider);
        initializable.Verify(h => h.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_DuplicateUid_Throws()
    {
        InvocationLifetimeManager manager = new();
        manager.AddInvocationLifetimeHandler(_ => CreateHandler("same", enabled: true).Object);
        manager.AddInvocationLifetimeHandler(_ => CreateHandler("same", enabled: true).Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(Mock.Of<IServiceProvider>()));
    }

    [TestMethod]
    public async Task BuildAsync_DisposesDisabledHandlers()
    {
        InvocationLifetimeManager manager = new();
        Mock<IInvocationLifetimeHandler> disabled = CreateHandler("a", enabled: false);
        Mock<IDisposable> disposable = disabled.As<IDisposable>();
        manager.AddInvocationLifetimeHandler(_ => disabled.Object);

        IReadOnlyList<IInvocationLifetimeHandler> handlers = await manager.BuildAsync(Mock.Of<IServiceProvider>());

        Assert.IsEmpty(handlers);
        disposable.Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WhenALaterHandlerFails_DisposesTheHandlersAlreadyBuilt()
    {
        InvocationLifetimeManager manager = new();
        Mock<IInvocationLifetimeHandler> first = CreateHandler("a", enabled: true);
        Mock<IDisposable> firstDisposable = first.As<IDisposable>();
        Mock<IInvocationLifetimeHandler> duplicate = CreateHandler("a", enabled: true);
        Mock<IDisposable> duplicateDisposable = duplicate.As<IDisposable>();
        manager.AddInvocationLifetimeHandler(_ => first.Object);
        manager.AddInvocationLifetimeHandler(_ => duplicate.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(Mock.Of<IServiceProvider>()));

        firstDisposable.Verify(d => d.Dispose(), Times.Once);
        duplicateDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WhenInitializationAndDisposalFail_PreservesInitializationFailureAndAttemptsEveryDisposal()
    {
        InvocationLifetimeManager manager = new();
        Mock<IInvocationLifetimeHandler> first = CreateHandler("a", enabled: true);
        Mock<IDisposable> firstDisposable = first.As<IDisposable>();
        firstDisposable.Setup(d => d.Dispose()).Throws(new ObjectDisposedException("first"));
        Mock<IInvocationLifetimeHandler> second = CreateHandler("b", enabled: true);
        Mock<IDisposable> secondDisposable = second.As<IDisposable>();
        Mock<IInvocationLifetimeHandler> failing = CreateHandler("c", enabled: true);
        Mock<IAsyncInitializableExtension> initializable = failing.As<IAsyncInitializableExtension>();
        InvalidOperationException initializationFailure = new("Initialization failed.");
        initializable.Setup(h => h.InitializeAsync()).ThrowsAsync(initializationFailure);
        Mock<IDisposable> failingDisposable = failing.As<IDisposable>();
        failingDisposable.Setup(d => d.Dispose()).Throws(new ObjectDisposedException("failing"));
        manager.AddInvocationLifetimeHandler(_ => first.Object);
        manager.AddInvocationLifetimeHandler(_ => second.Object);
        manager.AddInvocationLifetimeHandler(_ => failing.Object);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => manager.BuildAsync(Mock.Of<IServiceProvider>()));

        Assert.AreSame(initializationFailure, actual);
        firstDisposable.Verify(d => d.Dispose(), Times.Once);
        secondDisposable.Verify(d => d.Dispose(), Times.Once);
        failingDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    private static Mock<IInvocationLifetimeHandler> CreateHandler(string uid, bool enabled)
    {
        Mock<IInvocationLifetimeHandler> handler = new();
        handler.SetupGet(h => h.Uid).Returns(uid);
        handler.Setup(h => h.IsEnabledAsync()).ReturnsAsync(enabled);
        return handler;
    }
}
