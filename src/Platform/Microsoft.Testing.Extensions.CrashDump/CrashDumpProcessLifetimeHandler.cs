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

    // The dump enumeration relies on this set to identify dumps that pre-date the current test
    // run so it can skip them when publishing artifacts. File paths are case-insensitive on
    // Windows but case-sensitive on Linux/macOS, so use an OS-appropriate comparer to avoid
    // treating freshly produced dumps as "pre-existing" merely because of casing differences.
    //
    // KNOWN LIMITATION: when multiple testhost processes share the same dump directory (e.g. a
    // user running two `dotnet test` invocations into the same --results-directory), each
    // handler instance snapshots only the files present at *its own* start time. If process B
    // writes a dump after handler A's snapshot, handler A may publish B's dump as if it were
    // its own. The previous "PID-only match" code had the same issue. A more robust fix would
    // require tracking per-file creation times against the snapshot time, which we deliberately
    // avoid here to keep the handler free of an `IClock` dependency.
    private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly HashSet<string> _preExistingDumpFiles;

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
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
        _preExistingDumpFiles = new(PathComparer);
#pragma warning restore IDE0028
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
        //
        // We *union* into the existing set rather than reassign it, so multiple invocations of this
        // callback (e.g. host restart) cannot drop entries that we have already classified as
        // pre-existing.
        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        string dumpDirectory = GetDumpDirectory(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern);
        if (Directory.Exists(dumpDirectory))
        {
            foreach (string file in Directory.EnumerateFiles(dumpDirectory))
            {
                _preExistingDumpFiles.Add(file);
            }
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
        string dumpFileNameOnly = Path.GetFileName(dumpFileNamePattern);
        Regex dumpFileNameRegex = BuildDumpFileNameRegex(dumpFileNameOnly);

        // Narrow the file system enumeration to files that share the configured dump extension when
        // one is present. This avoids scanning every entry of the dump directory (which may also
        // contain TRX files, logs, attachments, ...). The placeholder-expanded regex above still
        // applies to filter out anything that does not match the configured name pattern.
        string dumpExtension = Path.GetExtension(dumpFileNameOnly);
        string dumpSearchPattern = dumpExtension.Length == 0 ? "*" : $"*{dumpExtension}";

        bool publishedAnyDump = false;
        if (generateDump && Directory.Exists(dumpDirectory))
        {
            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, dumpSearchPattern))
            {
                // Filter by exact extension to defend against Windows' legacy 8.3 short-name
                // matching where a pattern like '*.dmp' can also match files whose extension
                // merely starts with '.dmp' (for example 'foo.dmp.crashreport.json').
                if (dumpExtension.Length != 0
                    && !Path.GetExtension(dumpFile).Equals(dumpExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (dumpFileNameRegex.IsMatch(Path.GetFileName(dumpFile))
                    && !_preExistingDumpFiles.Contains(dumpFile))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                    publishedAnyDump = true;
                }
            }
        }

        // The crash banner and the "expected dump not found" warning are scoped to the testhost
        // dump specifically. We must not suppress them just because we published a dump for a
        // crashed child process: when only the child writes a dump, the user still needs to know
        // that the testhost's own dump never materialized.
        bool testhostDumpProduced = generateDump && File.Exists(expectedDumpFile);
        bool dumpArtifactProduced = generateDump && (testhostDumpProduced || publishedAnyDump);
        bool crashReportArtifactProduced = generateCrashReport && File.Exists(expectedCrashReportFile);

        // Inspect the disk before emitting the crash banner so the message reflects
        // what was actually produced, not what was requested. The runtime may fail
        // to emit one (or both) of the artifacts, e.g. when EnableCrashReport is
        // unsupported on the current platform/version.
        string processCrashedMessage = (dumpArtifactProduced, crashReportArtifactProduced) switch
        {
            (true, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpAndReportFileCreated, testHostProcessInformation.PID),
            (false, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, testHostProcessInformation.PID),
            (true, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID),
            (false, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, testHostProcessInformation.PID),
        };
        await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(processCrashedMessage), cancellationToken).ConfigureAwait(false);

        if (generateDump && !testhostDumpProduced)
        {
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellationToken).ConfigureAwait(false);

            // Only fall back to a directory-wide `*.dmp` scan when neither the testhost dump nor any
            // other matching dump was published. This avoids re-enumerating the directory when we
            // already published at least one dump (e.g. a child process dump) above.
            if (!publishedAnyDump && Directory.Exists(dumpDirectory))
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
                // The .NET runtime's createdump tool treats "%%" as an escape for a literal '%'.
                // Preserve that behavior so a configured name like "My%%App_%p.dmp" produces a regex
                // that requires a literal '%' (rather than collapsing both characters into a wildcard
                // and over-matching unrelated files).
                if (fileName[i + 1] == '%')
                {
                    // '%' is not a regex metacharacter so it does not need escaping.
                    sb.Append('%');
                    lastWasWildcard = false;
                    i++;
                    continue;
                }

                // Replace any other %X placeholder with ".*". Collapse consecutive wildcards to keep
                // the regex simple and to avoid backtracking on patterns like "%p%t".
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
