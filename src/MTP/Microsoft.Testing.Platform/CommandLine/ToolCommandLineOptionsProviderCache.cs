// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal struct ToolCommandLineOptionsProviderCache(IToolCommandLineOptionsProvider commandLineOptionsProvider) : IToolCommandLineOptionsProvider
{
    private readonly IToolCommandLineOptionsProvider _commandLineOptionsProvider = commandLineOptionsProvider;
    private IReadOnlyCollection<CommandLineOption>? _commandLineOptions;

    public readonly string Uid => _commandLineOptionsProvider.Uid;

    public readonly string Version => _commandLineOptionsProvider.Version;

    public readonly string DisplayName => _commandLineOptionsProvider.DisplayName;

    public readonly string Description => _commandLineOptionsProvider.Description;

    public readonly string ToolName => _commandLineOptionsProvider.ToolName;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        _commandLineOptions ??= _commandLineOptionsProvider.GetCommandLineOptions();

        return _commandLineOptions;
    }

    public readonly Task<bool> IsEnabledAsync()
        => _commandLineOptionsProvider.IsEnabledAsync();

    public readonly Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => _commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions);

    public readonly Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => _commandLineOptionsProvider.ValidateOptionArgumentsAsync(commandOption, arguments);
}
