// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class CrashDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IDataProducer, IOutputDeviceDataProducer
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IOutputDevice _outputDisplay;
    private readonly CrashDumpConfiguration _netCoreCrashDumpGeneratorConfiguration;
    private readonly CrashDumpArtifactPublisher _artifactPublisher;
    private readonly CrashDumpSequenceFileHandler _sequenceFileHandler;

    private int _ifSupportedIgnoredMessageEmitted;

    public CrashDumpProcessLifetimeHandler(
        ICommandLineOptions commandLineOptions,
        IMessageBus messageBus,
        IOutputDevice outputDisplay,
        CrashDumpConfiguration netCoreCrashDumpGeneratorConfiguration)
    {
        _commandLineOptions = commandLineOptions;
        _outputDisplay = outputDisplay;
        _netCoreCrashDumpGeneratorConfiguration = netCoreCrashDumpGeneratorConfiguration;
        _artifactPublisher = new(this, commandLineOptions, messageBus, outputDisplay, netCoreCrashDumpGeneratorConfiguration);
        _sequenceFileHandler = new(this, messageBus, outputDisplay, netCoreCrashDumpGeneratorConfiguration);
    }

    /// <inheritdoc />
    public string Uid => nameof(CrashDumpProcessLifetimeHandler);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => CrashDumpResources.CrashDumpDisplayName;

    /// <inheritdoc />
    public string Description => CrashDumpResources.CrashDumpDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(
            (_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName) ||
             _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName) ||
             _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName)) &&
            _netCoreCrashDumpGeneratorConfiguration.Enable);

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        // If the user opted into the best-effort '--crash-report-if-supported' variant on a
        // runtime/platform where crash reports are unsupported emit a single informational line
        // so the user knows the option was accepted but silently no-opped.
        if (!_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName)
            || _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName))
        {
            return;
        }

#if NETCOREAPP
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string message = CrashDumpResources.CrashReportIfSupportedIgnoredOnWindowsInfoMessage;
#else
        string message = CrashDumpResources.CrashReportIfSupportedIgnoredOnNetFrameworkInfoMessage;
#endif

        if (Interlocked.Exchange(ref _ifSupportedIgnoredMessageEmitted, 1) != 0)
        {
            return;
        }

        await _outputDisplay.DisplayAsync(
            this,
            new FormattedTextOutputDeviceData(message),
            cancellationToken).ConfigureAwait(false);
    }

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        if (IsCrashHandlingEffective())
        {
            _artifactPublisher.SnapshotPreExistingDumps(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsCrashHandlingEffective())
        {
            return;
        }

        if (testHostProcessInformation.HasExitedGracefully
            || (AppDomain.CurrentDomain.GetData("ProcessKilledByHangDump") is string processKilledByHangDump && processKilledByHangDump == "true"))
        {
            _sequenceFileHandler.TryDelete();
            return;
        }

        await _artifactPublisher.PublishAsync(testHostProcessInformation, cancellationToken).ConfigureAwait(false);
        await _sequenceFileHandler.TryPublishAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string GetDumpDirectory(string dumpFileNamePattern)
        => CrashDumpFileNameHelper.GetDumpDirectory(dumpFileNamePattern);

    internal static Regex BuildDumpFileNameRegex(string fileName)
        => CrashDumpFileNameHelper.BuildDumpFileNameRegex(fileName);

    internal static string BuildDumpFileNameRegexPattern(string fileName)
        => CrashDumpFileNameHelper.BuildDumpFileNameRegexPattern(fileName);

    private bool IsCrashHandlingEffective()
        => _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)
            || CrashDumpEnvironmentVariableProvider.IsCrashReportEffective(_commandLineOptions);
}
