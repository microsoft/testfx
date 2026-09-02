// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.DynamicExtensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// A builder for test applications and services.
/// </summary>
internal sealed class TestApplicationBuilder : IArtifactPostProcessingApplicationBuilder, IDynamicExtensionRegistrationGuard
{
    private readonly DateTimeOffset _createBuilderStart;
    private readonly ApplicationLoggingState _loggingState;
    private readonly TestApplicationOptions _testApplicationOptions;
    private readonly IUnhandledExceptionsHandler _unhandledExceptionsHandler;
    private readonly TestHostBuilder _testHostBuilder;
    private IHost? _host;
    private Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework>? _testFrameworkFactory;
    private Func<IServiceProvider, ITestFrameworkCapabilities>? _testFrameworkCapabilitiesFactory;
    private DynamicExtensionScope? _currentDynamicExtension;

    internal TestApplicationBuilder(
        ApplicationLoggingState loggingState,
        DateTimeOffset createBuilderStart,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        string[] args)
        : this(loggingState, createBuilderStart, testApplicationOptions, unhandledExceptionsHandler, args, args)
    {
    }

    internal TestApplicationBuilder(
        ApplicationLoggingState loggingState,
        DateTimeOffset createBuilderStart,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        string[] args,
        string[] expandedArgs)
    {
        _testHostBuilder = new TestHostBuilder(new SystemFileSystem(), new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler(), new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler(), args, expandedArgs));
        _createBuilderStart = createBuilderStart;
        _loggingState = loggingState;
        _testApplicationOptions = testApplicationOptions;
        _unhandledExceptionsHandler = unhandledExceptionsHandler;
    }

    public IChatClientManager ChatClientManager => _testHostBuilder.ChatClientManager;

    public ITestHostManager TestHost => _testHostBuilder.TestHost;

    public ITestHostControllersManager TestHostControllers => _testHostBuilder.TestHostControllers;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    ITestHostOrchestratorManager ITestApplicationBuilder.TestHostOrchestrator => _testHostBuilder.TestHostOrchestrator;

    // Binary backward compatibility: old extensions access this property on the concrete class.
    internal Extensions.TestHostOrchestrator.ITestHostOrchestratorManager TestHostOrchestrator => (Extensions.TestHostOrchestrator.ITestHostOrchestratorManager)_testHostBuilder.TestHostOrchestrator;

    public ICommandLineManager CommandLine => _testHostBuilder.CommandLine;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public IConfigurationManager Configuration => _testHostBuilder.Configuration;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public ILoggingManager Logging => _testHostBuilder.Logging;

    internal ITelemetryManager Telemetry => _testHostBuilder.Telemetry;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public IArtifactPostProcessingManager ArtifactPostProcessing => _testHostBuilder.ArtifactPostProcessing;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public IToolsManager Tools => _testHostBuilder.Tools;

    public ITestApplicationBuilder RegisterTestFramework(
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> frameworkFactory)
    {
        if (_currentDynamicExtension is { } dynamicExtension)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionCannotRegisterTestFrameworkErrorMessage,
                dynamicExtension.DisplayName,
                dynamicExtension.ManifestPath));
        }

        if (frameworkFactory is null)
        {
            throw new ArgumentNullException(nameof(frameworkFactory));
        }

        if (capabilitiesFactory is null)
        {
            throw new ArgumentNullException(nameof(capabilitiesFactory));
        }

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

    // The scope covers the synchronous execution of the hook, which is the whole window in which a hook is
    // contracted to touch the builder. ITestApplicationBuilder is not thread-safe, so a hook that captures the
    // builder and uses it from a background task after returning is already outside the contract for every
    // member, not just RegisterTestFramework. That is also why the guard is not flowed through the execution
    // context: it would single out one member, would not survive ExecutionContext.SuppressFlow anyway, and
    // would surface as an unhandled exception on a thread pool thread rather than an actionable startup error.
    IDisposable IDynamicExtensionRegistrationGuard.EnterDynamicExtensionScope(string displayName, string manifestPath)
    {
        DynamicExtensionScope scope = new(this, displayName, manifestPath);
        _currentDynamicExtension = scope;
        return scope;
    }

    private sealed class DynamicExtensionScope : IDisposable
    {
        private readonly TestApplicationBuilder _owner;

        public DynamicExtensionScope(TestApplicationBuilder owner, string displayName, string manifestPath)
        {
            _owner = owner;
            DisplayName = displayName;
            ManifestPath = manifestPath;
        }

        public string DisplayName { get; }

        public string ManifestPath { get; }

        public void Dispose() => _owner._currentDynamicExtension = null;
    }
}
