// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform;
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
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => CrashDumpResources.CrashDumpDisplayName;

    /// <inheritdoc />
    public string Description => CrashDumpResources.CrashDumpDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)
        && _netCoreCrashDumpGeneratorConfiguration.Enable);

    public Task BeforeTestHostProcessStartAsync(CancellationToken _) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation) => Task.CompletedTask;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested
            || testHostProcessInformation.HasExitedGracefully
            || (AppDomain.CurrentDomain.GetData("ProcessKilledByHangDump") is string processKilledByHangDump && processKilledByHangDump == "true"))
        {
            return;
        }

        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID)), cancellation).ConfigureAwait(false);

        string expectedDumpFile = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
        string? dumpDirectory = Path.GetDirectoryName(expectedDumpFile);
        if (RoslynString.IsNullOrEmpty(dumpDirectory) || !Directory.Exists(dumpDirectory))
        {
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellation).ConfigureAwait(false);
            return;
        }

        // Collect all dump files in the directory to capture crashes from child processes
        bool foundExpectedDump = false;
        foreach (string dumpFile in Directory.GetFiles(dumpDirectory, "*.dmp"))
        {
            if (string.Equals(dumpFile, expectedDumpFile, StringComparison.OrdinalIgnoreCase))
            {
                foundExpectedDump = true;
            }

            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
        }

        if (!foundExpectedDump)
        {
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellation).ConfigureAwait(false);
        }
    }
}
