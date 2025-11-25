// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// A builder for test applications and services.
/// </summary>
internal sealed class TestApplicationBuilder : ITestApplicationBuilder
{
    private readonly DateTimeOffset _createBuilderStart;
    private readonly ApplicationLoggingState _loggingState;
    private readonly TestApplicationOptions _testApplicationOptions;
    private readonly IUnhandledExceptionsHandler _unhandledExceptionsHandler;
    private readonly TestHostBuilder _testHostBuilder;
    private IHost? _host;
    private Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework>? _testFrameworkFactory;
    private Func<IServiceProvider, ITestFrameworkCapabilities>? _testFrameworkCapabilitiesFactory;

    internal TestApplicationBuilder(
        ApplicationLoggingState loggingState,
        DateTimeOffset createBuilderStart,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler)
    {
        _testHostBuilder = new TestHostBuilder(new SystemFileSystem(), new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler(), new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()));
        _createBuilderStart = createBuilderStart;
        _loggingState = loggingState;
        _testApplicationOptions = testApplicationOptions;
        _unhandledExceptionsHandler = unhandledExceptionsHandler;
    }

    public IChatClientManager ChatClientManager => _testHostBuilder.ChatClientManager;

    public ITestHostManager TestHost => _testHostBuilder.TestHost;

    public ITestHostControllersManager TestHostControllers => _testHostBuilder.TestHostControllers;

    public ICommandLineManager CommandLine => _testHostBuilder.CommandLine;

    internal ITestHostOrchestratorManager TestHostOrchestrator => _testHostBuilder.TestHostOrchestratorManager;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public IConfigurationManager Configuration => _testHostBuilder.Configuration;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public ILoggingManager Logging => _testHostBuilder.Logging;

    internal ITelemetryManager Telemetry => _testHostBuilder.Telemetry;

    internal IToolsManager Tools => _testHostBuilder.Tools;

    public ITestApplicationBuilder RegisterTestFramework(
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> frameworkFactory)
    {
        Guard.NotNull(frameworkFactory);
        Guard.NotNull(capabilitiesFactory);

        if (_testFrameworkFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderFrameworkAdapterFactoryAlreadyRegisteredErrorMessage);
        }

        _testFrameworkFactory = frameworkFactory;

        if (_testFrameworkCapabilitiesFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderTestFrameworkCapabilitiesAlreadyRegistered);
        }

        _testFrameworkCapabilitiesFactory = capabilitiesFactory;

        _testHostBuilder.TestFramework
            = new TestFrameworkManager(_testFrameworkFactory, _testFrameworkCapabilitiesFactory);

        return this;
    }

    public async Task<ITestApplication> BuildAsync()
    {
        if (_testFrameworkFactory is null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderTestFrameworkNotRegistered);
        }

        if (_host is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderApplicationAlreadyRegistered);
        }

        _host = await _testHostBuilder.BuildAsync(_loggingState, _testApplicationOptions, _unhandledExceptionsHandler, _createBuilderStart).ConfigureAwait(false);

        return new TestApplication(_host);
    }
}
