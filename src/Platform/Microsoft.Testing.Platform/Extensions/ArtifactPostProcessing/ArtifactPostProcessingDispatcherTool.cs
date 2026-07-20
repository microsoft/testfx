// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

internal sealed class ArtifactPostProcessingDispatcherTool(
    ICommandLineOptions commandLineOptions,
    IReadOnlyList<IArtifactPostProcessor> processors,
    IPushOnlyProtocol? protocol,
    IEnvironment environment,
    IOutputDevice outputDevice) : ITool, IOutputDeviceDataProducer
{
    public const string ToolName = "internal-merge-artifacts";

    private readonly ICommandLineOptions _commandLineOptions = commandLineOptions;
    private readonly IEnvironment _environment = environment;
    private readonly IOutputDevice _outputDevice = outputDevice;
    private readonly IReadOnlyList<IArtifactPostProcessor> _processors = processors;
    private readonly IPushOnlyProtocol? _protocol = protocol;

    public string Name => ToolName;

    public bool IsHidden => true;

    public string Uid => "Microsoft.Testing.Platform.ArtifactPostProcessing.Dispatcher";

    public string Version => PlatformVersion.Version;

    public string DisplayName => PlatformResources.ArtifactPostProcessingDispatcherDisplayName;

    public string Description => PlatformResources.ArtifactPostProcessingDispatcherDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (_protocol is null || !_protocol.IsServerMode)
        {
            await DisplayErrorAsync(PlatformResources.ArtifactPostProcessingDispatcherRequiresPipe, cancellationToken).ConfigureAwait(false);
            return (int)ExitCode.InvalidCommandLine;
        }

        ApplicationStateGuard.Ensure(
            _commandLineOptions.TryGetOptionArgumentList(ArtifactPostProcessingDispatcherToolCommandLine.ManifestOptionName, out string[]? manifestPaths));
        RoslynDebug.Assert(manifestPaths.Length == 1);

        ArtifactPostProcessingManifest manifest;
        try
        {
            manifest = ArtifactPostProcessingManifest.Load(manifestPaths[0]);
            Directory.CreateDirectory(manifest.OutputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException or NotSupportedException)
        {
            await DisplayErrorAsync(
                string.Format(CultureInfo.CurrentCulture, PlatformResources.ArtifactPostProcessingManifestReadError, ex.Message),
                cancellationToken).ConfigureAwait(false);
            return (int)ExitCode.InvalidCommandLine;
        }

        await WarnAboutProcessorConflictsAsync(cancellationToken).ConfigureAwait(false);

        List<InputArtifact> unmatchedInputs = [.. manifest.Inputs];
        List<ProcessedArtifact> outputs = [];
        bool failed = false;
        foreach (IArtifactPostProcessor processor in _processors)
        {
            InputArtifact[] matchingInputs = [.. unmatchedInputs.Where(input => Matches(processor, input))];
            if (matchingInputs.Length == 0)
            {
                continue;
            }

            unmatchedInputs.RemoveAll(input => matchingInputs.Contains(input));
            try
            {
                if (await processor.ProcessAsync(matchingInputs, manifest.OutputDirectory, cancellationToken).ConfigureAwait(false) is { } output)
                {
                    outputs.Add(output);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed = true;
                await DisplayErrorAsync(
                    string.Format(CultureInfo.CurrentCulture, PlatformResources.ArtifactPostProcessingDispatcherProcessorFailed, processor.Uid, ex.Message),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (outputs.Count > 0)
        {
            FileArtifactMessage[] messages = [.. outputs.Select(output => new FileArtifactMessage(
                output.Path,
                output.DisplayName,
                output.Description,
                TestUid: null,
                TestDisplayName: null,
                SessionUid: null,
                output.Kind))];
            await _protocol.SendMessageAsync(new FileArtifactMessages(
                _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
                _protocol.InstanceId,
                messages)).ConfigureAwait(false);
        }

        return failed ? (int)ExitCode.GenericFailure : (int)ExitCode.Success;
    }

    private static bool Matches(IArtifactPostProcessor processor, InputArtifact input)
        => input.Kind is not null
            ? processor.SupportedKinds.Contains(input.Kind, StringComparer.Ordinal)
            : processor.SupportedFileExtensionsFallback.Contains(Path.GetExtension(input.Path), StringComparer.OrdinalIgnoreCase);

    private Task DisplayErrorAsync(string message, CancellationToken cancellationToken)
        => _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(message), cancellationToken);

    private async Task WarnAboutProcessorConflictsAsync(CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, string> conflict in FindProcessorConflicts(_processors))
        {
            await _outputDevice.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData(string.Format(
                    CultureInfo.CurrentCulture,
                    PlatformResources.ArtifactPostProcessingDispatcherProcessorConflict,
                    conflict.Key,
                    conflict.Value)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyDictionary<string, string> FindProcessorConflicts(IEnumerable<IArtifactPostProcessor> processors)
    {
        Dictionary<string, string> firstProcessorByCapability = [with(StringComparer.Ordinal)];
        Dictionary<string, string> conflicts = [with(StringComparer.Ordinal)];
        foreach (IArtifactPostProcessor processor in processors)
        {
            HashSet<string> capabilities = new(processor.SupportedKinds, StringComparer.Ordinal);
            capabilities.UnionWith(processor.SupportedFileExtensionsFallback.Select(extension => extension.ToLowerInvariant()));
            foreach (string capability in capabilities)
            {
                if (!firstProcessorByCapability.TryGetValue(capability, out string? firstProcessorUid))
                {
                    firstProcessorByCapability.Add(capability, processor.Uid);
                }
                else if (firstProcessorUid != processor.Uid && !conflicts.ContainsKey(capability))
                {
                    conflicts.Add(capability, firstProcessorUid);
                }
            }
        }

        return conflicts;
    }
}
