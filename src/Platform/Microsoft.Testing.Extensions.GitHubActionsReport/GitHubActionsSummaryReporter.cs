// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Writes a markdown roll-up of the test run (totals, failures, slowest tests) to the file pointed to by
/// the <c>GITHUB_STEP_SUMMARY</c> environment variable. GitHub renders that file on the workflow run's
/// summary page. See
/// <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary"/>.
/// </summary>
internal sealed class GitHubActionsSummaryReporter :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer
{
    private const string StepSummaryEnvironmentVariable = "GITHUB_STEP_SUMMARY";
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";
    private const int MaxFailures = 20;
    private const int MaxSlowestTests = 10;

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly Dictionary<string, TestRecord> _records = new Dictionary<string, TestRecord>(StringComparer.Ordinal);
#pragma warning restore IDE0028

    public GitHubActionsSummaryReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory)
    {
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _logger = loggerFactory.CreateLogger<GitHubActionsSummaryReporter>();
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker);
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(GitHubActionsSummaryReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        lock (_stateLock)
        {
            _records.Clear();
        }

        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            TerminalKind kind = GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            TimingProperty? timing = null;
            string? fqnValue = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                switch (enumerator.Current)
                {
                    case TimingProperty t:
                        timing = t;
                        break;
                    case SerializableKeyValuePairStringProperty kv when kv.Key == FullyQualifiedNamePropertyKey && fqnValue is null:
                        fqnValue = kv.Value;
                        break;
                }
            }

            string fullyQualifiedName = fqnValue ?? displayName;
            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration);
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

        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled)
            {
                return;
            }

            string? path = _environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
            if (RoslynString.IsNullOrWhiteSpace(path))
            {
                // Outside a GitHub Actions step (or when summaries are unsupported) there is nowhere to
                // write. Stay quiet apart from a low-noise trace so local/dev runs don't get a warning.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"'{StepSummaryEnvironmentVariable}' is not set; skipping job summary.");
                }

                return;
            }

            List<TestRecord> snapshot;
            lock (_stateLock)
            {
                snapshot = [.. _records.Values];
            }

            string markdown = BuildMarkdown(snapshot, _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown", _targetFrameworkMoniker.Value);

            try
            {
                using IFileStream stream = _fileSystem.NewFileStream(path!, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false));
                await writer.WriteAsync(markdown).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string warning = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, path, ex.Message);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(warning);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
            }
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

    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failures = new List<TestRecord>();

        foreach (TestRecord record in records)
        {
            totalDuration += record.Duration;
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    if (failures.Count < MaxFailures)
                    {
                        failures.Add(record);
                    }

                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        string statusIcon = failed > 0 ? "❌" : "✅";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Test Run Summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(passed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(failed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(skipped.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(totalDuration)).Append(" |\n\n");

        if (failures.Count > 0)
        {
            builder.Append("### ❌ Failures (").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
            foreach (TestRecord failure in failures)
            {
                builder.Append("- `").Append(EscapeInlineCode(failure.FullyQualifiedName)).Append("`\n");
            }

            builder.Append('\n');
        }

        IEnumerable<TestRecord> slowest = records
            .Where(static r => r.Duration > TimeSpan.Zero)
            .OrderByDescending(static r => r.Duration)
            .Take(MaxSlowestTests);

        bool slowestEmitted = false;
        foreach (TestRecord record in slowest)
        {
            if (!slowestEmitted)
            {
                builder.Append("### ⏱ Slowest tests\n\n");
                slowestEmitted = true;
            }

            builder.Append("- `").Append(EscapeInlineCode(record.FullyQualifiedName)).Append("` — ").Append(FormatDuration(record.Duration)).Append('\n');
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}ms", (int)duration.TotalMilliseconds);
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00}s", duration.TotalSeconds);
        }

        if (duration < TimeSpan.FromHours(1))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}m {1:00}s", (int)duration.TotalMinutes, duration.Seconds);
        }

        long totalHours = (long)Math.Floor(duration.TotalHours);
        return string.Format(CultureInfo.InvariantCulture, "{0}h {1:00}m {2:00}s", totalHours, duration.Minutes, duration.Seconds);
    }

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static TerminalKind GetTerminalKind(TestNodeStateProperty? state)
        => state switch
        {
            PassedTestNodeStateProperty => TerminalKind.Passed,
            FailedTestNodeStateProperty => TerminalKind.Failed,
            ErrorTestNodeStateProperty => TerminalKind.Failed,
            TimeoutTestNodeStateProperty => TerminalKind.Failed,
            SkippedTestNodeStateProperty => TerminalKind.Skipped,
#pragma warning disable CS0618, MTP0001
            CancelledTestNodeStateProperty => TerminalKind.Failed,
#pragma warning restore CS0618, MTP0001
            _ => TerminalKind.NotTerminal,
        };

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }

    internal readonly struct TestRecord
    {
        public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration)
        {
            DisplayName = displayName;
            FullyQualifiedName = fullyQualifiedName;
            Kind = kind;
            Duration = duration;
        }

        public string DisplayName { get; }

        public string FullyQualifiedName { get; }

        public TerminalKind Kind { get; }

        public TimeSpan Duration { get; }
    }

    internal enum TerminalKind
    {
        NotTerminal,
        Passed,
        Failed,
        Skipped,
    }
}
