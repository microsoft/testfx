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

    private int _ifSupportedIgnoredMessageEmitted;

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
             _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName) ||
             _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName)) &&
            _netCoreCrashDumpGeneratorConfiguration.Enable);

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        // If the user opted into the best-effort '--crash-report-if-supported' variant on a
        // runtime/platform where crash reports are unsupported emit a single informational line
        // so the user knows the option was accepted but silently no-opped. Two scenarios apply:
        //  - On .NET Framework the env-var-based createdump/crashreport mechanism is unavailable.
        //  - On Windows the .NET runtime ignores DOTNET_EnableCrashReport(Only) (dotnet/runtime#80191).
        // The "emit once" guard uses Interlocked.Exchange so that, if the test-host controller
        // ever invokes the hook more than once (e.g. on retry), only the first caller actually
        // emits the message.
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

        // Use FormattedTextOutputDeviceData (neutral, info-style) rather than
        // WarningMessageOutputDeviceData: this is the expected, graceful no-op path for a
        // best-effort option and we do not want CI logs to surface a yellow warning that the
        // user would interpret as a problem.
        await _outputDisplay.DisplayAsync(
            this,
            new FormattedTextOutputDeviceData(message),
            cancellationToken).ConfigureAwait(false);
    }

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        // When neither --crashdump nor an effective --crash-report is requested (only the
        // silent-no-op '--crash-report-if-supported' case on Windows or .NET Framework),
        // the env-var provider was disabled and DumpFileNamePattern was never populated.
        // Nothing to snapshot in that case.
        if (!IsCrashHandlingEffective())
        {
            return Task.CompletedTask;
        }

        // Snapshot any pre-existing files in the dump directory so we can later restrict dump publication
        // to files that appeared during this run. Without this, when the results/dump directory is reused
        // across runs, stale dumps from a previous crash whose names also match the configured pattern
        // would be surfaced as artifacts of the current failure.
        //
        // We *union* into the existing set rather than reassign it, so multiple invocations of this
        // callback (e.g. host restart) cannot drop entries that we have already classified as
        // pre-existing.
        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        string dumpFileNamePattern = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern;
        string dumpDirectory = GetDumpDirectory(dumpFileNamePattern);
        if (Directory.Exists(dumpDirectory))
        {
            // Narrow the snapshot to files that share the configured dump extension when one is
            // present, matching the search pattern we use on exit. This avoids paying for an
            // enumeration of every entry in the dump directory (which may also contain TRX files,
            // logs, attachments, ...), especially when `dumpDirectory` resolves to "." or to a
            // large shared --results-directory.
            string dumpSearchPattern = GetDumpSearchPattern(dumpFileNamePattern);
            foreach (string file in Directory.EnumerateFiles(dumpDirectory, dumpSearchPattern))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _preExistingDumpFiles.Add(file);
            }
        }

        return Task.CompletedTask;
    }

    private static string GetDumpSearchPattern(string dumpFileNamePattern)
    {
        string dumpExtension = Path.GetExtension(Path.GetFileName(dumpFileNamePattern));
        return dumpExtension.Length == 0 ? "*" : $"*{dumpExtension}";
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Mirror the OnTestHostProcessStartedAsync guard: in the silent-no-op case the env-var
        // provider was disabled, so there is no dump file pattern to scan and no crash artifact
        // to surface.
        if (!IsCrashHandlingEffective())
        {
            return;
        }

        if (testHostProcessInformation.HasExitedGracefully
            || (AppDomain.CurrentDomain.GetData("ProcessKilledByHangDump") is string processKilledByHangDump && processKilledByHangDump == "true"))
        {
            // No crash → the sequence file (if any) has no diagnostic value. Delete it so the user's
            // results directory is not polluted with stale "tests still running" logs after a clean
            // run. Hang-dump kills are handled identically because HangDump already produces its own
            // .log of in-progress tests via IPC.
            TryDeleteSequenceFile();
            return;
        }

        ApplicationStateGuard.Ensure(_netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern is not null);
        bool generateDump = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);
        bool generateCrashReport = CrashDumpEnvironmentVariableProvider.IsCrashReportEffective(_commandLineOptions);

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

        // Stricter regex that bakes in the testhost PID for any '%p' placeholder before expanding
        // the remaining placeholders as wildcards. We use this to recognize the testhost's own
        // dump (versus a child process dump) regardless of whether the configured name relies on
        // additional placeholders such as '%e', '%h' or '%t' - relying on `File.Exists` with the
        // literal-`%p`-substituted path would only work when '%p' is the only placeholder.
        //
        // Note: when the configured pattern omits '%p', this regex collapses to `dumpFileNameRegex`
        // (the `Replace("%p", ...)` call is a no-op) and we cannot distinguish testhost from child
        // dumps by name — any matching dump is treated as the testhost's. The runtime in that case
        // can only produce one dump per process matching the configured shape, so the practical
        // impact is limited to setups that pre-create files with the same shape under the dump
        // directory.
        Regex testhostDumpRegex = BuildDumpFileNameRegex(
            dumpFileNameOnly.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture)));

        // Narrow the file system enumeration to files that share the configured dump extension when
        // one is present. This avoids scanning every entry of the dump directory (which may also
        // contain TRX files, logs, attachments, ...). The placeholder-expanded regex above still
        // applies to filter out anything that does not match the configured name pattern.
        string dumpExtension = Path.GetExtension(dumpFileNameOnly);
        string dumpSearchPattern = GetDumpSearchPattern(dumpFileNamePattern);

        bool publishedAnyDump = false;
        bool testhostDumpProduced = false;
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

                string dumpFileNameOnDisk = Path.GetFileName(dumpFile);
                if (dumpFileNameRegex.IsMatch(dumpFileNameOnDisk)
                    && !_preExistingDumpFiles.Contains(dumpFile))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                    publishedAnyDump = true;

                    if (testhostDumpRegex.IsMatch(dumpFileNameOnDisk))
                    {
                        testhostDumpProduced = true;
                    }
                }
            }
        }

        // The crash banner and the "expected dump not found" warning are scoped to the testhost
        // dump specifically. We must not suppress them just because we published a dump for a
        // crashed child process: when only the child writes a dump, the user still needs to know
        // that the testhost's own dump never materialized.
        // Fall back to checking `expectedDumpFile` existence on disk to cover the edge case where
        // a file matching the literal-`%p`-substituted name was already present at start time (and
        // therefore skipped by the regex loop because it is in `_preExistingDumpFiles`) - we still
        // want the banner to reflect that the testhost dump is, technically, present on disk.
        testhostDumpProduced = generateDump && (testhostDumpProduced || File.Exists(expectedDumpFile));
        bool dumpArtifactProduced = generateDump && (testhostDumpProduced || publishedAnyDump);

        // The crash report file is written as "<dump file name>.crashreport.json" beside the dump.
        // The dump file name pattern can contain runtime placeholders besides "%p" (e.g. "%e" when
        // the user picks the {pname} token, "%h" or "%t" when configured directly). A plain
        // File.Exists check on `expectedCrashReportFile` would miss those reports, so we apply the
        // same testhost-dump-name regex to the prefix of each "*.crashreport.json" file in the dump
        // directory: this preserves the literal-`%p`-baked PID match while expanding any remaining
        // placeholders as wildcards, mirroring the dump-publication logic above.
        List<string>? crashReportFiles = null;
        bool matchedCrashReportFile = false;
        if (generateCrashReport && Directory.Exists(dumpDirectory))
        {
            foreach (string crashReportFile in Directory.EnumerateFiles(dumpDirectory, CrashReportFileSearchPattern))
            {
                string crashReportFileName = Path.GetFileName(crashReportFile);
                if (!crashReportFileName.EndsWith(CrashReportFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                crashReportFiles ??= [];
                crashReportFiles.Add(crashReportFile);

                string dumpFileNamePart = crashReportFileName.Substring(0, crashReportFileName.Length - CrashReportFileExtension.Length);
                if (testhostDumpRegex.IsMatch(dumpFileNamePart))
                {
                    matchedCrashReportFile = true;
                }
            }
        }

        bool expectedCrashReportFileExists = File.Exists(expectedCrashReportFile);
        bool crashReportArtifactProduced = expectedCrashReportFileExists || matchedCrashReportFile;

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
            if (expectedCrashReportFileExists)
            {
                await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(expectedCrashReportFile), CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription)).ConfigureAwait(false);
            }
            else if (matchedCrashReportFile)
            {
                foreach (string crashReportFile in crashReportFiles!)
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(crashReportFile), CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription)).ConfigureAwait(false);
                }
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

        await TryPublishSequenceFileAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task TryPublishSequenceFileAsync(CancellationToken cancellationToken)
    {
        string? sequenceFilePath = _netCoreCrashDumpGeneratorConfiguration.SequenceFileName;
        if (RoslynString.IsNullOrEmpty(sequenceFilePath) || !File.Exists(sequenceFilePath))
        {
            return;
        }

        // Parse the journal to compute the set of tests that started but never ended. We can tolerate
        // a partially-written final line because the testhost flushes after each whole record; any
        // half-written tail line would simply fail to parse and be ignored.
        var inFlight = new Dictionary<string, (string DisplayName, DateTimeOffset StartedAt)>(StringComparer.Ordinal);
        DateTimeOffset latestSeen = DateTimeOffset.MinValue;
        try
        {
            foreach (string line in File.ReadLines(sequenceFilePath))
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                // Format: <event>\t<isoTimestamp>\t<uid>\t<displayName-or-state>
                string[] parts = line.Split('\t');
                if (parts.Length < 4)
                {
                    continue;
                }

                if (!DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp))
                {
                    continue;
                }

                if (timestamp > latestSeen)
                {
                    latestSeen = timestamp;
                }

                string uid = parts[2];
                // Any field originally containing tabs was sanitized to spaces by the testhost, so
                // a 4-element split is sufficient. Defensively re-join any extras to tolerate future
                // schema changes.
                string lastField = parts.Length == 4 ? parts[3] : string.Join("\t", parts, 3, parts.Length - 3);

                if (parts[0].Equals(CrashDumpSequenceLogger.StartedEvent, StringComparison.Ordinal))
                {
                    inFlight[uid] = (lastField, timestamp);
                }
                else if (parts[0].Equals(CrashDumpSequenceLogger.EndedEvent, StringComparison.Ordinal))
                {
                    inFlight.Remove(uid);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            // Best-effort diagnostic. If we cannot read the sequence file for any expected reason
            // (ACLs, sharing violations, malformed path, etc.) we still publish it so the user
            // can inspect it manually, but skip the friendly summary. Failing the crash-handling
            // path because of a diagnostic file would be strictly worse than missing the summary.
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileReadError, sequenceFilePath, ex.Message)), cancellationToken).ConfigureAwait(false);
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(sequenceFilePath), CrashDumpResources.CrashDumpSequenceArtifactDisplayName, CrashDumpResources.CrashDumpSequenceArtifactDescription)).ConfigureAwait(false);
            return;
        }

        if (inFlight.Count > 0)
        {
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(CrashDumpResources.CrashDumpTestsRunningAtCrash), cancellationToken).ConfigureAwait(false);
            DateTimeOffset anchor = latestSeen == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : latestSeen;
            foreach (KeyValuePair<string, (string DisplayName, DateTimeOffset StartedAt)> entry in inFlight.OrderBy(static x => x.Value.StartedAt))
            {
                TimeSpan elapsed = anchor - entry.Value.StartedAt;
                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData($"[{elapsed}] {entry.Value.DisplayName}"), cancellationToken).ConfigureAwait(false);
            }
        }

        await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(sequenceFilePath), CrashDumpResources.CrashDumpSequenceArtifactDisplayName, CrashDumpResources.CrashDumpSequenceArtifactDescription)).ConfigureAwait(false);
    }

    private void TryDeleteSequenceFile()
    {
        string? sequenceFilePath = _netCoreCrashDumpGeneratorConfiguration.SequenceFileName;
        if (RoslynString.IsNullOrEmpty(sequenceFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(sequenceFilePath))
            {
                File.Delete(sequenceFilePath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover sequence file is harmless beyond cluttering the results
            // directory. Avoid surfacing this as a user-visible error because the run itself succeeded.
        }
        catch (UnauthorizedAccessException)
        {
            // Same rationale as IOException.
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

    // Returns true when at least one of the crash dump / crash report mechanisms is going to
    // produce an artifact for the current process. The silent-no-op cases (e.g.
    // '--crash-report-if-supported' alone on Windows or on .NET Framework) leave this false,
    // so the lifecycle callbacks know there is nothing to snapshot or publish.
    private bool IsCrashHandlingEffective()
        => _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)
            || CrashDumpEnvironmentVariableProvider.IsCrashReportEffective(_commandLineOptions);
}
