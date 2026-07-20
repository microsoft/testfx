// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class CrashDumpProcessLifetimeHandler
{
    private sealed class CrashDumpArtifactPublisher
    {
        private const string CrashReportFileExtension = ".crashreport.json";
        private const string CrashReportFileSearchPattern = "*" + CrashReportFileExtension;

        private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private readonly CrashDumpProcessLifetimeHandler _owner;
        private readonly ICommandLineOptions _commandLineOptions;
        private readonly IMessageBus _messageBus;
        private readonly IOutputDevice _outputDisplay;
        private readonly CrashDumpConfiguration _configuration;
        private readonly HashSet<string> _preExistingDumpFiles;

        public CrashDumpArtifactPublisher(
            CrashDumpProcessLifetimeHandler owner,
            ICommandLineOptions commandLineOptions,
            IMessageBus messageBus,
            IOutputDevice outputDisplay,
            CrashDumpConfiguration configuration)
        {
            _owner = owner;
            _commandLineOptions = commandLineOptions;
            _messageBus = messageBus;
            _outputDisplay = outputDisplay;
            _configuration = configuration;
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
            _preExistingDumpFiles = new(PathComparer);
#pragma warning restore IDE0028
        }

        public void SnapshotPreExistingDumps(CancellationToken cancellationToken)
        {
            ApplicationStateGuard.Ensure(_configuration.DumpFileNamePattern is not null);
            string dumpFileNamePattern = _configuration.DumpFileNamePattern;
            string dumpDirectory = CrashDumpFileNameHelper.GetDumpDirectory(dumpFileNamePattern);
            if (!Directory.Exists(dumpDirectory))
            {
                return;
            }

            string dumpSearchPattern = CrashDumpFileNameHelper.GetDumpSearchPattern(dumpFileNamePattern);
            foreach (string file in Directory.EnumerateFiles(dumpDirectory, dumpSearchPattern))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _preExistingDumpFiles.Add(file);
            }
        }

        public async Task PublishAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
        {
            ApplicationStateGuard.Ensure(_configuration.DumpFileNamePattern is not null);
            bool generateDump = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);
            bool generateCrashReport = CrashDumpEnvironmentVariableProvider.IsCrashReportEffective(_commandLineOptions);

            string dumpFileNamePattern = _configuration.DumpFileNamePattern;
            string expectedDumpFile = dumpFileNamePattern.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture));
            string expectedCrashReportFile = $"{expectedDumpFile}{CrashReportFileExtension}";
            string dumpDirectory = CrashDumpFileNameHelper.GetDumpDirectory(dumpFileNamePattern);
            string dumpFileNameOnly = Path.GetFileName(dumpFileNamePattern);
            Regex dumpFileNameRegex = CrashDumpFileNameHelper.BuildDumpFileNameRegex(dumpFileNameOnly);
            Regex testhostDumpRegex = CrashDumpFileNameHelper.BuildDumpFileNameRegex(
                dumpFileNameOnly.Replace("%p", testHostProcessInformation.PID.ToString(CultureInfo.InvariantCulture)));
            string dumpExtension = Path.GetExtension(dumpFileNameOnly);
            string dumpSearchPattern = CrashDumpFileNameHelper.GetDumpSearchPattern(dumpFileNamePattern);

            (bool PublishedAny, bool TesthostProduced) dumpResult = generateDump && Directory.Exists(dumpDirectory)
                ? await PublishMatchingDumpsAsync(dumpDirectory, dumpSearchPattern, dumpExtension, dumpFileNameRegex, testhostDumpRegex).ConfigureAwait(false)
                : default;

            bool testhostDumpProduced = generateDump && (dumpResult.TesthostProduced || File.Exists(expectedDumpFile));
            bool dumpArtifactProduced = generateDump && (testhostDumpProduced || dumpResult.PublishedAny);

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
            string processCrashedMessage = (dumpArtifactProduced, crashReportArtifactProduced) switch
            {
                (true, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpAndReportFileCreated, testHostProcessInformation.PID),
                (false, true) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedReportFileCreated, testHostProcessInformation.PID),
                (true, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashedDumpFileCreated, testHostProcessInformation.PID),
                (false, false) => string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpProcessCrashed, testHostProcessInformation.PID),
            };
            await _outputDisplay.DisplayAsync(_owner, new ErrorMessageOutputDeviceData(processCrashedMessage), cancellationToken).ConfigureAwait(false);

            if (generateDump && !testhostDumpProduced)
            {
                await PublishFallbackDumpsAsync(expectedDumpFile, dumpDirectory, dumpResult.PublishedAny, cancellationToken).ConfigureAwait(false);
            }

            if (generateCrashReport)
            {
                await PublishCrashReportsAsync(
                    expectedCrashReportFile,
                    expectedCrashReportFileExists,
                    matchedCrashReportFile,
                    crashReportFiles,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<(bool PublishedAny, bool TesthostProduced)> PublishMatchingDumpsAsync(
            string dumpDirectory,
            string dumpSearchPattern,
            string dumpExtension,
            Regex dumpFileNameRegex,
            Regex testhostDumpRegex)
        {
            bool publishedAny = false;
            bool testhostProduced = false;
            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, dumpSearchPattern))
            {
                if (dumpExtension.Length != 0
                    && !Path.GetExtension(dumpFile).Equals(dumpExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string dumpFileNameOnDisk = Path.GetFileName(dumpFile);
                if (!dumpFileNameRegex.IsMatch(dumpFileNameOnDisk) || _preExistingDumpFiles.Contains(dumpFile))
                {
                    continue;
                }

                await PublishArtifactAsync(dumpFile, CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription).ConfigureAwait(false);
                publishedAny = true;
                testhostProduced |= testhostDumpRegex.IsMatch(dumpFileNameOnDisk);
            }

            return (publishedAny, testhostProduced);
        }

        private async Task PublishFallbackDumpsAsync(string expectedDumpFile, string dumpDirectory, bool publishedAnyDump, CancellationToken cancellationToken)
        {
            await _outputDisplay.DisplayAsync(
                _owner,
                new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashDumpFile, expectedDumpFile)),
                cancellationToken).ConfigureAwait(false);

            if (publishedAnyDump || !Directory.Exists(dumpDirectory))
            {
                return;
            }

            foreach (string dumpFile in Directory.EnumerateFiles(dumpDirectory, "*.dmp")
                .Where(static f => Path.GetExtension(f).Equals(".dmp", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_preExistingDumpFiles.Contains(dumpFile))
                {
                    await PublishArtifactAsync(dumpFile, CrashDumpResources.CrashDumpArtifactDisplayName, CrashDumpResources.CrashDumpArtifactDescription).ConfigureAwait(false);
                }
            }
        }

        private async Task PublishCrashReportsAsync(
            string expectedCrashReportFile,
            bool expectedCrashReportFileExists,
            bool matchedCrashReportFile,
            List<string>? crashReportFiles,
            CancellationToken cancellationToken)
        {
            if (expectedCrashReportFileExists)
            {
                await PublishArtifactAsync(expectedCrashReportFile, CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription).ConfigureAwait(false);
                return;
            }

            if (matchedCrashReportFile)
            {
                foreach (string crashReportFile in crashReportFiles!)
                {
                    await PublishArtifactAsync(crashReportFile, CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription).ConfigureAwait(false);
                }

                return;
            }

            await _outputDisplay.DisplayAsync(
                _owner,
                new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CannotFindExpectedCrashReportFile, expectedCrashReportFile, CrashReportFileSearchPattern)),
                cancellationToken).ConfigureAwait(false);

            foreach (string crashReportFile in Directory.GetFiles(Path.GetDirectoryName(expectedCrashReportFile)!, CrashReportFileSearchPattern)
                .Where(static f => f.EndsWith(CrashReportFileExtension, StringComparison.OrdinalIgnoreCase)))
            {
                await PublishArtifactAsync(crashReportFile, CrashDumpResources.CrashReportArtifactDisplayName, CrashDumpResources.CrashReportArtifactDescription).ConfigureAwait(false);
            }
        }

        private Task PublishArtifactAsync(string path, string displayName, string description)
            => _messageBus.PublishAsync(_owner, new FileArtifact(new FileInfo(path), displayName, description));
    }
}
