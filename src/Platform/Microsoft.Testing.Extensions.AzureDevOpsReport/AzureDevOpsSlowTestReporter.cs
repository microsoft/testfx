// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Surfaces tests that are still running past a per-test threshold as durable scrollback lines, lowering the
/// threshold for tests with a known-short historical runtime and decorating each emission with the historical
/// p95/p99 fetched from Azure DevOps test history.
/// </summary>
internal sealed class AzureDevOpsSlowTestReporter : SlowTestReporterBase
{
    private const string AzureDevOpsTfBuildVariableName = "TF_BUILD";

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IAzureDevOpsHistoryService _historyService;
    private readonly bool _isEnabled;
    private readonly TimeSpan _staticThreshold;

    private double _multiplier;
    private volatile int _minimumSampleCount;

    public AzureDevOpsSlowTestReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAzureDevOpsHistoryService historyService)
        : base(outputDevice, task, clock, loggerFactory.CreateLogger<AzureDevOpsSlowTestReporter>())
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _historyService = historyService;
        _staticThreshold = TimeSpan.FromSeconds(AzureDevOpsCommandLineOptions.SlowTestStaticThresholdSeconds);
        _isEnabled = commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            && commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory);
    }

    public override string Uid => nameof(AzureDevOpsSlowTestReporter);

    public override string DisplayName => AzureDevOpsResources.DisplayName;

    public override string Description => AzureDevOpsResources.Description;

    protected override bool IsEnabled => _isEnabled;

    protected override bool OnActivating()
    {
        if (!string.Equals(_environment.GetEnvironmentVariable(AzureDevOpsTfBuildVariableName), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Outside Azure DevOps the feature truly no-ops: we only leave a low-noise trace and never
            // surface an output-device line, so local/dev runs that happen to pass the option stay quiet.
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace(AzureDevOpsResources.SlowTestHistoryRequiresTfBuildWarning);
            }

            return false;
        }

        // 'double' cannot be marked 'volatile', so publish the multiplier through Volatile.Write; the
        // remaining fields use the 'volatile' modifier. The base class writes _active = true last, which
        // acts as the release fence that publishes all three to the test-data-producer threads in ConsumeAsync.
        Volatile.Write(ref _multiplier, GetMultiplier());
        _minimumSampleCount = GetMinimumSampleCount();

        return true;
    }

    protected override string GetTestName(TestNode testNode) => TestNodeIdentity.GetTestName(testNode);

    protected override string GetDisplayLabel(TestNode testNode) => TestNodeIdentity.GetDisplayLabel(testNode);

    protected override TimeSpan ResolveThreshold(string testName)
    {
        bool hasStats = _historyService.TryGetDurationStats(testName, out DurationHistoryStats stats);
        return AzureDevOpsSlowTestThresholds.ComputeThreshold(_staticThreshold, stats, hasStats, Volatile.Read(ref _multiplier), _minimumSampleCount);
    }

    protected override Task EmitSlowTestAsync(string testName, string displayLabel, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        string elapsedText = AzureDevOpsSlowTestThresholds.FormatDuration(elapsed.TotalMilliseconds);
        string line = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.SlowTestStillRunning, elapsedText, displayLabel);

        // History is aggregated by the stable fully-qualified name (testName), not the per-instance
        // display label, so the decoration lookup keeps using testName.
        if (_historyService.TryGetDurationStats(testName, out DurationHistoryStats stats)
            && AzureDevOpsSlowTestThresholds.HasUsableHistory(stats, hasStats: true, _minimumSampleCount))
        {
            string decoration = string.Format(
                CultureInfo.InvariantCulture,
                AzureDevOpsResources.SlowTestHistoryDecoration,
                AzureDevOpsSlowTestThresholds.FormatDuration(stats.P95Milliseconds),
                AzureDevOpsSlowTestThresholds.FormatDuration(stats.P99Milliseconds),
                stats.SampleCount.ToString(CultureInfo.InvariantCulture));
            line = $"{line}  {decoration}";
        }

        return OutputDevice.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), cancellationToken);
    }

    private double GetMultiplier()
        => _commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier, out string[]? arguments)
            && arguments is [string value]
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier)
            && multiplier > 0
                ? multiplier
                : AzureDevOpsCommandLineOptions.SlowTestHistoryDefaultMultiplier;

    private int GetMinimumSampleCount()
        => _commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample, out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minimum)
            && minimum >= 1
                ? minimum
                : AzureDevOpsCommandLineOptions.SlowTestHistoryDefaultMinSample;
}
