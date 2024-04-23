// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
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
    private readonly string[] _args;
    private readonly DateTimeOffset _createBuilderStart;
    private readonly ApplicationLoggingState _loggingState;
    private readonly TestApplicationOptions _testApplicationOptions;
    private readonly IUnhandledExceptionsHandler _unhandledExceptionsHandler;
    private readonly ITestHostBuilder _testHostBuilder;
    private ITestHost? _testHost;
    private Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework>? _testFrameworkAdapterFactory;
    private Func<IServiceProvider, ITestFrameworkCapabilities>? _testFrameworkCapabilitiesFactory;

    internal TestApplicationBuilder(
        string[] args,
        ApplicationLoggingState loggingState,
        DateTimeOffset createBuilderStart,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler)
    {
        _testHostBuilder = TestHostBuilderFactory();
        _args = args;
        _createBuilderStart = createBuilderStart;
        _loggingState = loggingState;
        _testApplicationOptions = testApplicationOptions;
        _unhandledExceptionsHandler = unhandledExceptionsHandler;
    }

    public ITestHostManager TestHost => _testHostBuilder.TestHost;

    public ITestHostControllersManager TestHostControllers => _testHostBuilder.TestHostControllers;

    public ICommandLineManager CommandLine => _testHostBuilder.CommandLine;

    internal IServerModeManager ServerMode => _testHostBuilder.ServerMode;

    internal ITestHostOrchestratorManager TestHostControllersManager => _testHostBuilder.TestHostOrchestratorManager;

    internal IConfigurationManager Configuration => _testHostBuilder.Configuration;

    internal ILoggingManager Logging => _testHostBuilder.Logging;

    internal IPlatformOutputDeviceManager OutputDisplay => _testHostBuilder.OutputDisplay;

    internal ITelemetryManager TelemetryManager => _testHostBuilder.Telemetry;

    internal IToolsManager Tools => _testHostBuilder.Tools;

    // Callers from the outside can substitute the builder and the services that are used
    // to build a testhost. We use this to provide a different builder for VSTest tests.
    internal static Func<ITestHostBuilder> TestHostBuilderFactory { get; set; }
        = () => new TestHostBuilder(new SystemFileSystem(), new SystemEnvironment(), new SystemProcessHandler(), new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()));

    public ITestApplicationBuilder RegisterTestFramework(
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> adapterFactory)
    {
        ArgumentGuard.IsNotNull(adapterFactory);
        ArgumentGuard.IsNotNull(capabilitiesFactory);

        if (_testFrameworkAdapterFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderFrameworkAdapterFactoryAlreadyRegisteredErrorMessage);
        }

        _testFrameworkAdapterFactory = adapterFactory;

        if (_testFrameworkCapabilitiesFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderTestFrameworkCapabilitiesAlreadyRegistered);
        }

        _testFrameworkCapabilitiesFactory = capabilitiesFactory;

        _testHostBuilder.TestFramework
            = new TestFrameworkManager(_testFrameworkAdapterFactory, _testFrameworkCapabilitiesFactory);

        return this;
    }

    public async Task<ITestApplication> BuildAsync()
    {
        if (_testFrameworkAdapterFactory is null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderTestFrameworkNotRegistered);
        }

        if (_testHost is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestApplicationBuilderApplicationAlreadyRegistered);
        }

        _testHost = await _testHostBuilder.BuildAsync(
            _args,
            _loggingState,
            _testApplicationOptions,
            _unhandledExceptionsHandler,
            _createBuilderStart);

        return new TestApplication(_testHost);
    }
}
