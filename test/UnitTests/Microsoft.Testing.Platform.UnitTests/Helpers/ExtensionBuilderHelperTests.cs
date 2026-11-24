// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExtensionBuilderHelperTests
{
    [TestMethod]
    public async Task BuildAndRegisterExtensionsAsync_InitializesEnabledOnlyAndPreservesFactoryOrder()
    {
        List<string> events = [];
        Mock<IExtension> first = CreateExtension("first", true, events);
        Mock<IExtension> disabled = CreateExtension("disabled", false, events);
        Mock<IExtension> last = CreateExtension("last", true, events);
        ServiceProvider services = new();
        List<IExtension> result = [];

        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(
            new Func<IServiceProvider, IExtension>[]
            {
                provider => Build(first, provider),
                provider => Build(disabled, provider),
                provider => Build(last, provider),
            },
            services,
            result,
            registerInServiceProvider: true);

        Assert.AreSequenceEqual(new[] { first.Object, last.Object }, result);
        Assert.AreSequenceEqual(
            new[] { "factory:first", "enabled:first", "init:first", "factory:disabled", "enabled:disabled", "factory:last", "enabled:last", "init:last" },
            events);
        Assert.AreSequenceEqual(new[] { first.Object, last.Object }, services.GetServicesInternal(typeof(IExtension)).Cast<IExtension>());

        IExtension Build(Mock<IExtension> extension, IServiceProvider provider)
        {
            Assert.AreSame(services, provider);
            events.Add($"factory:{extension.Object.Uid}");
            return extension.Object;
        }
    }

    [TestMethod]
    public async Task BuildAndRegisterExtensionsAsync_InitializationFailureStopsBeforeRegistrationAndLaterFactories()
    {
        InvalidOperationException expectedFailure = new("initialize failed");
        List<string> events = [];
        Mock<IExtension> extension = CreateExtension("broken", true, events);
        extension.As<IAsyncInitializableExtension>().Setup(x => x.InitializeAsync()).ThrowsAsync(expectedFailure);
        bool laterFactoryInvoked = false;
        ServiceProvider services = new();
        List<IExtension> result = [];

        InvalidOperationException actualFailure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(
                new Func<IServiceProvider, IExtension>[]
                {
                    _ => extension.Object,
                    _ =>
                    {
                        laterFactoryInvoked = true;
                        return Mock.Of<IExtension>();
                    },
                },
                services,
                result,
                registerInServiceProvider: true));

        Assert.AreSame(expectedFailure, actualFailure);
        Assert.IsEmpty(result);
        Assert.IsEmpty(services.GetServicesInternal(typeof(IExtension)));
        Assert.IsFalse(laterFactoryInvoked);
    }

    private static Mock<IExtension> CreateExtension(string uid, bool enabled, List<string> events)
    {
        Mock<IExtension> extension = new();
        extension.SetupGet(x => x.Uid).Returns(uid);
        extension.Setup(x => x.IsEnabledAsync()).Callback(() => events.Add($"enabled:{uid}")).ReturnsAsync(enabled);
        extension.As<IAsyncInitializableExtension>().Setup(x => x.InitializeAsync()).Callback(() => events.Add($"init:{uid}")).Returns(Task.CompletedTask);
        return extension;
    }
}
