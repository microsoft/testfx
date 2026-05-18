// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IDataProducer, IOutputDeviceDataProducer
{
    private const string CrashReportFileExtension = ".crashreport.json";
    private const string CrashReportFileSearchPattern = "*" + CrashReportFileExtension;

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDisplay;
    private readonly CrashDumpConfiguration _netCoreCrashDumpGeneratorConfiguration;

    public CrashDumpProcessLifetimeHandler(
        ICommandLineOptions commandLineOptions,
        IMessageBus messageBus,
        IOutputDevice outputDisplay,
        CrashDumpConfiguration netCoreCrashDumpGeneratorConfiguration)
    {
        _commandLineOptions = commandLineOptions;
        _messageBus = messageBus;
        _outputDisplay = outputDisplay;
        _netCoreCrashDumpGeneratorConfiguration = netCoreCrashDumpGeneratorConfiguration;
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
             _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName)) &&
            _netCoreCrashDumpGeneratorConfiguration.Enable);

    public Task BeforeTestHostProcessStartAsync(CancellationToken _) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (testHostProcessInformation.HasExitedGracefully
            || (AppDomain.CurrentDomain.GetData("ProcessKilledByHangDump") is string processKilledByHangDump && processKilledByHangDump == "true"))
        {
            return;
        }

        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        bool generateDump = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);
        bool generateCrashReport = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName);

        // TODO: Crash dump supports more placeholders that we don't handle here.
        // See "Dump name formatting" in:
        // https://github.com/dotnet/runtime/blob/82742628310076fff22d7e7ee216a74384352056/docs/design/coreclr/botr/xplat-minidump-generation.md
        string expectedDumpFile = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
        string expectedCrashReportFile = $"{expectedDumpFile}{CrashReportFileExtension}";

        // Inspect the disk before emitting the crash banner so the message reflects
        // what was actually produced, not what was requested. The runtime may fail
        // to emit one (or both) of the artifacts, e.g. when EnableCrashReport is
        // unsupported on the current platform/version.
        bool dumpArtifactProduced = generateDump && File.Exists(expectedDumpFile);
        bool crashReportArtifactProduced = generateCrashReport && File.Exists(expectedCrashReportFile);

        string? processCrashedMessage = (dumpArtifactProduced, crashReportArtifactProduced) switch
        {
            (true, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpAndReportFileCreated, testHostProcessInformation.PID),
            (false, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, testHostProcessInformation.PID),
            (true, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID),
            (false, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, testHostProcessInformation.PID),
        };
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(processCrashedMessage), cancellationToken).ConfigureAwait(false);

        if (generateDump)
        {
            if (dumpArtifactProduced)
            {
                await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(expectedDumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
            }
            else
            {
                await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellationToken).ConfigureAwait(false);

                // Filter by exact extension to defend against Windows' legacy 8.3 short-name
                // matching where a pattern like '*.dmp' can also match files whose extension
                // merely starts with '.dmp' (for example 'foo.dmp.crashreport.json').
                foreach (string dumpFile in Directory.EnumerateFiles(Path.GetDirectoryName(expectedDumpFile)!, "*.dmp")
                    .Where(static f => Path.GetExtension(f).Equals(".dmp", StringComparison.OrdinalIgnoreCase)))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                }
            }
        }

        if (generateCrashReport)
        {
            if (crashReportArtifactProduced)
            {
                await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(expectedCrashReportFile), CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription)).ConfigureAwait(false);
            }
            else
            {
                await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashReportFile, expectedCrashReportFile, CrashReportFileSearchPattern)), cancellationToken).ConfigureAwait(false);

                // Filter by exact suffix to defend against Windows' legacy 8.3 short-name
                // matching where a pattern can also match files whose extension only starts
                // with the requested extension.
                foreach (string crashReportFile in Directory.GetFiles(Path.GetDirectoryName(expectedCrashReportFile)!, CrashReportFileSearchPattern)
                    .Where(static f => f.EndsWith(CrashReportFileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(crashReportFile), CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription)).ConfigureAwait(false);
                }
            }
        }
    }
}
