// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions;

internal sealed class MSBuildCommandLineProvider : ICommandLineOptionsProvider
{
    public const string MSBuildNodeOptionKey = "internal-msbuild-node";

    public string Uid => nameof(MSBuildCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(MSBuildCommandLineProvider);

    public string Description => Resources.MSBuildResources.MSBuildExtensionsDescription;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(MSBuildNodeOptionKey, "Used to pass the msbuild node handle", ArgumentArity.ExactlyOne, isHidden: true, isBuiltIn: true),
        };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;
}
