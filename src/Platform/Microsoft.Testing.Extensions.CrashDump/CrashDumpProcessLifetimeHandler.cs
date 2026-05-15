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
        => Task.FromResult(_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)
        && _netCoreCrashDumpGeneratorConfiguration.Enable);

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
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID)), cancellationToken).ConfigureAwait(false);

        // The crash dump file name pattern can contain placeholders such as %p (PID), %e (process exe name),
        // %h (hostname), %t (timestamp), etc. that are expanded by the .NET runtime when it writes the dump.
        // See "Dump name formatting" in:
        // https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/xplat-minidump-generation.md
        // We replace every placeholder with a wildcard so we can collect not just the testhost dump but also
        // dumps produced by any of its child processes that may have crashed alongside it.
        string dumpFileNamePattern = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern;
        string? dumpDirectory = Path.GetDirectoryName(dumpFileNamePattern);
        string searchPattern = ReplaceCrashDumpPlaceholdersWithWildcard(Path.GetFileName(dumpFileNamePattern));

        bool publishedAny = false;
        if (dumpDirectory is not null && Directory.Exists(dumpDirectory))
        {
            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, searchPattern))
            {
                await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                publishedAny = true;
            }
        }

        if (!publishedAny)
        {
            string expectedDumpFile = dumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellationToken).ConfigureAwait(false);
            if (dumpDirectory is not null && Directory.Exists(dumpDirectory))
            {
                foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, "*.dmp"))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                }
            }
        }
    }

    internal static string ReplaceCrashDumpPlaceholdersWithWildcard(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        for (int i = 0; i < fileName.Length; i++)
        {
            if (fileName[i] == '%' && i + 1 < fileName.Length)
            {
                // Replace any %X placeholder with '*'. Collapse consecutive wildcards to keep the search
                // pattern minimal and avoid confusing search engines with redundant '**' sequences.
                if (sb.Length == 0 || sb[sb.Length - 1] != '*')
                {
                    sb.Append('*');
                }

                i++;
            }
            else
            {
                sb.Append(fileName[i]);
            }
        }

        return sb.ToString();
    }
}
