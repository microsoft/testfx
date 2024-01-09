// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.Testing.Extensions.TestFramework;
using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class ConsoleTestHost(
    ServiceProvider serviceProvider,
    Func<ServiceProvider,
        ITestExecutionRequestFactory,
        ITestFrameworkInvoker,
        ITestExecutionFilterFactory,
        IPlatformOutputDevice,
        IEnumerable<IDataConsumer>,
        TestFrameworkManager,
        TestHostManager,
        MessageBusProxy,
        bool,
        Task<ITestFramework>> buildTestFrameworkAsync,
    TestFrameworkManager testFrameworkManager,
    TestHostManager testHostManager) : CommonTestHost(serviceProvider)
{
    private static readonly ClientInfo Client = new("testingplatform-console", AppVersion.DefaultSemVer);

    private readonly ILogger<ConsoleTestHost> _logger = serviceProvider.GetLoggerFactory().CreateLogger<ConsoleTestHost>();
    private readonly IClock _clock = serviceProvider.GetClock();
    private readonly Func<ServiceProvider,
                      ITestExecutionRequestFactory,
                      ITestFrameworkInvoker,
                      ITestExecutionFilterFactory,
                      IPlatformOutputDevice,
                      IEnumerable<IDataConsumer>,
                      TestFrameworkManager,
                      TestHostManager,
                      MessageBusProxy,
                      bool,
                      Task<ITestFramework>> _buildTestFrameworkAsync = buildTestFrameworkAsync;

    private readonly TestFrameworkManager _testFrameworkManager = testFrameworkManager;
    private readonly TestHostManager _testHostManager = testHostManager;

    protected override bool RunTestApplicationLifecycleCallbacks => true;

    protected override async Task<int> InternalRunAsync()
    {
        var consoleRunStarted = Stopwatch.StartNew();
        DateTimeOffset consoleRunStart = _clock.UtcNow;

        CancellationToken abortRun = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        DateTimeOffset adapterLoadStart = _clock.UtcNow;

        // Use user provided filter factory or create console default one.
        ITestExecutionFilterFactory testExecutionFilterFactory = ServiceProvider.GetService<ITestExecutionFilterFactory>()
            ?? new ConsoleTestExecutionFilterFactory(ServiceProvider.GetCommandLineOptions());

        // Use user provided filter factory or create console default one.
        ITestFrameworkInvoker testAdapterInvoker = ServiceProvider.GetService<ITestFrameworkInvoker>()
            ?? new TestHostTestFrameworkInvoker(ServiceProvider);

        ServiceProvider.TryAddService(new Services.TestSessionContext(abortRun));
        ITestFramework testFrameworkAdapter = await _buildTestFrameworkAsync(
            ServiceProvider,
            new ConsoleTestExecutionRequestFactory(ServiceProvider.GetCommandLineOptions(), testExecutionFilterFactory),
            testAdapterInvoker,
            testExecutionFilterFactory,
            ServiceProvider.GetPlatformOutputDevice(),
            Enumerable.Empty<IDataConsumer>(),
            _testFrameworkManager,
            _testHostManager,
            new MessageBusProxy(),
            ServiceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey));

        ITelemetryCollector telemetry = ServiceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = ServiceProvider.GetTelemetryInformation();
        Statistics? statistics = null;
        string? extensionInformation = null;
        await _logger.LogInformationAsync($"Starting test session '{ServiceProvider.GetTestSessionContext().SessionId}'");
        int exitCode = ExitCodes.GenericFailure;
        DateTimeOffset adapterLoadStop = _clock.UtcNow;
        DateTimeOffset requestExecuteStart = _clock.UtcNow;
        DateTimeOffset? requestExecuteStop = null;
        try
        {
            ITestSessionContext testSessionInfo = ServiceProvider.GetTestSessionContext();

            await ExecuteRequestAsync(
                ServiceProvider.GetPlatformOutputDevice(),
                testSessionInfo,
                ServiceProvider,
                ServiceProvider.GetBaseMessageBus(),
                testFrameworkAdapter,
                Client);
            requestExecuteStop = _clock.UtcNow;

            // Get the exit code service to be able to set the exit code
            ITestApplicationProcessExitCode testApplicationResult = ServiceProvider.GetTestApplicationProcessExitCode();
            statistics = testApplicationResult.GetStatistics();
            exitCode = await testApplicationResult.GetProcessExitCodeAsync();

            await _logger.LogInformationAsync($"Test session '{ServiceProvider.GetTestSessionContext().SessionId}' ended with exit code '{exitCode}' in {consoleRunStarted.Elapsed}");

            // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
            if (telemetryInformation.IsEnabled)
            {
                extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider);
            }
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == abortRun)
        {
            if (requestExecuteStop == null)
            {
                requestExecuteStop = _clock.UtcNow;
            }

            exitCode = ExitCodes.TestSessionAborted;
            await _logger.LogInformationAsync("Test session cancelled.");
        }
        finally
        {
            // Cleanup all services
            await DisposeServiceProviderAsync(ServiceProvider);
        }

        if (telemetryInformation.IsEnabled)
        {
            DateTimeOffset consoleRunStop = _clock.UtcNow;
            var metrics = new Dictionary<string, object>
            {
                { TelemetryProperties.HostProperties.RunStart, consoleRunStart },
                { TelemetryProperties.HostProperties.RunStop, consoleRunStop },
                { TelemetryProperties.RequestProperties.AdapterLoadStart, adapterLoadStart },
                { TelemetryProperties.RequestProperties.AdapterLoadStop, adapterLoadStop },
                { TelemetryProperties.RequestProperties.RequestExecuteStart, requestExecuteStart },
                { TelemetryProperties.RequestProperties.RequestExecuteStop, requestExecuteStop },
                { TelemetryProperties.HostProperties.ExitCodePropertyName, abortRun.IsCancellationRequested ? ExitCodes.TestSessionAborted : exitCode.ToString(CultureInfo.InvariantCulture) },
            };

            if (statistics is not null)
            {
                metrics.Add(TelemetryProperties.RequestProperties.TotalFailedTestsPropertyName, statistics.TotalFailedTests);
                metrics.Add(TelemetryProperties.RequestProperties.TotalRanTestsPropertyName, statistics.TotalRanTests);
            }

            if (extensionInformation is not null)
            {
                metrics.Add(TelemetryProperties.HostProperties.ExtensionsPropertyName, extensionInformation);
            }

            await telemetry.LogEventAsync(TelemetryEvents.ConsoleTestHostExitEventName, metrics);
        }

        return exitCode;
    }
}
