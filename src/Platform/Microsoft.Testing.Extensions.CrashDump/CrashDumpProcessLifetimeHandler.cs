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
    private HashSet<string> _preExistingDumpFiles = new(StringComparer.OrdinalIgnoreCase);

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

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        // Snapshot any pre-existing files in the dump directory so we can later restrict dump publication
        // to files that appeared during this run. Without this, when the results/dump directory is reused
        // across runs, stale dumps from a previous crash whose names also match the configured pattern
        // would be surfaced as artifacts of the current failure.
        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        string dumpDirectory = GetDumpDirectory(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern);
        if (Directory.Exists(dumpDirectory))
        {
            _preExistingDumpFiles = new HashSet<string>(Directory.EnumerateFiles(dumpDirectory), StringComparer.OrdinalIgnoreCase);
        }

        return Task.CompletedTask;
    }

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

        // The crash dump file name pattern can contain placeholders such as %p (PID), %e (process exe name),
        // %h (hostname), %t (timestamp), etc. that are expanded by the .NET runtime when it writes the dump.
        // See "Dump name formatting" in:
        // https://github.com/dotnet/runtime/blob/82742628310076fff22d7e7ee216a74384352056/docs/design/coreclr/botr/xplat-minidump-generation.md
        // We convert the file name part of the pattern into a regular expression (escaping literal characters
        // and turning %X placeholders into '.*') so we can collect not just the testhost dump but also dumps
        // produced by any of its child processes that may have crashed alongside it. Using a regex (instead
        // of passing the pattern as a glob to Directory.EnumerateFiles) ensures that any literal glob
        // metacharacter (e.g. '*' or '?') in the configured file name is matched literally and not as a
        // wildcard, which would otherwise cause unrelated files to be picked up on file systems that allow
        // these characters in file names (e.g. Linux/macOS).
        string dumpFileNamePattern = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern;
        string expectedDumpFile = dumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
        string expectedCrashReportFile = $"{expectedDumpFile}{CrashReportFileExtension}";
        string dumpDirectory = GetDumpDirectory(dumpFileNamePattern);
        Regex dumpFileNameRegex = BuildDumpFileNameRegex(Path.GetFileName(dumpFileNamePattern));

        bool publishedAnyDump = false;
        if (generateDump && Directory.Exists(dumpDirectory))
        {
            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory))
            {
                if (dumpFileNameRegex.IsMatch(Path.GetFileName(dumpFile))
                    && !_preExistingDumpFiles.Contains(dumpFile))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                    publishedAnyDump = true;
                }
            }
        }

        // Inspect the disk before emitting the crash banner so the message reflects
        // what was actually produced, not what was requested. The runtime may fail
        // to emit one (or both) of the artifacts, e.g. when EnableCrashReport is
        // unsupported on the current platform/version.
        bool dumpArtifactProduced = generateDump && (publishedAnyDump || File.Exists(expectedDumpFile));
        bool crashReportArtifactProduced = generateCrashReport && File.Exists(expectedCrashReportFile);

        string processCrashedMessage = (dumpArtifactProduced, crashReportArtifactProduced) switch
        {
            (true, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpAndReportFileCreated, testHostProcessInformation.PID),
            (false, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, testHostProcessInformation.PID),
            (true, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID),
            (false, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, testHostProcessInformation.PID),
        };
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(processCrashedMessage), cancellationToken).ConfigureAwait(false);

        if (generateDump && !publishedAnyDump)
        {
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(dumpDirectory))
            {
                // Filter by exact extension to defend against Windows' legacy 8.3 short-name
                // matching where a pattern like '*.dmp' can also match files whose extension
                // merely starts with '.dmp' (for example 'foo.dmp.crashreport.json').
                foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, "*.dmp")
                    .Where(static f => Path.GetExtension(f).Equals(".dmp", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!_preExistingDumpFiles.Contains(dumpFile))
                    {
                        await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                    }
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

    internal static string GetDumpDirectory(string dumpFileNamePattern)
    {
        // Path.GetDirectoryName returns "" (not null) for a bare filename on .NET Core/5+ but throws
        // ArgumentException for an empty string on .NET Framework; treat both as the current working
        // directory so the dump enumeration is not silently skipped.
        if (dumpFileNamePattern is null or "")
        {
            return ".";
        }

        string? rawDirectory = Path.GetDirectoryName(dumpFileNamePattern);
        return rawDirectory is null or "" ? "." : rawDirectory;
    }

    internal static Regex BuildDumpFileNameRegex(string fileName)
        => new(BuildDumpFileNameRegexPattern(fileName), RegexOptions.CultureInvariant);

    internal static string BuildDumpFileNameRegexPattern(string fileName)
    {
        var sb = new StringBuilder("^");
        bool lastWasWildcard = false;
        for (int i = 0; i < fileName.Length; i++)
        {
            if (fileName[i] == '%' && i + 1 < fileName.Length)
            {
                // Replace any %X placeholder with ".*". Collapse consecutive wildcards to keep the regex
                // simple and to avoid backtracking on patterns like "%p%t".
                if (!lastWasWildcard)
                {
                    sb.Append(".*");
                    lastWasWildcard = true;
                }

                i++;
            }
            else
            {
                sb.Append(Regex.Escape(fileName[i].ToString()));
                lastWasWildcard = false;
            }
        }

        sb.Append('$');
        return sb.ToString();
    }
}
