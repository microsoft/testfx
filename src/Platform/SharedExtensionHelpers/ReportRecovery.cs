// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal static class ReportControllerMode
{
    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsSupported { get; } =
        !OperatingSystem.IsAndroid()
        && !OperatingSystem.IsBrowser()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWasi();
}

internal sealed class ReportJournalConfiguration(string environmentVariableName)
{
    private string? _path;

    public string EnvironmentVariableName { get; } = environmentVariableName;

    public string GetOrCreatePath(IConfiguration configuration, IFileSystem fileSystem)
    {
        if (_path is null)
        {
            string directory = fileSystem.CreateDirectory(configuration.GetTestResultDirectory());
            _path = Path.Combine(directory, $"report-recovery-{Guid.NewGuid():N}.jsonl");
        }

        return _path;
    }
}

internal sealed class ReportJournalEnvironmentVariableProvider(
    ICommandLineOptions commandLineOptions,
    IConfiguration configuration,
    IFileSystem fileSystem,
    string optionName,
    ReportJournalConfiguration journal) : ITestHostEnvironmentVariableProvider
{
    public string Uid => $"{nameof(ReportJournalEnvironmentVariableProvider)}.{journal.EnvironmentVariableName}";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => Uid;

    public string Description => Uid;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(commandLineOptions.IsOptionSet(optionName) && ReportControllerMode.IsSupported);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        string path = journal.GetOrCreatePath(configuration, fileSystem);
        using (fileSystem.NewFileStream(path, FileMode.Create))
        {
        }

        environmentVariables.SetVariable(new EnvironmentVariable(
            journal.EnvironmentVariableName,
            path,
            isSecret: false,
            isLocked: true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => environmentVariables.TryGetVariable(journal.EnvironmentVariableName, out OwnedEnvironmentVariable? variable)
            && variable.Value == journal.GetOrCreatePath(configuration, fileSystem)
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask($"The report recovery environment variable '{journal.EnvironmentVariableName}' is missing or invalid.");
}

internal sealed class ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult> :
    ITestHostProcessLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer,
    IDisposable
    where TGenerator : ReportGeneratorBase<TGenerator, TCapturedTestResult>
    where TCapturedTestResult : class
{
    internal const long MaxJournalBytes = 256L * 1024 * 1024;
    internal const int MaxJournalRecordBytes = 4 * 1024 * 1024;
    internal const int MaxJournalRecordChars = 4 * 1024 * 1024;
    internal const int MaxJournalRecordCount = 500_000;

    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IFileSystem _fileSystem;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly IClock _clock;
    private readonly IEnvironment _environment;
    private readonly ILogger _logger;
    private readonly string _optionName;
    private readonly ReportJournalConfiguration _journal;
    private readonly Func<IServiceProvider, RecoveredReportMetadata, TGenerator> _generatorFactory;
    private readonly Func<string, ReportJournalRecord<TCapturedTestResult>?> _journalDeserializer;

    public ReportProcessLifetimeHandler(
        IServiceProvider serviceProvider,
        string optionName,
        ReportJournalConfiguration journal,
        Func<IServiceProvider, RecoveredReportMetadata, TGenerator> generatorFactory,
        Func<string, ReportJournalRecord<TCapturedTestResult>?> journalDeserializer)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = serviceProvider.GetCommandLineOptions();
        _configuration = serviceProvider.GetConfiguration();
        _fileSystem = serviceProvider.GetRequiredService<IFileSystem>();
        _messageBus = serviceProvider.GetMessageBus();
        _outputDevice = serviceProvider.GetOutputDevice();
        _clock = serviceProvider.GetSystemClock();
        _environment = serviceProvider.GetEnvironment();
        _logger = serviceProvider.GetLoggerFactory().CreateLogger(typeof(ReportProcessLifetimeHandler<,>).FullName!);
        _optionName = optionName;
        _journal = journal;
        _generatorFactory = generatorFactory;
        _journalDeserializer = journalDeserializer;
    }

    public string Uid => $"{nameof(ReportProcessLifetimeHandler<,>)}.{_journal.EnvironmentVariableName}";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => Uid;

    public string Description => Uid;

    public Type[] DataTypesProduced { get; } = [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(_optionName) && ReportControllerMode.IsSupported);

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        string path = _journal.GetOrCreatePath(_configuration, _fileSystem);
        if (!_fileSystem.ExistFile(path))
        {
            return;
        }

        try
        {
            if (testHostProcessInformation.HasExitedGracefully)
            {
                return;
            }

            ReportJournalReadResult<TCapturedTestResult> journal = ReadJournal(path, testHostProcessInformation);
            TGenerator generator = _generatorFactory(_serviceProvider, journal.Metadata);
            bool republishCompletedReport = journal.CompletedReportFileName is not null
                && _fileSystem.ExistFile(journal.CompletedReportFileName);
            string fileName;
            string? warning = null;
            if (republishCompletedReport)
            {
                fileName = journal.CompletedReportFileName!;
            }
            else
            {
                (fileName, warning) = await generator.GenerateRecoveredReportAsync(
                    journal,
                    testHostProcessInformation.ExitCode,
                    cancellationToken).ConfigureAwait(false);
            }

            if (warning is not null)
            {
                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
            }

            await _outputDevice.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData(republishCompletedReport
                    ? $"The test host terminated before report artifact delivery was confirmed. Re-published the completed report '{fileName}'."
                    : journal.Completed
                    ? $"The test host terminated before report artifact delivery was confirmed, but the completed report file was unavailable. Re-generated it from {journal.Results.Count} journaled terminal test result(s) and marked it incomplete."
                    : $"The test host terminated before report generation completed. Recovered {journal.Results.Count} terminal test result(s); the generated report is marked incomplete."),
                cancellationToken).ConfigureAwait(false);
            if (journal.IsPartial && !republishCompletedReport)
            {
                await _outputDevice.DisplayAsync(
                    this,
                    new WarningMessageOutputDeviceData("Report recovery stopped before the end of the journal because it was truncated, corrupt, or exceeded a safety limit. The recovered report contains only the valid bounded prefix."),
                    cancellationToken).ConfigureAwait(false);
            }

            await _messageBus.PublishAsync(
                this,
                new FileArtifact(
                    new FileInfo(fileName),
                    generator.RecoveredArtifactDisplayName,
                    generator.RecoveredArtifactDescription,
                    generator.RecoveredArtifactKind)).ConfigureAwait(false);
            await WriteRetryArtifactManifestAsync(
                fileName,
                generator.RecoveredArtifactKind,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.LogErrorAsync($"Failed to recover report data from '{path}'.", ex).ConfigureAwait(false);
            await _outputDevice.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData($"The test host terminated before report generation completed, but the partial report could not be recovered: {ex.Message}"),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteJournal(path);
        }
    }

    private async Task WriteRetryArtifactManifestAsync(
        string artifactPath,
        string? artifactKind,
        CancellationToken cancellationToken)
    {
        const string retryArtifactManifestEnvironmentVariable = "TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST";
        string? manifestPath = _environment.GetEnvironmentVariable(retryArtifactManifestEnvironmentVariable);
        if (manifestPath is null || manifestPath.Length == 0)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(manifestPath);
            if (directory is not null && directory.Length > 0)
            {
                _fileSystem.CreateDirectory(directory);
            }

            string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(artifactPath));
            string encodedKind = artifactKind is null
                ? "-"
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(artifactKind));
            using IFileStream stream = _fileSystem.NewFileStream(
                manifestPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream.Stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
#if NETCOREAPP
            await writer.WriteLineAsync($"{encodedPath}\t{encodedKind}".AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"{encodedPath}\t{encodedKind}").ConfigureAwait(false);
#endif
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.LogWarningAsync(
                $"Failed to register recovered report '{artifactPath}' with retry artifact consolidation: {ex.Message}").ConfigureAwait(false);
        }
    }

    private void TryDeleteJournal(string path)
    {
        try
        {
            if (_fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to delete report recovery journal '{path}': {ex.Message}");
        }
    }

    public void Dispose()
        => TryDeleteJournal(_journal.GetOrCreatePath(_configuration, _fileSystem));

    private ReportJournalReadResult<TCapturedTestResult> ReadJournal(
        string path,
        ITestHostProcessInformation processInformation)
    {
        ReportJournalRecord<TCapturedTestResult>? header = null;
        List<TCapturedTestResult> results = [];
        List<ReportJournalParentEntry> parents = [];
        bool completed = false;
        string? completedReportFileName = null;
        int droppedJournalRecords = 0;
        bool isPartial = false;
        int recordCount = 0;

        using IFileStream stream = _fileSystem.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var reader = new BoundedUtf8LineReader(stream.Stream, MaxJournalBytes, MaxJournalRecordBytes, MaxJournalRecordChars);
        while (recordCount < MaxJournalRecordCount)
        {
            BoundedLineReadResult readResult = reader.ReadLine(out string? line);
            if (readResult == BoundedLineReadResult.End)
            {
                break;
            }

            if (readResult == BoundedLineReadResult.LimitExceeded)
            {
                isPartial = true;
                _logger.LogWarning($"Stopped reading report recovery journal '{path}' because it exceeded a configured size limit.");
                break;
            }

            recordCount++;
            try
            {
                ReportJournalRecord<TCapturedTestResult>? record = _journalDeserializer(line!);
                if (record is null)
                {
                    _logger.LogDebug("Stopped reading the report recovery journal at an invalid record.");
                    isPartial = true;
                    break;
                }

                switch (record.Type)
                {
                    case ReportJournalRecordType.Header:
                        header = record;
                        break;
                    case ReportJournalRecordType.Test:
                        if (record.Result is not null)
                        {
                            results.Add(record.Result);
                        }

                        if (record.Parent is not null)
                        {
                            parents.Add(record.Parent);
                        }

                        break;
                    case ReportJournalRecordType.Completion:
                        completed = true;
                        completedReportFileName = record.ReportFileName;
                        droppedJournalRecords = record.DroppedJournalRecords;
                        break;
                }

                if (completed)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Stopped reading the report recovery journal at an incomplete record: {ex.Message}");
                isPartial = true;
                break;
            }
        }

        if (!completed && recordCount >= MaxJournalRecordCount)
        {
            isPartial = true;
            _logger.LogWarning($"Stopped reading report recovery journal '{path}' after the maximum of {MaxJournalRecordCount} records.");
        }

        var metadata = new RecoveredReportMetadata
        {
            StartTime = header?.StartTime ?? _clock.UtcNow,
            ProcessId = processInformation.PID,
            FrameworkUid = header?.FrameworkUid ?? "unknown",
            FrameworkVersion = header?.FrameworkVersion ?? string.Empty,
            FrameworkDisplayName = header?.FrameworkDisplayName ?? "unknown",
            IsIncomplete = true,
        };
        return new ReportJournalReadResult<TCapturedTestResult>(
            metadata,
            results,
            parents,
            completed,
            completedReportFileName,
            droppedJournalRecords,
            isPartial || droppedJournalRecords > 0);
    }
}

internal enum BoundedLineReadResult
{
    Line,
    End,
    LimitExceeded,
}

internal sealed class BoundedUtf8LineReader
{
    private const int BufferSize = 8192;

    private readonly Stream _stream;
    private readonly long _maxBytes;
    private readonly int _maxLineBytes;
    private readonly int _maxLineChars;
    private readonly byte[] _readBuffer = new byte[BufferSize];
    private readonly byte[] _lineBuffer;
    private int _readOffset;
    private int _readCount;
    private long _bytesRead;

    public BoundedUtf8LineReader(Stream stream, long maxBytes, int maxLineBytes, int maxLineChars)
    {
        _stream = stream;
        _maxBytes = maxBytes;
        _maxLineBytes = maxLineBytes;
        _maxLineChars = maxLineChars;
        _lineBuffer = new byte[maxLineBytes];
    }

    public BoundedLineReadResult ReadLine(out string? line)
    {
        int lineLength = 0;
        while (TryReadByte(out byte value))
        {
            if (++_bytesRead > _maxBytes)
            {
                line = null;
                return BoundedLineReadResult.LimitExceeded;
            }

            if (value == (byte)'\n')
            {
                return DecodeLine(lineLength, out line);
            }

            if (lineLength >= _maxLineBytes)
            {
                line = null;
                return BoundedLineReadResult.LimitExceeded;
            }

            _lineBuffer[lineLength++] = value;
        }

        if (lineLength == 0)
        {
            line = null;
            return BoundedLineReadResult.End;
        }

        return DecodeLine(lineLength, out line);
    }

    private BoundedLineReadResult DecodeLine(int lineLength, out string? line)
    {
        if (lineLength > 0 && _lineBuffer[lineLength - 1] == (byte)'\r')
        {
            lineLength--;
        }

        line = Encoding.UTF8.GetString(_lineBuffer, 0, lineLength);
        if (line.Length > _maxLineChars)
        {
            line = null;
            return BoundedLineReadResult.LimitExceeded;
        }

        return BoundedLineReadResult.Line;
    }

    private bool TryReadByte(out byte value)
    {
        if (_readOffset >= _readCount)
        {
            _readCount = _stream.Read(_readBuffer, 0, _readBuffer.Length);
            _readOffset = 0;
            if (_readCount == 0)
            {
                value = default;
                return false;
            }
        }

        value = _readBuffer[_readOffset++];
        return true;
    }
}

internal enum ReportJournalRecordType
{
    Header,
    Test,
    Completion,
}

internal sealed class ReportJournalRecord<TCapturedTestResult>
    where TCapturedTestResult : class
{
    public ReportJournalRecordType Type { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public int? ProcessId { get; set; }

    public string? FrameworkUid { get; set; }

    public string? FrameworkVersion { get; set; }

    public string? FrameworkDisplayName { get; set; }

    public TCapturedTestResult? Result { get; set; }

    public ReportJournalParentEntry? Parent { get; set; }

    public string? ReportFileName { get; set; }

    public int DroppedJournalRecords { get; set; }

    public static ReportJournalRecord<TCapturedTestResult> CreateHeader(
        DateTimeOffset startTime,
        int processId,
        ITestFramework testFramework)
        => new()
        {
            Type = ReportJournalRecordType.Header,
            StartTime = startTime,
            ProcessId = processId,
            FrameworkUid = testFramework.Uid,
            FrameworkVersion = testFramework.Version,
            FrameworkDisplayName = testFramework.DisplayName,
        };

    public static ReportJournalRecord<TCapturedTestResult> CreateTest(
        TCapturedTestResult? result,
        ReportJournalParentEntry? parent)
        => new()
        {
            Type = ReportJournalRecordType.Test,
            Result = result,
            Parent = parent,
        };

    public static ReportJournalRecord<TCapturedTestResult> CreateCompletion(
        string reportFileName,
        int droppedJournalRecords)
        => new()
        {
            Type = ReportJournalRecordType.Completion,
            ReportFileName = reportFileName,
            DroppedJournalRecords = droppedJournalRecords,
        };
}

internal sealed class ReportJournalParentEntry
{
    public string Uid { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ParentUid { get; set; }
}

internal sealed record ReportJournalReadResult<TCapturedTestResult>(
    RecoveredReportMetadata Metadata,
    IReadOnlyList<TCapturedTestResult> Results,
    IReadOnlyList<ReportJournalParentEntry> Parents,
    bool Completed,
    string? CompletedReportFileName,
    int DroppedJournalRecords,
    bool IsPartial)
    where TCapturedTestResult : class;

internal sealed class RecoveredReportMetadata
{
    public DateTimeOffset StartTime { get; set; }

    public int ProcessId { get; set; }

    public string FrameworkUid { get; set; } = string.Empty;

    public string FrameworkVersion { get; set; } = string.Empty;

    public string FrameworkDisplayName { get; set; } = string.Empty;

    public bool IsIncomplete { get; set; }
}

internal sealed class RecoveredTestFramework(RecoveredReportMetadata metadata) : ITestFramework
{
    public string Uid => metadata.FrameworkUid;

    public string Version => metadata.FrameworkVersion;

    public string DisplayName => metadata.FrameworkDisplayName;

    public string Description => DisplayName;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => throw new NotSupportedException();

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => throw new NotSupportedException();

    public Task ExecuteRequestAsync(ExecuteRequestContext context) => throw new NotSupportedException();
}

#pragma warning restore RS0051
