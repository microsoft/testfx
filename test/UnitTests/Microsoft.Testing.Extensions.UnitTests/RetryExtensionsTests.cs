// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using TestHostOrchestratorManager = Microsoft.Testing.Platform.TestHostOrchestrator.TestHostOrchestratorManager;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryExtensionsTests
{
    [TestMethod]
    public async Task AddRetryProvider_RegistersRetryCommandLineOptionsProvider()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        builder.AddRetryProvider();

        var commandLineManager = (CommandLineManager)builder.CommandLine;
        FieldInfo factoryField = typeof(CommandLineManager).GetField(
            "_commandLineProviderFactory",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var factories = (List<Func<IServiceProvider, ICommandLineOptionsProvider>>)factoryField.GetValue(commandLineManager)!;
        ICommandLineOptionsProvider provider = factories
            .Select(factory => factory(new ServiceProvider()))
            .Single(provider => provider is RetryCommandLineOptionsProvider);

        Assert.IsInstanceOfType<RetryCommandLineOptionsProvider>(provider);
    }

    [TestMethod]
    public async Task AddRetryProvider_RegistersLifecycleAndSharesRetryDataConsumerInstance()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        builder.AddRetryProvider();
        var testHostManager = (TestHostManager)builder.TestHost;
        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(new TestCommandLineOptions(new()
        {
            [RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = ["1"],
            [RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = ["pipe"],
        }));
        serviceProvider.AddService(new SystemEnvironment());

        RetryLifecycleCallbacks lifecycle = Assert.IsInstanceOfType<RetryLifecycleCallbacks>(
            (await testHostManager.BuildTestApplicationLifecycleCallbackAsync(serviceProvider)).Single());
        serviceProvider.AddService(lifecycle);
        List<ICompositeExtensionFactory> alreadyBuiltServices = [];

        RetryDataConsumer dataConsumer = Assert.IsInstanceOfType<RetryDataConsumer>(
            (await testHostManager.BuildDataConsumersAsync(serviceProvider, alreadyBuiltServices)).Single().Consumer);
        RetryDataConsumer sessionLifetimeHandler = Assert.IsInstanceOfType<RetryDataConsumer>(
            (await testHostManager.BuildTestSessionLifetimeHandleAsync(serviceProvider, alreadyBuiltServices)).Single().TestSessionLifetimeHandler);

        Assert.AreSame(dataConsumer, sessionLifetimeHandler);
    }

    [TestMethod]
    public async Task AddRetryProvider_RegistersSingleRetryOrchestrator()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        builder.AddRetryProvider();

        var orchestratorManager = (TestHostOrchestratorManager)builder.TestHostOrchestrator;
        FieldInfo factoriesField = typeof(TestHostOrchestratorManager).GetField(
            "_factories",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var factories = (List<Func<IServiceProvider, ITestHostExecutionOrchestrator>>)factoriesField.GetValue(orchestratorManager)!;
        Assert.HasCount(1, factories);
        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(new TestCommandLineOptions(new()
        {
            [RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = ["1"],
        }));
        serviceProvider.AddService(new SystemFileSystem());

        ITestHostExecutionOrchestrator orchestrator = factories.Single()(serviceProvider);

        Assert.IsInstanceOfType<RetryOrchestrator>(orchestrator);
    }
}
