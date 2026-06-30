// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.CommandLine;

internal abstract class CommandLineOptionsProviderBase : ICommandLineOptionsProvider
{
    private readonly IReadOnlyCollection<CommandLineOption> _commandLineOptions;

    protected CommandLineOptionsProviderBase(
        string uid,
        string version,
        string displayName,
        string description,
        IReadOnlyCollection<CommandLineOption> commandLineOptions)
    {
        Uid = uid ?? throw new ArgumentNullException(nameof(uid));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _commandLineOptions = commandLineOptions ?? throw new ArgumentNullException(nameof(commandLineOptions));
    }

    protected CommandLineOptionsProviderBase(
        IExtension extension,
        IReadOnlyCollection<CommandLineOption> commandLineOptions)
        : this(
            (extension ?? throw new ArgumentNullException(nameof(extension))).Uid,
            extension.Version,
            extension.DisplayName,
            extension.Description,
            commandLineOptions)
    {
    }

    public string Uid { get; }

    public string Version { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => _commandLineOptions;

    public virtual Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public virtual Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    protected static Task<ValidationResult> ValidateAllowedValuesAsync(string value, string[] allowedValues, string formatString)
        => allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(string.Format(
                CultureInfo.InvariantCulture,
                formatString,
                value,
                string.Join(", ", allowedValues.Select(v => $"'{v}'"))));
}
