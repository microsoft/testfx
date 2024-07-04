// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#else
using System.Collections.Concurrent;
#endif
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.Telemetry;

/// <summary>
/// Allows to log telemetry events via AppInsights.
/// </summary>
internal sealed partial class AppInsightsProvider :
    ITelemetryCollector
#pragma warning disable SA1001 // Commas should be spaced correctly
#if NETCOREAPP
    , IAsyncDisposable
#else
    , IDisposable
#endif
#pragma warning restore SA1001 // Commas should be spaced correctly
{
    // Note: We're currently using the same environment variable as dotnet CLI.
    public static readonly string SessionIdEnvVar = "TESTINGPLATFORM_APPINSIGHTS_SESSIONID";

    // Allows us to correlate events produced from the same process.
    // Not calling this ProcessId, because it has a different meaning.
    private static readonly string CurrentReporterId = Guid.NewGuid().ToString();
    private readonly string _currentSessionId;
    private readonly bool _isCi;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ITelemetryInformation _telemetryInformation;
    private readonly ITelemetryClientFactory _telemetryClientFactory;
    private readonly bool _isDevelopmentRepository;
    private readonly ILogger<AppInsightsProvider> _logger;
    private readonly Task? _telemetryTask;
    private readonly CancellationTokenSource _flushTimeoutOrStop = new();
#if NETCOREAPP
    private readonly Channel<(string EventName, IDictionary<string, object> ParamsMap)> _payloads;
#else
    private readonly BlockingCollection<(string EventName, IDictionary<string, object> ParamsMap)> _payloads;
#endif
#if DEBUG
    // Telemetry properties that are allowed to contain unhashed information.
    private static readonly HashSet<string> StringWhitelist =
    [
        TelemetryProperties.VersionPropertyName,
        TelemetryProperties.ReporterIdPropertyName,
        TelemetryProperties.SessionId,
        TelemetryProperties.HostProperties.TestingPlatformVersionPropertyName,
        TelemetryProperties.HostProperties.FrameworkDescriptionPropertyName,
        TelemetryProperties.HostProperties.OSDescriptionPropertyName,
        TelemetryProperties.HostProperties.RuntimeIdentifierPropertyName,
        TelemetryProperties.HostProperties.ApplicationModePropertyName,
        TelemetryProperties.HostProperties.ExitCodePropertyName,
        TelemetryProperties.HostProperties.ExtensionsPropertyName
    ];
#endif

    private ITelemetryClient? _client;
    private bool _isDisposed;

    public AppInsightsProvider(
        IEnvironment environment,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ITask task,
        ILoggerFactory loggerFactory,
        IClock clock,
        IConfiguration configuration,
        ITelemetryInformation telemetryInformation,
        ITelemetryClientFactory telemetryClientFactory,
        string sessionId)
    {
        _ = bool.TryParse(configuration[PlatformConfigurationConstants.PlatformTelemetryIsDevelopmentRepository], out _isDevelopmentRepository);
        _isCi = CIEnvironmentDetectorForTelemetry.IsCIEnvironment();
        _environment = environment;
        _currentSessionId = sessionId;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _task = task;
        _clock = clock;
        _telemetryInformation = telemetryInformation;
        _telemetryClientFactory = telemetryClientFactory;

#if NETCOREAPP
        _payloads = Channel.CreateUnbounded<(string EventName, IDictionary<string, object> ParamsMap)>(new UnboundedChannelOptions()
        {
            // We process only 1 data at a time
            SingleReader = true,

            // We don't know how many threads will call the Log method
            SingleWriter = false,

            // We want to unlink the caller from the consumer
            AllowSynchronousContinuations = false,
        });

        _telemetryTask = task.Run(IngestLoopAsync, _testApplicationCancellationTokenSource.CancellationToken);
#else
        // Keep the custom thread to avoid to waste one from thread pool.
        // We have some await but we should stay on custom one if not for special needs like trace log or exception.
        _payloads = new();
        _telemetryTask = _task.RunLongRunning(IngestLoopAsync, "Telemetry AppInsightsProvider", _testApplicationCancellationTokenSource.CancellationToken);
#endif

        _logger = loggerFactory.CreateLogger<AppInsightsProvider>();
    }

    // Initialize the telemetry client and start ingesting events.
    private async Task IngestLoopAsync()
    {
        if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _client = _telemetryClientFactory.Create(_currentSessionId, _environment.OsVersion);
        }
        catch (Exception e)
        {
            _client = null;

            await _logger.LogErrorAsync($"Failed to initialize telemetry client", e);
            return;
        }

        DateTimeOffset? lastLoggedError = null;
        _testApplicationCancellationTokenSource.CancellationToken.Register(() => _flushTimeoutOrStop.Cancel());
        try
        {
#if NETCOREAPP
            while (await _payloads.Reader.WaitToReadAsync(_flushTimeoutOrStop.Token))
            {
                (string eventName, IDictionary<string, object> paramsMap) = await _payloads.Reader.ReadAsync();
#else
            foreach ((string eventName, IDictionary<string, object> paramsMap) in _payloads.GetConsumingEnumerable(_flushTimeoutOrStop.Token))
            {
#endif

                // Add common properties.
                paramsMap.Add(TelemetryProperties.VersionPropertyName, _telemetryInformation.Version);
                paramsMap.Add(TelemetryProperties.SessionId, _currentSessionId);
                paramsMap.Add(TelemetryProperties.ReporterIdPropertyName, CurrentReporterId);
                paramsMap.Add(TelemetryProperties.IsCIPropertyName, _isCi.AsTelemetryBool());

                if (_isDevelopmentRepository)
                {
                    paramsMap.Add(TelemetryProperties.HostProperties.IsDevelopmentRepositoryPropertyName, TelemetryProperties.True);
                }

                var metrics = new Dictionary<string, double>();
                var properties = new Dictionary<string, string>();

                foreach (KeyValuePair<string, object> pair in paramsMap)
                {
                    switch (pair.Value)
                    {
                        // Metrics:
                        case double value:
                            metrics.Add(pair.Key, value);
                            break;
                        case DateTimeOffset value:
                            metrics.Add(pair.Key, ToUnixTimeNanoseconds(value));
                            break;

                        // Properties:
#if DEBUG
                        case string value:
                            AssertHashed(pair.Key, value);
                            properties.Add(pair.Key, value);
                            break;
#endif
                        case bool value:
                            RoslynDebug.Assert(false, $"Telemetry entry '{pair.Key}' contains a boolean value, boolean values should always be converted to string using: .{nameof(TelemetryExtensions.AsTelemetryBool)}()");
                            properties.Add(pair.Key, value.AsTelemetryBool());
                            break;
                        default:
                            properties.Add(pair.Key, pair.Value?.ToString() ?? string.Empty);
                            break;
                    }
                }

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    StringBuilder builder = new();
                    builder.AppendLine(CultureInfo.InvariantCulture, $"Send telemetry event: {eventName}");
                    foreach (KeyValuePair<string, string> keyValue in properties)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"    {keyValue.Key}: {keyValue.Value}");
                    }

                    foreach (KeyValuePair<string, double> keyValue in metrics)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"    {keyValue.Key}: {keyValue.Value.ToString("f", CultureInfo.InvariantCulture)}");
                    }

                    await _logger.LogTraceAsync(builder.ToString());
                }

                try
                {
                    _client.TrackEvent(eventName, properties, metrics);
                }
                catch (Exception ex)
                {
                    // If we have a lot of issues with the network we could have a lot of logs here.
                    // We log one error every 3 seconds.
                    // We could do better backpressure.
                    if (_logger.IsEnabled(LogLevel.Error) && (!lastLoggedError.HasValue || (lastLoggedError.Value - _clock.UtcNow).TotalSeconds > 3))
                    {
                        await _logger.LogErrorAsync($"Error during telemetry report.", ex);
                        lastLoggedError = _clock.UtcNow;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when the test application is shutting down or if flush timeout.
            return;
        }
    }

    private static double ToUnixTimeNanoseconds(DateTimeOffset value) =>

        // The magic number is DateTimeOffset.UnixEpoch.Ticks in newer TFMs.
        // We multiply by 100 because Ticks are 100 ns, and we want to report ns.
        (value.UtcTicks - 621355968000000000L) * 100;

#if DEBUG
    private static void AssertHashed(string key, string value)
    {
        if (value is TelemetryProperties.True or TelemetryProperties.False)
        {
            return;
        }

        // Full qualification of Regex to avoid adding conditional 'using' on top of the file.
        if (value.Length == 64 && GetValidHashPattern().IsMatch(value))
        {
            return;
        }

        if (StringWhitelist.Contains(key))
        {
            return;
        }

        RoslynDebug.Assert(false, $"Telemetry entry '{key}' contains an unhashed string value '{value}'. Strings need to be hashed using {nameof(Sha256Hasher)}.{nameof(Sha256Hasher.HashWithNormalizedCasing)}(), or whitelisted.");
    }

#if NET7_0_OR_GREATER
    [System.Text.RegularExpressions.GeneratedRegex("[a-f0-9]{64}")]
    private static partial System.Text.RegularExpressions.Regex GetValidHashPattern();
#else
    private static System.Text.RegularExpressions.Regex GetValidHashPattern()
        => new("[a-f0-9]{64}");
#endif
#endif

    public async Task LogEventAsync(string eventName, IDictionary<string, object> paramsMap)
    {
#if NETCOREAPP
        await _payloads.Writer.WriteAsync((eventName, paramsMap));
#else
        _payloads.Add((eventName, paramsMap));
        await Task.CompletedTask;
#endif
    }

#if !NETCOREAPP
    // Adding dispose on graceful shutdown per https://github.com/microsoft/ApplicationInsights-dotnet/issues/1152#issuecomment-518742922
    public void Dispose()
    {
        _payloads.CompleteAdding();
        if (!_isDisposed)
        {
            if (_telemetryTask is null)
            {
                throw new InvalidOperationException("Unexpected null _telemetryTask");
            }

            int flushForSeconds = 3;
            if (!_telemetryTask.Wait(TimeSpan.FromSeconds(flushForSeconds)))
            {
                _flushTimeoutOrStop.Cancel();
                _logger.LogWarning($"Telemetry task didn't flush after '{flushForSeconds}', some payload could be lost");
            }

            _isDisposed = true;
        }
    }
#endif

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        _payloads.Writer.Complete();
        if (!_isDisposed)
        {
            if (_telemetryTask is null)
            {
                throw new InvalidOperationException("Unexpected null _telemetryTask");
            }

            int flushForSeconds = 3;
            try
            {
                await _telemetryTask.TimeoutAfterAsync(TimeSpan.FromSeconds(flushForSeconds));
            }
            catch (TimeoutException)
            {
                await _flushTimeoutOrStop.CancelAsync();
                await _logger.LogWarningAsync($"Telemetry task didn't flush after '{flushForSeconds}', some payload could be lost");
            }

            _isDisposed = true;
        }
    }
#endif
}
