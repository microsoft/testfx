// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.CommandLine;

internal struct CommandLineOptionsProviderCache(ICommandLineOptionsProvider commandLineOptionsProvider) : ICommandLineOptionsProvider
{
    private CommandLineOption[]? _commandLineOptions;

    public readonly string Uid => commandLineOptionsProvider.Uid;

    public readonly string Version => commandLineOptionsProvider.Version;

    public readonly string DisplayName => commandLineOptionsProvider.DisplayName;

    public readonly string Description => commandLineOptionsProvider.Description;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        _commandLineOptions ??= commandLineOptionsProvider.GetCommandLineOptions().ToArray();

        return _commandLineOptions;
    }

    public readonly Task<bool> IsEnabledAsync()
        => commandLineOptionsProvider.IsEnabledAsync();

    public readonly Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions);

    public readonly Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandLineOptionsProvider.ValidateOptionArgumentsAsync(commandOption, arguments);
}
