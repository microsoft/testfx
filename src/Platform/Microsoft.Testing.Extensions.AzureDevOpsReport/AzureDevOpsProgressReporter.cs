// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsProgressReporter : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string AzureDevOpsTfBuildVariableName = "TF_BUILD";
    internal const int MinimumEmissionIntervalMs = 250;

    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly HashSet<string> _terminalUids = new HashSet<string>(StringComparer.Ordinal);
#pragma warning restore IDE0028
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
    private readonly Stopwatch _emissionThrottle = new();
    private readonly Guid _recordId = Guid.NewGuid();
    private readonly bool _isEnabled;

    private bool _emitAzureDevOpsCommands;
    private int _completed;
    private int _seen;
    private int _failed;
    private int _lastEmittedPercent;
    private bool _hasEmittedInitial;

    public AzureDevOpsProgressReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory)
    {
        _environment = environment;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _logger = loggerFactory.CreateLogger<AzureDevOpsProgressReporter>();
        _isEnabled = commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsProgress);
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(AzureDevOpsProgressReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            _emitAzureDevOpsCommands = false;
            lock (_stateLock)
            {
                _terminalUids.Clear();
                _completed = 0;
                _seen = 0;
                _failed = 0;
                _lastEmittedPercent = -1;
                _hasEmittedInitial = false;
                _emissionThrottle.Reset();
            }

            if (!_isEnabled)
            {
                return;
            }

            _emitAzureDevOpsCommands = string.Equals(_environment.GetEnvironmentVariable(AzureDevOpsTfBuildVariableName), "true", StringComparison.OrdinalIgnoreCase);
            if (!_emitAzureDevOpsCommands)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(AzureDevOpsResources.ProgressRequiresTfBuildWarning);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(AzureDevOpsResources.ProgressRequiresTfBuildWarning), testSessionContext.CancellationToken).ConfigureAwait(false);
                return;
            }

            string name = $"{_testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown"} ({_targetFrameworkMoniker.Value})";
            string line = $"##vso[task.logdetail id={_recordId.ToString("D", CultureInfo.InvariantCulture)};name={AzDoEscaper.Escape(name)};type=Build;order=1;state=InProgress;progress=0]Starting tests";
            await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), testSessionContext.CancellationToken).ConfigureAwait(false);

            lock (_stateLock)
            {
                _hasEmittedInitial = true;
                _lastEmittedPercent = 0;
                _emissionThrottle.Start();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_emitAzureDevOpsCommands || value is not TestNodeUpdateMessage update)
            {
                return;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            ProgressEvent evt = Classify(state);
            if (evt == ProgressEvent.None)
            {
                return;
            }

            string uid = update.TestNode.Uid;
            int percent;
            bool shouldEmit;
            lock (_stateLock)
            {
                if (evt == ProgressEvent.InProgress)
                {
                    _seen++;
                }
                else
                {
                    if (!_terminalUids.Add(uid))
                    {
                        return;
                    }

                    _completed++;
                    if (evt == ProgressEvent.Failed)
                    {
                        _failed++;
                    }
                }

                int denominator = Math.Max(_seen, _completed);
                // Cap in-progress emissions at 99 so 100% is reserved for the final
                // `state=Completed` emission in OnTestSessionFinishingAsync. Otherwise
                // hitting 100 here would prevent any further updates (updates are monotonic).
                percent = denominator <= 0
                    ? 0
                    : Math.Min(99, Math.Max(0, (int)(_completed * 100L / denominator)));

                if (percent <= _lastEmittedPercent)
                {
                    return;
                }

                long elapsed = _emissionThrottle.ElapsedMilliseconds;
                if (_hasEmittedInitial && elapsed < MinimumEmissionIntervalMs)
                {
                    return;
                }

                _lastEmittedPercent = percent;
                shouldEmit = true;
                _emissionThrottle.Restart();
            }

            if (shouldEmit)
            {
                string line = $"##vso[task.logdetail id={_recordId.ToString("D", CultureInfo.InvariantCulture)};progress={percent.ToString(CultureInfo.InvariantCulture)};state=InProgress]";
                await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(ConsumeAsync), ex);
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_emitAzureDevOpsCommands || !_hasEmittedInitial)
            {
                return;
            }

            string result;
            lock (_stateLock)
            {
                result = _failed > 0 ? "Failed" : "Succeeded";
            }

            string line = $"##vso[task.logdetail id={_recordId.ToString("D", CultureInfo.InvariantCulture)};progress=100;state=Completed;result={result}]";
            await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private static ProgressEvent Classify(TestNodeStateProperty? state)
        => state switch
        {
            InProgressTestNodeStateProperty => ProgressEvent.InProgress,
            PassedTestNodeStateProperty => ProgressEvent.Passed,
            SkippedTestNodeStateProperty => ProgressEvent.Passed,
            FailedTestNodeStateProperty => ProgressEvent.Failed,
            ErrorTestNodeStateProperty => ProgressEvent.Failed,
            TimeoutTestNodeStateProperty => ProgressEvent.Failed,
#pragma warning disable CS0618, MTP0001
            CancelledTestNodeStateProperty => ProgressEvent.Failed,
#pragma warning restore CS0618, MTP0001
            _ => ProgressEvent.None,
        };

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }

    private enum ProgressEvent
    {
        None,
        InProgress,
        Passed,
        Failed,
    }
}
