// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using CreateTestSessionContext = Microsoft.Testing.Platform.Extensions.TestFramework.CreateTestSessionContext;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A native Microsoft.Testing.Platform (MTP) test framework for MSTest. Unlike <see cref="MSTestBridgedTestFramework"/>
/// it does not derive from the VSTest bridge: it handles the MTP discovery/run requests directly, builds the run
/// context, filter and runsettings natively (see <see cref="MSTestRunContext"/> / <see cref="MSTestDiscoveryContext"/>
/// / <see cref="MSTestRunSettings"/>), and publishes test nodes through the native <see cref="MtpUnitTestElementSink"/>
/// / <see cref="MtpTestResultRecorder"/>. It reuses MSTest's existing <see cref="MSTestDiscoverer"/> /
/// <see cref="MSTestExecutor"/> engine.
/// </summary>
/// <remarks>
/// This is opt-in (behind <c>MSTEST_EXPERIMENTAL_NATIVE_MTP</c>) while the native path reaches full parity with the
/// bridge. It still relies on the bridge only for the shared command-line options (<c>--filter</c>, <c>--settings</c>,
/// <c>--test-parameter</c>) and the runsettings configuration/environment-variable providers; retiring those and the
/// bridge package reference is the final step of the migration.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
[StackTraceHidden]
internal sealed class MSTestTestFramework : ITestFramework, IDataProducer, IDisposable
{
    private readonly MSTestExtension _extension;
    private readonly Func<IEnumerable<Assembly>> _getTestAssemblies;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITrxReportCapability? _trxReportCapability;
    private readonly BridgedConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private bool? _isTrxEnabled;
    private bool _isDisposed;
    private SessionUid? _sessionUid;

    public MSTestTestFramework(MSTestExtension extension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
    {
        _extension = extension;
        _getTestAssemblies = getTestAssemblies;
        _serviceProvider = serviceProvider;
        _trxReportCapability = capabilities.GetCapability<ITrxReportCapability>();
        _configuration = new(serviceProvider.GetConfiguration());
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        PlatformServiceProvider.Instance.AdapterTraceLogger = new MTPTraceLogger(_loggerFactory.CreateLogger("mstest-trace"));
    }

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public Type[] DataTypesProduced { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
    ];

    private bool IsTrxEnabled
        => _isTrxEnabled ??= _trxReportCapability is IMSTestTrxReportCapability internalCapability
            ? internalCapability.IsTrxEnabled
            : _trxReportCapability is { IsSupported: true };

    public Task<bool> IsEnabledAsync() => _extension.IsEnabledAsync();

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (_sessionUid is not null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, PlatformAdapterResources.VSTestBridgedTestFrameworkSessionAlreadyCreatedErrorMessage, _sessionUid.Value.Value));
        }

        _sessionUid = context.SessionUid;
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    public async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        _incomingRequestCounter.Signal();
        await _incomingRequestCounter.WaitAsync(context.CancellationToken).ConfigureAwait(false);
        _sessionUid = null;
        return new CloseTestSessionResult { IsSuccess = true };
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        _incomingRequestCounter.AddCount();
        try
        {
            switch (context.Request)
            {
                case DiscoverTestExecutionRequest discoverRequest:
                    await DiscoverTestsAsync(discoverRequest, context.MessageBus, context.CancellationToken).ConfigureAwait(false);
                    break;

                case RunTestExecutionRequest runRequest:
                    await RunTestsAsync(runRequest, context.MessageBus, context.CancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException($"The native MSTest framework does not support requests of type '{context.Request.GetType()}'.");
            }
        }
        finally
        {
            _incomingRequestCounter.Signal();
            context.Complete();
        }
    }

    private async Task DiscoverTestsAsync(DiscoverTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_DISCOVERTESTS") == "1" && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        SessionUid sessionUid = _sessionUid!.Value;
        string[] assemblyPaths = GetAssemblyPaths();
        var handle = new MSTestFrameworkHandle(_serviceProvider.GetOutputDevice(), _extension, cancellationToken);
        MSTestRunSettings runSettings = CreateRunSettings(handle);
        var discoveryContext = new MSTestDiscoveryContext(_serviceProvider.GetCommandLineOptions(), runSettings, request.Filter);
        var elementSink = new MtpUnitTestElementSink(messageBus, this, sessionUid, IsTrxEnabled);

        await new MSTestDiscoverer(new TestSourceHandler(), CreateTelemetrySender())
            .DiscoverTestsAsync(assemblyPaths, discoveryContext, handle, elementSink, _configuration, isMTP: true)
            .ConfigureAwait(false);
    }

    private async Task RunTestsAsync(RunTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1" && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        SessionUid sessionUid = _sessionUid!.Value;
        string[] assemblyPaths = GetAssemblyPaths();
        var handle = new MSTestFrameworkHandle(_serviceProvider.GetOutputDevice(), _extension, cancellationToken);
        MSTestRunSettings runSettings = CreateRunSettings(handle);
        var runContext = new MSTestRunContext(_serviceProvider.GetCommandLineOptions(), runSettings, request.Filter);

        await new MSTestExecutor(cancellationToken, CreateTelemetrySender())
            .RunTestsAsync(
                assemblyPaths,
                runContext,
                handle,
                settings => new MtpTestResultRecorder(messageBus, this, sessionUid, IsTrxEnabled, settings),
                _configuration,
                isMTP: true)
            .ConfigureAwait(false);
    }

    private MSTestRunSettings CreateRunSettings(MSTestFrameworkHandle handle)
        => new(
            _serviceProvider.GetCommandLineOptions(),
            _serviceProvider.GetFileSystem(),
            _serviceProvider.GetConfiguration(),
            _serviceProvider.GetClientInfo(),
            handle);

    private string[] GetAssemblyPaths()
        => [.. _getTestAssemblies().Select(GetAssemblyPath)];

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Empty Assembly.Location is handled explicitly by falling back to the assembly simple name.")]
    private static string GetAssemblyPath(Assembly assembly)
    {
        string location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            return location;
        }

        string name = assembly.GetName().Name
            ?? throw new InvalidOperationException($"Cannot determine the name of assembly '{assembly}'.");

        return name + ".dll";
    }

    private Func<string, IDictionary<string, object>, Task>? CreateTelemetrySender()
    {
        ITelemetryInformation telemetryInformation = _serviceProvider.GetTelemetryInformation();
        if (!telemetryInformation.IsEnabled)
        {
            return null;
        }

        ITelemetryCollector telemetryCollector = _serviceProvider.GetTelemetryCollector();
        return (eventName, metrics) => telemetryCollector.LogEventAsync(eventName, metrics, CancellationToken.None);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _incomingRequestCounter.Dispose();
            _isDisposed = true;
        }
    }
}
#endif
