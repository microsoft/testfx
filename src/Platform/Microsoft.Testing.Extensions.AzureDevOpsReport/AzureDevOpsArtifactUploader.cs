// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsArtifactUploader : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string AzureDevOpsArtifactUploadCommandFormat = "##vso[artifact.upload containerfolder={0};artifactname={0}]{1}";
    private const string AzureDevOpsBuildAddTagCommandPrefix = "##vso[build.addbuildtag]";
    private const string AzureDevOpsTfBuildVariableName = "TF_BUILD";
    private const string CrashDumpProducerUid = "CrashDumpProcessLifetimeHandler";
    private const string CrashDumpTag = "has-crashdump";
    private const string HangDumpProducerUid = "HangDumpProcessLifetimeHandler";
    private const string HangDumpTag = "has-hangdump";
    private const string TestFailuresTag = "has-test-failures";
    private static readonly string[] DefaultIncludePatterns = ["**/*"];

    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly AzureDevOpsArtifactUploadMode _uploadMode;
    private readonly string[] _includePatterns;
    private readonly string[] _excludePatterns;
    private readonly string? _artifactNameOverride;
    private readonly Lazy<string> _targetFrameworkMoniker;

    private bool _emitAzureDevOpsCommands;
    private int _hasCrashDump;
    private int _hasHangDump;
    private int _hasTestFailures;
    private string? _testResultsDirectory;

    public AzureDevOpsArtifactUploader(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _logger = loggerFactory.CreateLogger<AzureDevOpsArtifactUploader>();
        _uploadMode = GetUploadMode(commandLineOptions);
        _includePatterns = GetPatterns(commandLineOptions, AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, DefaultIncludePatterns);
        _excludePatterns = GetPatterns(commandLineOptions, AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, []);
        _artifactNameOverride = commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName, out string[]? artifactNameArguments)
            && artifactNameArguments is [string artifactName]
                ? artifactName
                : null;
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage), typeof(FileArtifact)];

    public string Uid => nameof(AzureDevOpsArtifactUploader);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_uploadMode is not AzureDevOpsArtifactUploadMode.Off);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            string? configuredTestResultsDirectory = _configuration.GetTestResultDirectory();
            _testResultsDirectory = RoslynString.IsNullOrWhiteSpace(configuredTestResultsDirectory)
                ? null
                : Path.GetFullPath(configuredTestResultsDirectory);
            _emitAzureDevOpsCommands = false;
            Volatile.Write(ref _hasCrashDump, 0);
            Volatile.Write(ref _hasHangDump, 0);
            Volatile.Write(ref _hasTestFailures, 0);

            if (_uploadMode is AzureDevOpsArtifactUploadMode.Off)
            {
                return;
            }

            _emitAzureDevOpsCommands = string.Equals(_environment.GetEnvironmentVariable(AzureDevOpsTfBuildVariableName), "true", StringComparison.OrdinalIgnoreCase);
            if (_emitAzureDevOpsCommands)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(AzureDevOpsResources.ArtifactUploadRequiresTfBuildWarning);
            }

            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(AzureDevOpsResources.ArtifactUploadRequiresTfBuildWarning), testSessionContext.CancellationToken).ConfigureAwait(false);
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

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (value)
            {
                case TestNodeUpdateMessage nodeUpdateMessage when IsFailureState(nodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>()):
                    Interlocked.Exchange(ref _hasTestFailures, 1);
                    break;

                case FileArtifact:
                    TrackDump(dataProducer.Uid);
                    break;
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

            if (!_emitAzureDevOpsCommands)
            {
                return;
            }

            if (_uploadMode is AzureDevOpsArtifactUploadMode.TagsOnly or AzureDevOpsArtifactUploadMode.All)
            {
                if (Volatile.Read(ref _hasCrashDump) == 1)
                {
                    await EmitBuildTagAsync(CrashDumpTag, testSessionContext.CancellationToken).ConfigureAwait(false);
                }

                if (Volatile.Read(ref _hasHangDump) == 1)
                {
                    await EmitBuildTagAsync(HangDumpTag, testSessionContext.CancellationToken).ConfigureAwait(false);
                }

                if (Volatile.Read(ref _hasTestFailures) == 1)
                {
                    await EmitBuildTagAsync(TestFailuresTag, testSessionContext.CancellationToken).ConfigureAwait(false);
                }
            }

            if (_uploadMode is AzureDevOpsArtifactUploadMode.Files or AzureDevOpsArtifactUploadMode.All)
            {
                await EmitArtifactUploadCommandsAsync(testSessionContext.CancellationToken).ConfigureAwait(false);
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

    private async Task EmitArtifactUploadCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_testResultsDirectory is null || !_fileSystem.ExistDirectory(_testResultsDirectory))
            {
                return;
            }

            string[] files = _fileSystem.GetFiles(_testResultsDirectory, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                return;
            }

            Matcher? matcher = ShouldUploadAllFiles() ? null : BuildMatcher();
            string artifactName = AzDoEscaper.Escape(GetArtifactName());
            string testResultsDirectoryWithSeparator = EnsureTrailingDirectorySeparator(_testResultsDirectory);

            foreach (string filePath in files.OrderBy(path => path, PathComparison.Comparer))
            {
                string? relativePath = TryGetRelativePath(filePath, testResultsDirectoryWithSeparator);
                if (relativePath is null)
                {
                    continue;
                }

                if (matcher is not null && !MatcherExtensions.Match(matcher, NormalizePath(relativePath)).HasMatches)
                {
                    continue;
                }

                await EmitArtifactUploadCommandAsync(artifactName, filePath, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(EmitArtifactUploadCommandsAsync), ex);
        }
    }

    private async Task EmitArtifactUploadCommandAsync(string artifactName, string filePath, CancellationToken cancellationToken)
        => await EmitLineAsync(string.Format(CultureInfo.InvariantCulture, AzureDevOpsArtifactUploadCommandFormat, artifactName, AzDoEscaper.Escape(filePath)), cancellationToken).ConfigureAwait(false);

    private async Task EmitBuildTagAsync(string tag, CancellationToken cancellationToken)
        => await EmitLineAsync($"{AzureDevOpsBuildAddTagCommandPrefix}{tag}", cancellationToken).ConfigureAwait(false);

    private async Task EmitLineAsync(string line, CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);

    private string GetArtifactName()
        => _artifactNameOverride is { } artifactName && !RoslynString.IsNullOrWhiteSpace(artifactName)
            ? artifactName
            : $"TestResults_{_testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown"}_{_targetFrameworkMoniker.Value}";

    private Matcher BuildMatcher()
    {
        var matcher = new Matcher(PathComparison.Comparison);
        foreach (string includePattern in _includePatterns)
        {
            matcher.AddInclude(includePattern);
        }

        foreach (string excludePattern in _excludePatterns)
        {
            matcher.AddExclude(excludePattern);
        }

        return matcher;
    }

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }

    private bool ShouldUploadAllFiles()
        => _includePatterns.Length == 1
            && _includePatterns[0] == DefaultIncludePatterns[0]
            && _excludePatterns.Length == 0;

    private void TrackDump(string dataProducerUid)
    {
        switch (dataProducerUid)
        {
            case CrashDumpProducerUid:
                Volatile.Write(ref _hasCrashDump, 1);
                break;

            case HangDumpProducerUid:
                Volatile.Write(ref _hasHangDump, 1);
                break;
        }
    }

    private static AzureDevOpsArtifactUploadMode GetUploadMode(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts, out string[]? arguments)
            && arguments is [string argument]
                ? ParseUploadMode(argument)
                : AzureDevOpsArtifactUploadMode.Off;

    private static string[] GetPatterns(ICommandLineOptions commandLineOptions, string optionName, string[] defaultPatterns)
        => commandLineOptions.TryGetOptionArgumentList(optionName, out string[]? patterns)
            && patterns is { Length: > 0 }
                ? [.. patterns.Select(NormalizePath)]
                : defaultPatterns;

    private static AzureDevOpsArtifactUploadMode ParseUploadMode(string mode)
        => mode.ToLowerInvariant() switch
        {
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly => AzureDevOpsArtifactUploadMode.TagsOnly,
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles => AzureDevOpsArtifactUploadMode.Files,
            AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll => AzureDevOpsArtifactUploadMode.All,
            _ => AzureDevOpsArtifactUploadMode.Off,
        };

    private static bool IsFailureState(TestNodeStateProperty? state)
    {
        if (state is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty)
        {
            return true;
        }

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
        return state is CancelledTestNodeStateProperty;
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
    }

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static string? TryGetRelativePath(string filePath, string testResultsDirectoryWithSeparator)
        => filePath.StartsWith(testResultsDirectoryWithSeparator, PathComparison.Comparison)
            ? filePath.Substring(testResultsDirectoryWithSeparator.Length)
            : null;
}

internal enum AzureDevOpsArtifactUploadMode
{
    Off,
    TagsOnly,
    Files,
    All,
}
