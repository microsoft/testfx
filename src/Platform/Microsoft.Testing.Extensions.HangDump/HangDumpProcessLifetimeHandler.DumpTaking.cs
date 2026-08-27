// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

#if NETCOREAPP
using Microsoft.Diagnostics.NETCore.Client;
#endif

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class HangDumpProcessLifetimeHandler
{
    private async Task TakeDumpAsync(IProcess process, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);
        ApplicationStateGuard.Ensure(_dumpType is not null);

        string processId = process.Id.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(process.Name, processId, _clock.UtcNow);

        string pattern = _dumpFileNamePattern ?? $"{process.Name}_%p_hang.dmp";

        // First resolve {placeholder} templates, then handle legacy %p pattern for backward compatibility.
        string finalDumpFileName = ArtifactNamingHelper.ResolveTemplate(pattern, replacements)
            .Replace("%p", processId);
        string resultsDirectory = Path.GetFullPath(_configuration.GetTestResultDirectory());
        finalDumpFileName = Path.GetFullPath(Path.Combine(resultsDirectory, finalDumpFileName));

        // Reject resolved paths that escape the results directory (e.g. rooted paths or ".." segments).
        // Append a trailing separator to prevent sibling-directory bypass (e.g. "/tmp/results" vs "/tmp/results-evil").
        // Use case-insensitive comparison on Windows where paths are case-insensitive.
        StringComparison pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string separatorStr = Path.DirectorySeparatorChar.ToString();
        string resultsDirectoryGuard = resultsDirectory.EndsWith(separatorStr, StringComparison.Ordinal)
            ? resultsDirectory
            : resultsDirectory + separatorStr;
        if (!finalDumpFileName.StartsWith(resultsDirectoryGuard, pathComparison))
        {
            throw new InvalidOperationException($"The resolved dump file path '{finalDumpFileName}' is outside the results directory '{resultsDirectory}'. Ensure --hangdump-filename is a relative path without '..' segments.");
        }

        // Ensure the destination directory exists (templates may include directory separators, e.g. {asm}/{pname}).
        Directory.CreateDirectory(Path.GetDirectoryName(finalDumpFileName)!);

        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        GetInProgressTestsResponse tests = await _namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), cancellationToken).ConfigureAwait(false);
        if (tests.Tests.Length > 0)
        {
            string hangTestsFileName = Path.ChangeExtension(finalDumpFileName, ".log");
            using (FileStream fs = File.OpenWrite(hangTestsFileName))
            using (StreamWriter sw = new(fs))
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(ExtensionResources.RunningTestsWhileDumping), cancellationToken).ConfigureAwait(false);
                foreach ((string testName, int seconds) in tests.Tests)
                {
                    await sw.WriteLineAsync($"[{TimeSpan.FromSeconds(seconds)}] {testName}").ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"[{TimeSpan.FromSeconds(seconds)}] {testName}"), cancellationToken).ConfigureAwait(false);
                }
            }

            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(hangTestsFileName), ExtensionResources.HangTestListArtifactDisplayName, ExtensionResources.HangTestListArtifactDescription)).ConfigureAwait(false);
        }

        await _logger.LogInformationAsync($"Creating dump filename {finalDumpFileName}").ConfigureAwait(false);

        await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.CreatingDumpFile, finalDumpFileName)), cancellationToken).ConfigureAwait(false);

#if NETCOREAPP
        DiagnosticsClient diagnosticsClient = new(process.Id);
        DumpType? dumpType = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => DumpType.Normal,
            "heap" => DumpType.WithHeap,
            "triage" => DumpType.Triage,
            "full" => DumpType.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        DumpFileNames dumpFileNames = GetDumpFileNames(finalDumpFileName);

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (dumpType.HasValue)
            {
                diagnosticsClient.WriteDump(dumpType.Value, dumpFileNames.WriteDumpFileName, logDumpGeneration: false);
                _dumpFiles.Add(dumpFileNames.ArtifactDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }

#else
        MiniDumpWriteDump.MiniDumpTypeOption? miniDumpTypeOption = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => MiniDumpWriteDump.MiniDumpTypeOption.Mini,
            "heap" => MiniDumpWriteDump.MiniDumpTypeOption.Heap,
            "full" => MiniDumpWriteDump.MiniDumpTypeOption.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (miniDumpTypeOption.HasValue)
            {
                MiniDumpWriteDump.CollectDumpUsingMiniDumpWriteDump(process.Id, finalDumpFileName, miniDumpTypeOption.Value);
                _dumpFiles.Add(finalDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    // Wrap the dump path into "" when it has space in it, this is a workaround for this runtime issue: https://github.com/dotnet/diagnostics/issues/5020
    // It only affects windows. Otherwise the dump creation fails with: [createdump] The pid argument is no longer supported
    internal static DumpFileNames GetDumpFileNames(string dumpFileName)
        => new(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && dumpFileName.Contains(' ')
                ? $"\"{dumpFileName}\""
                : dumpFileName,
            dumpFileName);

    internal readonly record struct DumpFileNames(string WriteDumpFileName, string ArtifactDumpFileName);
}
