// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class TreeNodeFilterCommandLineOptionsProvider(IExtension extension) : ICommandLineOptionsProvider
{
    public const string TreenodeFilter = "treenode-filter";

    /// <inheritdoc />
    public string Uid { get; } = extension.Uid;

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = extension.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = extension.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(TreenodeFilter, PlatformResources.TreeNodeFilterDescription, ArgumentArity.ExactlyOne, false)
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
