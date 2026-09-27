// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal sealed partial class ReportProcessLifetimeHandler<TGenerator, TCapturedTestResult> :
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
}

#pragma warning restore RS0051
