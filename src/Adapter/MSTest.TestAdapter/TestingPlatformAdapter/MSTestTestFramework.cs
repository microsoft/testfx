// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
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
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using CreateTestSessionContext = Microsoft.Testing.Platform.Extensions.TestFramework.CreateTestSessionContext;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A native Microsoft.Testing.Platform (MTP) test framework for MSTest. It does not derive from the VSTest bridge
/// and it no longer routes through the VSTest <c>MSTestDiscoverer</c> / <c>MSTestExecutor</c> classes: it handles the
/// MTP discovery/run requests directly, builds the run context, filter and runsettings natively
/// (see <see cref="MSTestRunContext"/> / <see cref="MSTestDiscoveryContext"/> / <see cref="MSTestRunSettings"/>),
/// publishes test nodes through the native <see cref="MtpUnitTestElementSink"/> / <see cref="MtpTestResultRecorder"/>,
/// and drives discovery/execution through the platform-agnostic <see cref="MSTestEngine"/>.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
[StackTraceHidden]
internal sealed class MSTestTestFramework : ITestFramework, IDataProducer, IDisposable
{
    private readonly MSTestExtension _extension;
    private readonly Func<IEnumerable<Assembly>> _getTestAssemblies;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITrxReportCapability? _trxReportCapability;
    private readonly PlatformServicesConfigurationAdapter _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MSTestGracefulStopTestExecutionCapability _gracefulStopCapability;
    private readonly string _resultFilesStagingDirectory;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private int _nextResultFileStagingDirectory;
    private int _resultFilesStagingDirectoryCreated;
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
        _gracefulStopCapability = (MSTestGracefulStopTestExecutionCapability)capabilities.GetCapability<IGracefulStopTestExecutionCapability>()!;
        _resultFilesStagingDirectory = Path.Combine(
            serviceProvider.GetConfiguration().GetTestResultDirectory(),
            $".mstest-{Guid.NewGuid():N}"[..16]);
        ((Microsoft.Testing.Platform.Services.ServiceProvider)serviceProvider)
            .AddService(new ResultFilesStagingDirectoryCleanup(CleanupResultFilesStagingDirectory));
        PlatformServiceProvider.Instance.AdapterTraceLogger = new MTPTraceLogger(_loggerFactory.CreateLogger("mstest-trace"));
        _gracefulStopCapability.NotifyTestExecutionPending();

        // Let the engine emit fixture/test-method spans that nest under the platform's test-case spans. This is a
        // no-op unless the OpenTelemetry extension is registered.
        MSTestPlatformActivity.TryEnable(serviceProvider);
    }

    internal ITestClassInstanceFactory? TestClassInstanceFactory { get; set; }

    internal MSTestDiscoveryCache? DiscoveryCache { get; set; }

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
                    await RunTestsAsync(runRequest, context).ConfigureAwait(false);
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

        // Call the platform-agnostic engine directly with neutral inputs; the native MTP path no longer routes
        // through the VSTest MSTestDiscoverer class. The MTP-specific filter provider evaluates the filter from the
        // neutral UnitTestElement model so this path never materializes a vstest TestCase (see #9769).
        var engine = new MSTestEngine(cancellationToken, CreateTelemetrySender())
        {
            TestClassInstanceFactory = TestClassInstanceFactory,
        };
        await engine.DiscoverAsync(
                assemblyPaths,
                runSettings.SettingsXml,
                handle.ToAdapterMessageLogger(),
                elementSink,
                new MtpTestElementFilterProvider(discoveryContext),
                _configuration,
                new TestSourceHandler(),
                isMTP: true)
            .ConfigureAwait(false);
    }

    private async Task RunTestsAsync(RunTestExecutionRequest request, ExecuteRequestContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1" && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        SessionUid sessionUid = _sessionUid!.Value;
        Assembly[] assemblies = [.. _getTestAssemblies()];
        string[] assemblyPaths = [.. assemblies.Select(GetAssemblyPath)];
        var handle = new MSTestFrameworkHandle(_serviceProvider.GetOutputDevice(), _extension, cancellationToken);
        MSTestRunSettings runSettings = CreateRunSettings(handle);
        var runContext = new MSTestRunContext(_serviceProvider.GetCommandLineOptions(), runSettings, request.Filter);

        // Call the platform-agnostic engine directly with neutral inputs; the native MTP path no longer routes
        // through the VSTest MSTestExecutor class. Results are published natively via MtpTestResultRecorder and the
        // MTP-specific filter provider evaluates the filter from the neutral UnitTestElement model so this path
        // never materializes a vstest TestCase (see #9769).
        _gracefulStopCapability.NotifyTestExecutionStarting();
        try
        {
            var executionManager = new TestExecutionManager();
            string? enableCache = Environment.GetEnvironmentVariable(MSTestDiscoveryCache.EnableEnvironmentVariable);
            string? disableCache = Environment.GetEnvironmentVariable(MSTestDiscoveryCache.DisableEnvironmentVariable);
            if (enableCache is "1" or "true"
                && disableCache is not ("1" or "true")
                && DiscoveryCache is { } cache
                && TestClassInstanceFactory is null
                // Match MTP's JSON-RPC protocol resolution, not its one-shot dotnet-test pipe.
                && _serviceProvider.GetCommandLineOptions().TryGetOptionArgumentList("server", out string[]? protocolName)
                && (protocolName is null || protocolName.Length == 0
                    || protocolName[0].Equals("jsonrpc", StringComparison.OrdinalIgnoreCase))
                && request.Filter is TestNodeUidListFilter uidFilter
                && uidFilter.TestNodeUids.All(uid => Guid.TryParse(uid.Value, out _)))
            {
                cache.SetSources(assemblyPaths);
                executionManager.UnitTestDiscovererFactory = sourceHandler
                    => cache.CreateDiscoverer(sourceHandler, assemblies, uidFilter.TestNodeUids, cancellationToken);
            }

            var engine = new MSTestEngine(cancellationToken, CreateTelemetrySender(), executionManager)
            {
                TestClassInstanceFactory = TestClassInstanceFactory,
            };
            await engine.RunFromSourcesAsync(
                    assemblyPaths,
                    runSettings.SettingsXml,
                    runContext.TestRunDirectory,
                    handle.ToAdapterMessageLogger(),
                    settings => new MtpTestResultRecorder(
                        context,
                        this,
                        sessionUid,
                        IsTrxEnabled,
                        settings,
                        StageResultFiles),
                    new MtpTestElementFilterProvider(runContext),
                    _configuration,
                    new TestSourceHandler(),
                    isMTP: true)
                .ConfigureAwait(false);
        }
        finally
        {
            _gracefulStopCapability.NotifyTestExecutionCompleted();
        }
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

    private void StageResultFiles(TestResult result)
    {
        if (result.ResultFiles is not { Count: > 0 } resultFiles)
        {
            return;
        }

        var durableResultFiles = new List<string>(resultFiles.Count);
        foreach (string resultFile in resultFiles)
        {
            string sourcePath = PlatformServiceProvider.Instance.FileOperations.GetFullFilePath(resultFile);
            try
            {
                int directoryId = Interlocked.Increment(ref _nextResultFileStagingDirectory);
                string stagingDirectory = Path.Combine(_resultFilesStagingDirectory, directoryId.ToString(CultureInfo.InvariantCulture));
                string stagedPath = Path.Combine(stagingDirectory, GetFileName(sourcePath));

                Directory.CreateDirectory(GetPathForFileSystemAccess(stagingDirectory));
                Volatile.Write(ref _resultFilesStagingDirectoryCreated, 1);
                File.Copy(
                    GetPathForFileSystemAccess(sourcePath),
                    GetPathForFileSystemAccess(stagedPath));

                durableResultFiles.Add(stagedPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Preserve the existing reporter behavior: leave the original path in place so each
                // consumer can surface its normal attachment-copy warning or failure.
                durableResultFiles.Add(resultFile);
            }
        }

        result.ResultFiles = durableResultFiles;
    }

    private static string GetPathForFileSystemAccess(string path)
    {
#if NETCOREAPP
        return path;
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return path;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (PathTooLongException) when (Path.IsPathRooted(path) && !ContainsRelativePathSegments(path))
        {
            fullPath = path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }

        return fullPath.Length < 260
            ? path
            : fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath.Substring(2)
                : @"\\?\" + fullPath;
#endif
    }

    private static string GetFileName(string path)
    {
        int separatorIndex = path.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return separatorIndex < 0 ? path : path.Substring(separatorIndex + 1);
    }

#if !NETCOREAPP
    private static bool ContainsRelativePathSegments(string path)
    {
        foreach (string segment in path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                return true;
            }
        }

        return false;
    }
#endif

    private void CleanupResultFilesStagingDirectory()
    {
        if (Volatile.Read(ref _resultFilesStagingDirectoryCreated) == 0)
        {
            return;
        }

        try
        {
            Directory.Delete(GetPathForFileSystemAccess(_resultFilesStagingDirectory), recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The desired cleanup state has already been reached.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                "Failed to delete result-file staging directory '{0}': {1}",
                _resultFilesStagingDirectory,
                ex);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _gracefulStopCapability.NotifyTestExecutionCompleted();
            CleanupResultFilesStagingDirectory();
            _incomingRequestCounter.Dispose();
            _isDisposed = true;
        }
    }

    private sealed class ResultFilesStagingDirectoryCleanup(Action cleanup) : IDisposable
    {
        private Action? _cleanup = cleanup;

        public void Dispose()
            => Interlocked.Exchange(ref _cleanup, null)?.Invoke();
    }
}
