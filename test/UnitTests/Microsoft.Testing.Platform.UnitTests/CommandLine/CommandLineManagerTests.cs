// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

#pragma warning disable TPEXP // Type is for evaluation purposes only

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineManagerTests
{
    [TestMethod]
    public void AddProvider_NullFactory_ThrowsArgumentNullException()
    {
        CommandLineManager manager = CreateManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddProvider((Func<ICommandLineOptionsProvider>)null!));

        Assert.AreEqual("commandLineProviderFactory", exception.ParamName);
    }

    [TestMethod]
    public void AddProvider_NullServiceProviderFactory_ThrowsArgumentNullException()
    {
        CommandLineManager manager = CreateManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddProvider((Func<IServiceProvider, ICommandLineOptionsProvider>)null!));

        Assert.AreEqual("commandLineProviderFactory", exception.ParamName);
    }

    [TestMethod]
    public async Task BuildAsync_WithoutRegisteredProviders_HasNoExtensionProvidersButHasSystemProviders()
    {
        CommandLineManager manager = CreateManager();

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, new ServiceProvider(), Mock.Of<IConfiguration>());

        Assert.IsEmpty(handler.ExtensionsCommandLineOptionsProviders);
        Assert.IsNotEmpty(handler.SystemCommandLineOptionsProviders);
    }

    [TestMethod]
    public async Task BuildAsync_ProviderFactoryWithoutServiceProvider_IsInvokedAndEnabledProviderIsInitializedAndRegistered()
    {
        CommandLineManager manager = CreateManager();
        var provider = new FakeProvider("enabled", isEnabled: true);
        int factoryCalls = 0;
        manager.AddProvider(() =>
        {
            factoryCalls++;
            return provider;
        });

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, new ServiceProvider(), Mock.Of<IConfiguration>());

        Assert.AreEqual(1, factoryCalls);
        Assert.AreEqual(1, provider.InitializeCalls);
        ICommandLineOptionsProvider registered = Assert.ContainsSingle(handler.ExtensionsCommandLineOptionsProviders);
        Assert.AreEqual("enabled", registered.Uid);
        Assert.IsFalse(registered is IToolCommandLineOptionsProvider);
    }

    [TestMethod]
    public async Task BuildAsync_ServiceProviderFactory_ReceivesTheServiceProvider()
    {
        CommandLineManager manager = CreateManager();
        var services = new ServiceProvider();
        IServiceProvider? received = null;
        var provider = new FakeProvider("with-services", isEnabled: true);
        manager.AddProvider(serviceProvider =>
        {
            received = serviceProvider;
            return provider;
        });

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, services, Mock.Of<IConfiguration>());

        Assert.AreSame(services, received);
        Assert.AreEqual(1, provider.InitializeCalls);
        Assert.AreEqual("with-services", Assert.ContainsSingle(handler.ExtensionsCommandLineOptionsProviders).Uid);
    }

    [TestMethod]
    public async Task BuildAsync_DisabledProvider_IsNeitherInitializedNorRegistered_AndFollowingProvidersAreStillProcessed()
    {
        CommandLineManager manager = CreateManager();
        var disabled = new FakeProvider("disabled", isEnabled: false);
        var enabled = new FakeProvider("enabled", isEnabled: true);
        manager.AddProvider(() => disabled);
        manager.AddProvider(() => enabled);

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, new ServiceProvider(), Mock.Of<IConfiguration>());

        Assert.AreEqual(0, disabled.InitializeCalls);
        Assert.AreEqual(1, enabled.InitializeCalls);
        Assert.AreEqual("enabled", Assert.ContainsSingle(handler.ExtensionsCommandLineOptionsProviders).Uid);
    }

    [TestMethod]
    public async Task BuildAsync_RegistersProvidersInRegistrationOrder_AndKeepsToolProvidersAsToolProviders()
    {
        CommandLineManager manager = CreateManager();
        manager.AddProvider(() => new FakeProvider("first", isEnabled: true));
        manager.AddProvider(() => new FakeToolProvider("tool", isEnabled: true));
        manager.AddProvider(() => new FakeProvider("last", isEnabled: true));

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, new ServiceProvider(), Mock.Of<IConfiguration>());

        ICommandLineOptionsProvider[] providers = [.. handler.ExtensionsCommandLineOptionsProviders];
        Assert.AreSequenceEqual(["first", "tool", "last"], providers.Select(p => p.Uid));
        Assert.IsFalse(providers[0] is IToolCommandLineOptionsProvider);
        Assert.IsTrue(providers[1] is IToolCommandLineOptionsProvider);
        Assert.AreEqual("tool-name", ((IToolCommandLineOptionsProvider)providers[1]).ToolName);
        Assert.IsFalse(providers[2] is IToolCommandLineOptionsProvider);
    }

    [TestMethod]
    public async Task BuildAsync_ProviderOptionsAreCachedAcrossCalls()
    {
        CommandLineManager manager = CreateManager();
        var provider = new FakeProvider("cached", isEnabled: true);
        manager.AddProvider(() => provider);

        CommandLineHandler handler = await manager.BuildAsync(CommandLineParseResult.Empty, new ServiceProvider(), Mock.Of<IConfiguration>());
        ICommandLineOptionsProvider registered = Assert.ContainsSingle(handler.ExtensionsCommandLineOptionsProviders);

        _ = registered.GetCommandLineOptions();
        _ = registered.GetCommandLineOptions();

        Assert.AreEqual(1, provider.GetOptionsCalls);
    }

    private static CommandLineManager CreateManager()
        => new(Mock.Of<IRuntimeFeature>(), Mock.Of<ITestApplicationModuleInfo>());

    private class FakeProvider(string uid, bool isEnabled) : ICommandLineOptionsProvider, IAsyncInitializableExtension
    {
        public int InitializeCalls { get; private set; }

        public int GetOptionsCalls { get; private set; }

        public string Uid => uid;

        public string Version => "1.0";

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(isEnabled);

        public Task InitializeAsync()
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        {
            GetOptionsCalls++;
            return [];
        }

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
            => ValidationResult.ValidTask;

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
            => ValidationResult.ValidTask;
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only
    private sealed class FakeToolProvider(string uid, bool isEnabled) : FakeProvider(uid, isEnabled), IToolCommandLineOptionsProvider
    {
        public string ToolName => "tool-name";
    }
#pragma warning restore TPEXP
}
