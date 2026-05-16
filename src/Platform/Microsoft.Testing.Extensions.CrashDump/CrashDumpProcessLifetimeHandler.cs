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
        // We convert the file name part of the pattern into a regular expression (escaping literal characters
        // and turning %X placeholders into '.*') so we can collect not just the testhost dump but also dumps
        // produced by any of its child processes that may have crashed alongside it. Using a regex (instead
        // of passing the pattern as a glob to Directory.EnumerateFiles) ensures that any literal glob
        // metacharacter (e.g. '*' or '?') in the configured file name is matched literally and not as a
        // wildcard, which would otherwise cause unrelated files to be picked up on file systems that allow
        // these characters in file names (e.g. Linux/macOS).
        string dumpFileNamePattern = _netCoreCrashDumpGeneratorConfiguration.DumpFileNamePattern;
        string dumpDirectory = GetDumpDirectory(dumpFileNamePattern);
        Regex dumpFileNameRegex = BuildDumpFileNameRegex(Path.GetFileName(dumpFileNamePattern));

        bool publishedAny = false;
        if (Directory.Exists(dumpDirectory))
        {
            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory))
            {
                if (dumpFileNameRegex.IsMatch(Path.GetFileName(dumpFile)))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                    publishedAny = true;
                }
            }
        }

        if (!publishedAny)
        {
            string expectedDumpFile = dumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
            await _outputDisplay.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)), cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(dumpDirectory))
            {
                foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, "*.dmp"))
                {
                    await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription)).ConfigureAwait(false);
                }
            }
        }
    }

    internal static string GetDumpDirectory(string dumpFileNamePattern)
    {
        // Path.GetDirectoryName returns "" (not null) for a bare filename on .NET Core/5+; treat that as
        // the current working directory so the dump enumeration is not silently skipped.
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
