// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Represents the interface for command line options.
/// </summary>
public interface ICommandLineOptions
{
    /// <summary>
    /// Checks if the specified option is set.
    /// </summary>
    /// <param name="optionName">The name of the option.</param>
    /// <returns>True if the option is set; otherwise, false.</returns>
    bool IsOptionSet(string optionName);

    /// <summary>
    /// Tries to get the argument list for the specified option.
    /// </summary>
    /// <param name="optionName">The name of the option.</param>
    /// <param name="arguments">The argument list for the option, if found.</param>
    /// <returns>True if the argument list is found; otherwise, false.</returns>
    bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments);
}

/// <summary>
/// Provides extension methods for command-line options.
/// </summary>
public static class CommandLineOptionsExtensions
{
    /// <summary>
    /// Tries to get the explicit argument list for an option, falling back to a passive default
    /// from <c>commandLineOptionDefaults</c> in <c>testconfig.json</c>.
    /// </summary>
    /// <param name="commandLineOptions">The command-line options.</param>
    /// <param name="optionName">The name of the option.</param>
    /// <param name="arguments">The explicit or default argument list, if found.</param>
    /// <returns><see langword="true"/> if an explicit value or configured default was found; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// A configured default does not make <see cref="ICommandLineOptions.IsOptionSet(string)"/> return
    /// <see langword="true"/>. Extensions should use this method only after determining that the feature
    /// which owns the option is enabled.
    /// </remarks>
    public static bool TryGetOptionArgumentListOrDefault(
        this ICommandLineOptions commandLineOptions,
        string optionName,
        [NotNullWhen(true)] out string[]? arguments)
    {
        _ = commandLineOptions ?? throw new ArgumentNullException(nameof(commandLineOptions));

        return commandLineOptions is ICommandLineOptionsWithDefaults commandLineOptionsWithDefaults
            ? commandLineOptionsWithDefaults.TryGetOptionArgumentListOrDefault(optionName, out arguments)
            : commandLineOptions.TryGetOptionArgumentList(optionName, out arguments);
    }
}

internal interface ICommandLineOptionsWithDefaults
{
    bool TryGetOptionArgumentListOrDefault(string optionName, [NotNullWhen(true)] out string[]? arguments);
}
