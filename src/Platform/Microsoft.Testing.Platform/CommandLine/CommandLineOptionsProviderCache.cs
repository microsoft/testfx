// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.CommandLine;

internal struct CommandLineOptionsProviderCache : ICommandLineOptionsProvider
{
    private readonly ICommandLineOptionsProvider _commandLineOptionsProvider;
    private CommandLineOption[]? _commandLineOptions;

    public CommandLineOptionsProviderCache(ICommandLineOptionsProvider commandLineOptionsProvider)
    {
        _commandLineOptionsProvider = commandLineOptionsProvider;
    }

    public readonly string Uid => _commandLineOptionsProvider.Uid;

    public readonly string Version => _commandLineOptionsProvider.Version;

    public readonly string DisplayName => _commandLineOptionsProvider.DisplayName;

    public readonly string Description => _commandLineOptionsProvider.Description;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        _commandLineOptions ??= _commandLineOptionsProvider.GetCommandLineOptions().ToArray();

        return _commandLineOptions;
    }

    public readonly Task<bool> IsEnabledAsync()
        => _commandLineOptionsProvider.IsEnabledAsync();

    public readonly Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => _commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions);

    public readonly Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => _commandLineOptionsProvider.ValidateOptionArgumentsAsync(commandOption, arguments);
}
