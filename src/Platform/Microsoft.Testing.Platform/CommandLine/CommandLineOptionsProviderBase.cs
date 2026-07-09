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

    /// <summary>
    /// Returns an invalid <see cref="Task{ValidationResult}"/> when any of the <paramref name="subOptionNames"/>
    /// is set but <paramref name="mainOptionName"/> is not, otherwise returns <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Callers typically use the null-coalescing operator to fall through to further checks:
    /// <code>
    /// return RequiresMainOption(options, subOptionNames, mainOptionName, errorMessageFactory)
    ///     ?? ValidationResult.ValidTask;
    /// </code>
    /// </remarks>
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This is not meant to be an async await call but rather a task helper")]
    protected static Task<ValidationResult>? RequiresMainOption(
        ICommandLineOptions options,
        string[] subOptionNames,
        string mainOptionName,
        Func<string> errorMessageFactory)
    {
        bool anySubOption = subOptionNames.Any(options.IsOptionSet);
        return anySubOption && !options.IsOptionSet(mainOptionName)
            ? ValidationResult.InvalidTask(errorMessageFactory())
            : null;
    }

    /// <summary>
    /// Returns an invalid <see cref="Task{ValidationResult}"/> when any of the <paramref name="subOptionNames"/>
    /// is set but none of the <paramref name="mainOptionNames"/> is set, otherwise returns <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Callers typically use the null-coalescing operator to fall through to further checks:
    /// <code>
    /// return RequiresMainOption(options, subOptionNames, mainOptionNames, errorMessageFactory)
    ///     ?? ValidationResult.ValidTask;
    /// </code>
    /// </remarks>
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This is not meant to be an async await call but rather a task helper")]
    protected static Task<ValidationResult>? RequiresMainOption(
        ICommandLineOptions options,
        string[] subOptionNames,
        string[] mainOptionNames,
        Func<string> errorMessageFactory)
    {
        bool anySubOption = subOptionNames.Any(options.IsOptionSet);
        bool anyMainOption = mainOptionNames.Any(options.IsOptionSet);
        return anySubOption && !anyMainOption
            ? ValidationResult.InvalidTask(errorMessageFactory())
            : null;
    }

    protected static Task<ValidationResult> ValidateAllowedValuesAsync(string value, string[] allowedValues, string formatString)
        => allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(string.Format(
                CultureInfo.InvariantCulture,
                formatString,
                value,
                string.Join(", ", allowedValues.Select(v => $"'{v}'"))));
}
