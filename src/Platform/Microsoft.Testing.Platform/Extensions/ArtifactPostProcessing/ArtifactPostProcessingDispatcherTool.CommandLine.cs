// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

internal sealed class ArtifactPostProcessingDispatcherToolCommandLine : IToolCommandLineOptionsProvider
{
    public const string ManifestOptionName = "manifest";

    public string Uid => "Microsoft.Testing.Platform.ArtifactPostProcessing.Dispatcher.CommandLine";

    public string Version => PlatformVersion.Version;

    public string DisplayName => PlatformResources.ArtifactPostProcessingDispatcherDisplayName;

    public string Description => PlatformResources.ArtifactPostProcessingDispatcherCommandLineDescription;

    public string ToolName => ArtifactPostProcessingDispatcherTool.ToolName;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => [new(ManifestOptionName, PlatformResources.ArtifactPostProcessingDispatcherManifestOptionDescription, ArgumentArity.ExactlyOne, isHidden: true)];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => arguments is { Length: 1 } && File.Exists(arguments[0])
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(PlatformResources.ArtifactPostProcessingDispatcherManifestNotFound);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
