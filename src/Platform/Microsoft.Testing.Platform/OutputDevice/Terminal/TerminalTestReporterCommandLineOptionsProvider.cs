// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal class TerminalTestReporterCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string NoProgressOption = "no-progress";
    public const string NoAnsiOption = "no-ansi";

    /// <inheritdoc />
    public string Uid { get; } = nameof(TerminalTestReporterCommandLineOptionsProvider);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = PlatformResources.TerminalTestReporterDisplayName;

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.TerminalTestReporterDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(NoProgressOption, PlatformResources.TerminalNoProgressOptionDescription, ArgumentArity.Zero, false),
            new(NoAnsiOption, PlatformResources.TerminalNoAnsiOptionDescription, ArgumentArity.Zero, false)
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => // No problem found
        ValidationResult.ValidTask;
}
