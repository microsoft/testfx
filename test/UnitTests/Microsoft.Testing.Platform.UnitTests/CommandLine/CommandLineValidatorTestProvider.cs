// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

/// <summary>
/// Configurable <see cref="ICommandLineOptionsProvider"/> used to drive <c>CommandLineOptionsValidator</c> scenarios.
/// </summary>
internal class CommandLineValidatorTestProvider : ICommandLineOptionsProvider
{
    private readonly CommandLineOption[] _options;
    private readonly Func<CommandLineOption, string[], ValidationResult>? _validateArguments;
    private readonly string? _configurationError;

    public CommandLineValidatorTestProvider(
        string displayName,
        IEnumerable<CommandLineOption> options,
        Func<CommandLineOption, string[], ValidationResult>? validateArguments = null,
        string? configurationError = null)
    {
        DisplayName = displayName;
        _options = [.. options];
        _validateArguments = validateArguments;
        _configurationError = configurationError;
    }

    public List<(string OptionName, string[] Arguments)> ValidatedArguments { get; } = [];

    public string Uid => DisplayName + "-uid";

    public string Version => "1.0.0";

    public string DisplayName { get; }

    public string Description => "Test provider";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => _options;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        ValidatedArguments.Add((commandOption.Name, arguments));
        return Task.FromResult(_validateArguments?.Invoke(commandOption, arguments) ?? ValidationResult.Valid());
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => Task.FromResult(_configurationError is null ? ValidationResult.Valid() : ValidationResult.Invalid(_configurationError));
}

internal sealed class CommandLineValidatorTestToolProvider : CommandLineValidatorTestProvider, IToolCommandLineOptionsProvider
{
    public CommandLineValidatorTestToolProvider(string displayName, string toolName, IEnumerable<CommandLineOption> options)
        : base(displayName, options)
        => ToolName = toolName;

    public string ToolName { get; }
}
