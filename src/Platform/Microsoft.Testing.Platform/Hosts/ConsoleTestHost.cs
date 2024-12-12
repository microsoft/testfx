// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
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
    Func<TestFrameworkBuilderData, Task<ITestFramework>> buildTestFrameworkAsync,
    TestFrameworkManager testFrameworkManager,
    TestHostManager testHostManager)
    : CommonTestHost(serviceProvider)
{
    private static readonly ClientInfo ClientInfoHost = new("testingplatform-console", AppVersion.DefaultSemVer);
    private static readonly IClientInfo ClientInfoService = new ClientInfoService("testingplatform-console", AppVersion.DefaultSemVer);

    private readonly ILogger<ConsoleTestHost> _logger = serviceProvider.GetLoggerFactory().CreateLogger<ConsoleTestHost>();
    private readonly IClock _clock = serviceProvider.GetClock();

    protected override bool RunTestApplicationLifeCycleCallbacks => true;

    protected override async Task<int> InternalRunAsync()
    {
        var consoleRunStarted = Stopwatch.StartNew();
        DateTimeOffset consoleRunStart = _clock.UtcNow;

        CancellationToken abortRun = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        DateTimeOffset adapterLoadStart = _clock.UtcNow;

        // Add the ClientInfo service to the service provider
        ServiceProvider.TryAddService(ClientInfoService);

        // Use user provided filter factory or create console default one.
        ITestFrameworkInvoker testAdapterInvoker = ServiceProvider.GetService<ITestFrameworkInvoker>()
            ?? new TestHostTestFrameworkInvoker(ServiceProvider);

        ITestExecutionFilter filter = await testHostManager.BuildFilterAsync(ServiceProvider, []);

        ServiceProvider.TryAddService(new Services.TestSessionContext(abortRun));
        ITestFramework testFramework = await buildTestFrameworkAsync(new TestFrameworkBuilderData(
            ServiceProvider,
            new ConsoleTestExecutionRequestFactory(ServiceProvider.GetCommandLineOptions(), filter),
            testAdapterInvoker,
            ServiceProvider.GetPlatformOutputDevice(),
            [],
            testFrameworkManager,
            testHostManager,
            new MessageBusProxy(),
            ServiceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey),
            false));

        ITelemetryCollector telemetry = ServiceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = ServiceProvider.GetTelemetryInformation();
        Statistics? statistics = null;
        string? extensionInformation = null;
        await _logger.LogInformationAsync($"Starting test session '{ServiceProvider.GetTestSessionContext().SessionId}'");
        int exitCode;
        DateTimeOffset adapterLoadStop = _clock.UtcNow;
        DateTimeOffset requestExecuteStart = _clock.UtcNow;
        DateTimeOffset? requestExecuteStop = null;
        try
        {
            ITestSessionContext testSessionInfo = ServiceProvider.GetTestSessionContext();

            await ExecuteRequestAsync(
                (ProxyOutputDevice)ServiceProvider.GetOutputDevice(),
                testSessionInfo,
                ServiceProvider,
                ServiceProvider.GetBaseMessageBus(),
                testFramework,
                ClientInfoHost);
            requestExecuteStop = _clock.UtcNow;

            // Get the exit code service to be able to set the exit code
            ITestApplicationProcessExitCode testApplicationResult = ServiceProvider.GetTestApplicationProcessExitCode();
            statistics = testApplicationResult.GetStatistics();
            exitCode = testApplicationResult.GetProcessExitCode();

            await _logger.LogInformationAsync($"Test session '{ServiceProvider.GetTestSessionContext().SessionId}' ended with exit code '{exitCode}' in {consoleRunStarted.Elapsed}");

            // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
            if (telemetryInformation.IsEnabled)
            {
                extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider);
            }
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == abortRun)
        {
            requestExecuteStop ??= _clock.UtcNow;

            exitCode = ExitCodes.TestSessionAborted;
            await _logger.LogInformationAsync("Test session canceled.");
        }
        finally
        {
            // Cleanup all services
            await DisposeServiceProviderAsync(ServiceProvider);
        }

        if (telemetryInformation.IsEnabled)
        {
            DateTimeOffset consoleRunStop = _clock.UtcNow;
            Dictionary<string, object> metrics = new()
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
