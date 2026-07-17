// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CompositeExtensionFactoryTests
{
    [TestMethod]
    public void GetInstance_WhenFactoryThrows_WrapsWithActionableMessageAndPreservesInnerException()
    {
        InvalidOperationException originalException = new("Boom from the factory");
        CompositeExtensionFactory<TestExtension> factory = new(() => throw originalException);

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(
            () => ((ICompositeExtensionFactory)factory).GetInstance());

        // The outer message must identify the extension type that failed to build, so a user can immediately
        // tell which extension is broken without having to guess from a generic "Initialization failed" message.
        Assert.Contains(nameof(TestExtension), thrown.Message);

        // The full original failure must never be swallowed: it must be reachable as InnerException.
        Assert.AreSame(originalException, thrown.InnerException);
    }

    [TestMethod]
    public void GetInstance_WithServiceProviderFactory_WhenFactoryThrows_WrapsWithActionableMessageAndPreservesInnerException()
    {
        InvalidOperationException originalException = new("Boom from the service-provider factory");
        CompositeExtensionFactory<TestExtension> factory = new(_ => throw originalException);

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(
            () => ((ICompositeExtensionFactory)factory).GetInstance(new ServiceProvider()));

        Assert.Contains(nameof(TestExtension), thrown.Message);
        Assert.AreSame(originalException, thrown.InnerException);
    }

    [TestMethod]
    public void GetInstance_WhenFactoryIsCancelled_PreservesCancellationException()
    {
        OperationCanceledException cancellationException = new("Factory was cancelled.");
        CompositeExtensionFactory<TestExtension> factory = new(() => throw cancellationException);

        OperationCanceledException thrown = Assert.ThrowsExactly<OperationCanceledException>(
            () => ((ICompositeExtensionFactory)factory).GetInstance());

        Assert.AreSame(cancellationException, thrown);
    }

    [TestMethod]
    public void GetInstance_WhenFactoryReturnsNull_ThrowsActionableMessageIdentifyingExtensionType()
    {
        CompositeExtensionFactory<TestExtension> factory = new(() => null!);

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(
            () => ((ICompositeExtensionFactory)factory).GetInstance());

        // No exception was thrown by the factory itself here, so there is nothing to preserve as InnerException,
        // but the message must still make it obvious which extension's factory misbehaved.
        Assert.Contains(nameof(TestExtension), thrown.Message);
        Assert.IsNull(thrown.InnerException);
    }

    [TestMethod]
    public void GetInstance_WhenFactorySucceeds_ReturnsInstanceAndCachesIt()
    {
        int callCount = 0;
        CompositeExtensionFactory<TestExtension> factory = new(() =>
        {
            callCount++;
            return new TestExtension();
        });

        object first = ((ICompositeExtensionFactory)factory).GetInstance();
        object second = ((ICompositeExtensionFactory)factory).GetInstance();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, callCount);
    }
}
