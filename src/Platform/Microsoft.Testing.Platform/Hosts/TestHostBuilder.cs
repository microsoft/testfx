// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder(IFileSystem fileSystem, IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler processHandler, ITestApplicationModuleInfo testApplicationModuleInfo) : ITestHostBuilder
{
    private const string BuilderHostTypeOTelKey = "HostType";
    private readonly IEnvironment _environment = environment;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IProcessHandler _processHandler = processHandler;
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
    private readonly PlatformOutputDeviceManager _outputDisplay = new();

    public IChatClientManager ChatClientManager { get; } = new ChatClientManager();

    public ITestFrameworkManager? TestFramework { get; set; }

    public ITestHostManager TestHost { get; } = new TestHostManager();

    public IConfigurationManager Configuration { get; } = new ConfigurationManager(fileSystem, testApplicationModuleInfo);

    public ILoggingManager Logging { get; } = new LoggingManager();

    public ICommandLineManager CommandLine { get; } = new CommandLineManager(runtimeFeature, testApplicationModuleInfo);

    public ITelemetryManager Telemetry { get; } = new TelemetryManager();

    public ITestHostControllersManager TestHostControllers { get; } = new TestHostControllersManager();

    public IToolsManager Tools { get; } = new ToolsManager();

    private readonly TestHostOrchestratorManager _testHostOrchestratorManager = new Extensions.TestHostOrchestrator.TestHostOrchestratorManager();

    public ITestHostOrchestratorManager TestHostOrchestrator => _testHostOrchestratorManager;

    public async Task<IHost> BuildAsync(
        ApplicationLoggingState loggingState,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        DateTimeOffset createBuilderStart)
    {
        ApplicationStateGuard.Ensure(TestFramework is not null);
        BuildContext context = await SetupCommonServicesAsync(loggingState, testApplicationOptions, unhandledExceptionsHandler, createBuilderStart).ConfigureAwait(false);
        return await BuildHostAsync(context).ConfigureAwait(false);
    }

    private sealed class BuildContext(
        ApplicationLoggingState loggingState,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        DateTimeOffset createBuilderStart,
        SystemClock systemClock,
        DateTimeOffset buildBuilderStart,
        Dictionary<string, object> builderMetrics,
        SystemEnvironment systemEnvironment,
        ServiceProvider serviceProvider,
        TestHostControllerInfo testHostControllerInfo)
    {
        public ApplicationLoggingState LoggingState { get; } = loggingState;

        public TestApplicationOptions TestApplicationOptions { get; } = testApplicationOptions;

        public IUnhandledExceptionsHandler UnhandledExceptionsHandler { get; } = unhandledExceptionsHandler;

        public DateTimeOffset CreateBuilderStart { get; } = createBuilderStart;

        public SystemClock SystemClock { get; } = systemClock;

        public DateTimeOffset BuildBuilderStart { get; } = buildBuilderStart;

        public Dictionary<string, object> BuilderMetrics { get; } = builderMetrics;

        public SystemEnvironment SystemEnvironment { get; } = systemEnvironment;

        public ServiceProvider ServiceProvider { get; } = serviceProvider;

        public TestHostControllerInfo TestHostControllerInfo { get; } = testHostControllerInfo;

        public AggregatedConfiguration Configuration { get; set; } = null!;

        public IPlatformActivity? BuilderActivity { get; set; }

        public CTRLPlusCCancellationTokenSource TestApplicationCancellationTokenSource { get; set; } = null!;

        public CommandLineHandler CommandLineHandler { get; set; } = null!;

        public ProxyOutputDevice ProxyOutputDevice { get; set; } = null!;

        public ITestFrameworkCapabilities TestFrameworkCapabilities { get; set; } = null!;

        public StopPoliciesService PoliciesService { get; set; } = null!;

        public ILoggerFactory LoggerFactory { get; set; } = null!;

        public SystemMonitorAsyncFactory SystemMonitorAsyncFactory { get; set; } = null!;

        public bool HasServerFlag { get; set; }

        public bool IsJsonRpcProtocol { get; set; }

        public bool IsHelpCommand { get; set; }

        public bool IsInfoCommand { get; set; }

        public IHost? EarlyHost { get; set; }
    }
}
