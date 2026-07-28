// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// <b>Infrastructure.</b> Base class that implements the boilerplate of
/// <see cref="ICommandLineOptionsProvider"/> for first-party command-line option providers.
/// </summary>
/// <remarks>
/// <b>This type is not intended to be used directly from application code.</b> It is public only so
/// that first-party platform extensions can derive from it across the assembly boundary without an
/// <c>InternalsVisibleTo</c> grant. Its shape is an implementation detail; do not depend on it from
/// your own code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[Experimental("TPINTERNAL")]
public abstract class CommandLineOptionsProviderBase : ICommandLineOptionsProvider
{
    private readonly IReadOnlyCollection<CommandLineOption> _commandLineOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineOptionsProviderBase"/> class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineOptionsProviderBase"/> class from an extension.
    /// </summary>
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

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => _commandLineOptions;

    /// <inheritdoc />
    public virtual Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    /// <inheritdoc />
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

    /// <summary>
    /// Returns a valid result when <paramref name="value"/> is one of <paramref name="allowedValues"/>
    /// (case-insensitive), otherwise an invalid result formatted with <paramref name="formatString"/>.
    /// </summary>
    protected static Task<ValidationResult> ValidateAllowedValuesAsync(string value, string[] allowedValues, string formatString)
        => allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(string.Format(
                CultureInfo.InvariantCulture,
                formatString,
                value,
                string.Join(", ", allowedValues.Select(v => $"'{v}'"))));
}
