// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsSummaryReporter : IDataConsumer, IDataProducer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string DefaultSummaryFileNameFormat = "azdo-summary-{0}-{1}-{2}.md";
    private const string FullyQualifiedNamePropertyKey = "vstest.TestCase.FullyQualifiedName";
    private const int MaxSlowestTests = 10;
    private const int MaxTopFailingClasses = 5;
    private const int MaxFirstFailingFqns = 10;

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly Func<bool> _shouldDeferToArtifactPostProcessing;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly Dictionary<string, TestRecord> _records = new Dictionary<string, TestRecord>(StringComparer.Ordinal);
#pragma warning restore IDE0028
    private readonly bool _isEnabled;

    private bool _emitAzureDevOpsCommands;

    public AzureDevOpsSummaryReporter(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ILoggerFactory loggerFactory,
        Func<bool> shouldDeferToArtifactPostProcessing)
    {
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _messageBus = messageBus;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _logger = loggerFactory.CreateLogger<AzureDevOpsSummaryReporter>();
        _isEnabled = commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsSummary);
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _shouldDeferToArtifactPostProcessing = shouldDeferToArtifactPostProcessing;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    public string Uid => nameof(AzureDevOpsSummaryReporter);

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
                _records.Clear();
            }

            if (!_isEnabled)
            {
                return;
            }

            _emitAzureDevOpsCommands = AzureDevOpsConstants.IsRunningInAzureDevOps(_environment);
            if (_emitAzureDevOpsCommands)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(AzureDevOpsResources.SummaryRequiresTfBuildWarning);
            }

            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(AzureDevOpsResources.SummaryRequiresTfBuildWarning), testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
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
            TerminalKind kind = SummaryReporterHelpers.GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            // Single-pass collection of TimingProperty and the FQN SerializableKeyValuePairStringProperty:
            // replaces 1 × SingleOrDefault<TimingProperty>() + 1 × OfType<>().FirstOrDefault() with one
            // GetStructEnumerator() walk, saving 1 linked-list traversal and 1 LINQ allocation per terminal result.
            // Singleton-typed properties use the local GetSingleOrDefaultValue helper to preserve the
            // throw-on-duplicate invariant that SingleOrDefault<T>() provided; the FQN key keeps the
            // prior FirstOrDefault semantics (first match wins) so we don't silently overwrite earlier values.
            TimingProperty? timing = null;
            string? fqnValue = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                switch (enumerator.Current)
                {
                    case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                    case SerializableKeyValuePairStringProperty kv when kv.Key == FullyQualifiedNamePropertyKey && fqnValue is null:
                        fqnValue = kv.Value;
                        break;
                }
            }

            static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
                where TProperty : class, IProperty
                => existingProperty is not null
                    ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                    : property;

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
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_emitAzureDevOpsCommands)
            {
                return;
            }

            List<TestRecord> snapshot;
            lock (_stateLock)
            {
                snapshot = [.. _records.Values];
            }

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown";
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext);
                string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                    resultsDirectory,
                    AzureDevOpsSummaryArtifactPostProcessor.Provider,
                    AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                    module).ConfigureAwait(false);
                await _messageBus.PublishAsync(
                    this,
                    new SessionFileArtifact(
                        testSessionContext.SessionUid,
                        new FileInfo(fragmentPath),
                        AzureDevOpsResources.DisplayName,
                        AzureDevOpsResources.Description,
                        AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind)).ConfigureAwait(false);
                return;
            }

            string markdown = BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value);
            string? path = ResolveSummaryPath();
            if (path is null)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Could not resolve Azure DevOps summary path.");
                }

                return;
            }

            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!RoslynString.IsNullOrEmpty(directory) && !_fileSystem.ExistDirectory(directory))
                {
                    _fileSystem.CreateDirectory(directory!);
                }

                using IFileStream stream = _fileSystem.NewFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false));
                await writer.WriteAsync(markdown).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.SummaryWriteFailedWarning, path, ex.Message);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(warning);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
                return;
            }

            string line = $"##vso[task.uploadsummary]{AzDoEscaper.Escape(path)}";
            await _outputDevice.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private CiRunSummaryModule CreateModule(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        ITestSessionContext testSessionContext)
        => CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode(),
            ResolveExplicitSummaryPath(_commandLineOptions));

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;

    private string? ResolveSummaryPath()
    {
        if (_commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSummary, out string[]? arguments)
            && arguments is [string explicitPath]
            && !RoslynString.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        string? configuredTestResultsDirectory = _configuration.GetTestResultDirectory();
        if (RoslynString.IsNullOrWhiteSpace(configuredTestResultsDirectory))
        {
            return null;
        }

        // Include the assembly name and process architecture in the default file name (matching the
        // <asm>_<tfm>_<arch> shape used by the TRX/HTML/JUnit reports) so that multiple test assemblies
        // that share the same target framework and TestResults directory (a common CI setup) don't all
        // resolve to the same path and race to write it, which surfaced as
        // "The process cannot access the file ... because it is being used by another process".
        string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown";
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        // Sanitize the whole file name (matching TRX/HTML/JUnit) so that unexpected characters in any segment
        // - including the target framework moniker or architecture, not just the assembly name - cannot produce
        // an invalid file name.
        string fileName = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(string.Format(
            CultureInfo.InvariantCulture,
            DefaultSummaryFileNameFormat,
            assemblyName,
            _targetFrameworkMoniker.Value,
            architecture));
        return Path.GetFullPath(Path.Combine(configuredTestResultsDirectory!, fileName));
    }

    internal static string? ResolveExplicitSummaryPath(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSummary, out string[]? arguments)
            && arguments is [string explicitPath]
            && !RoslynString.IsNullOrWhiteSpace(explicitPath)
                ? Path.GetFullPath(explicitPath)
                : null;

    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failingByClass = new Dictionary<string, int>(StringComparer.Ordinal);
        var failingFqns = new List<string>();

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
                    if (failingFqns.Count < MaxFirstFailingFqns)
                    {
                        failingFqns.Add(record.FullyQualifiedName);
                    }

                    string className = GetClassName(record.FullyQualifiedName);
                    failingByClass[className] = failingByClass.TryGetValue(className, out int count) ? count + 1 : 1;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        var builder = new StringBuilder();
        builder.Append("# Test summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(total.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Total duration | ").Append(FormatDuration(totalDuration)).Append(" |\n");
        builder.Append('\n');

        if (failingByClass.Count > 0)
        {
            builder.Append("## Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (KeyValuePair<string, int> pair in failingByClass.OrderByDescending(static p => p.Value).ThenBy(static p => p.Key, StringComparer.Ordinal).Take(MaxTopFailingClasses))
            {
                builder.Append("| ").Append(EscapeCell(pair.Key)).Append(" | ").Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        if (failingFqns.Count > 0)
        {
            builder.Append("## First failing tests\n\n");
            foreach (string fqn in failingFqns)
            {
                builder.Append("- ").Append(EscapeCell(fqn)).Append('\n');
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
                builder.Append("## Slowest tests\n\n");
                builder.Append("| Test | Duration |\n");
                builder.Append("| --- | ---: |\n");
                slowestEmitted = true;
            }

            builder.Append("| ").Append(EscapeCell(record.DisplayName)).Append(" | ").Append(FormatDuration(record.Duration)).Append(" |\n");
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    internal static string BuildAggregateMarkdown(CiRunSummaryAggregate aggregate)
    {
        var builder = new StringBuilder();
        builder.Append("# Overall test summary\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(aggregate.TotalTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(aggregate.PassedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(aggregate.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(aggregate.SkippedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Duration | ")
            .Append(aggregate.Duration is { } duration ? FormatDuration(duration) : "Unavailable")
            .Append(" |\n");
        if (aggregate.ExitCode is int exitCode)
        {
            builder.Append("| Exit code | ").Append(exitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        }

        builder.Append('\n');
        if (aggregate.IsPartial)
        {
            builder.Append("> **Partial summary:** the test run was truncated.\n\n");
        }
        else if (!aggregate.HasAuthoritativeRunSummary)
        {
            builder.Append("> Counts reflect the observed module fragments. The outer `dotnet test` duration and exit verdict were not supplied by the SDK.\n\n");
        }

        foreach (CiRunSummaryModule module in aggregate.Modules)
        {
            bool needsDiscriminator = HasDuplicateModuleIdentity(aggregate.Modules, module);
            builder.Append("<details>\n<summary>")
                .Append(HtmlEncode(module.AssemblyName))
                .Append(" (").Append(HtmlEncode(module.TargetFramework)).Append(", ")
                .Append(HtmlEncode(module.Architecture));
            if (needsDiscriminator)
            {
                builder.Append(", attempt ").Append(module.AttemptNumber.ToString(CultureInfo.InvariantCulture))
                    .Append(", session ").Append(HtmlEncode(module.SessionUid));
            }

            builder.Append(")</summary>\n\n");
            AppendModuleMarkdown(builder, module);
            builder.Append("</details>\n\n");
        }

        return builder.ToString();
    }

    private static void AppendModuleMarkdown(StringBuilder builder, CiRunSummaryModule module)
    {
        builder.Append("## ").Append(EscapeCell(module.AssemblyName)).Append("\n\n");
        builder.Append("| Metric | Value |\n");
        builder.Append("| --- | ---: |\n");
        builder.Append("| Total | ").Append(module.TotalTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Passed | ").Append(module.PassedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Failed | ").Append(module.FailedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Skipped | ").Append(module.SkippedTests.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
        builder.Append("| Test duration | ").Append(FormatDuration(TimeSpan.FromTicks(module.TestDurationTicks))).Append(" |\n");
        builder.Append("| Module exit code | ").Append(module.ExitCode.ToString(CultureInfo.InvariantCulture)).Append(" |\n\n");

        if (module.TopFailingClasses.Length > 0)
        {
            builder.Append("### Top failing classes\n\n");
            builder.Append("| Class | Failures |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryFailingClass item in module.TopFailingClasses)
            {
                builder.Append("| ").Append(EscapeCell(item.ClassName)).Append(" | ")
                    .Append(item.FailureCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
            }

            builder.Append('\n');
        }

        if (module.Failures.Length > 0)
        {
            builder.Append("### First failing tests\n\n");
            foreach (CiRunSummaryTest failure in module.Failures.Take(MaxFirstFailingFqns))
            {
                builder.Append("- ").Append(EscapeCell(failure.FullyQualifiedName)).Append('\n');
            }

            builder.Append('\n');
        }

        if (module.SlowestTests.Length > 0)
        {
            builder.Append("### Slowest tests\n\n");
            builder.Append("| Test | Duration |\n");
            builder.Append("| --- | ---: |\n");
            foreach (CiRunSummaryTest test in module.SlowestTests)
            {
                builder.Append("| ").Append(EscapeCell(test.DisplayName)).Append(" | ")
                    .Append(FormatDuration(TimeSpan.FromTicks(test.DurationTicks))).Append(" |\n");
            }

            builder.Append('\n');
        }
    }

    private static string GetClassName(string fullyQualifiedName)
    {
        if (RoslynString.IsNullOrEmpty(fullyQualifiedName))
        {
            return "(unknown)";
        }

        int lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot <= 0 ? "(unknown)" : fullyQualifiedName.Substring(0, lastDot);
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0:D2}:{1:D2}", "{0}:{1:D2}:{2:D2}");

    private static string EscapeCell(string value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '|':
                    sb.Append("\\|");
                    break;
                case '`':
                    sb.Append("\\`");
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append("<br>");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private static bool HasDuplicateModuleIdentity(IReadOnlyList<CiRunSummaryModule> modules, CiRunSummaryModule module)
        => modules.Count(candidate =>
            string.Equals(candidate.ModulePath, module.ModulePath, StringComparison.Ordinal)
            && string.Equals(candidate.TargetFramework, module.TargetFramework, StringComparison.Ordinal)
            && string.Equals(candidate.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase)) > 1;
}
