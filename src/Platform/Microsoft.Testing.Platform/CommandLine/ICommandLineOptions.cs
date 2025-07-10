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

internal static class CommandLineOptionsExtensions
{
    public static bool TryGetOptionArgument(this ICommandLineOptions options, string optionName, [NotNullWhen(true)] out string? argument)
    {
        if (!options.TryGetOptionArgumentList(optionName, out var arguments) ||
            arguments.Length == 0)
        {
            argument = null;
            return false;
        }

        if (arguments.Length > 1)
        {
            throw new($"Multiple options named '{optionName}' are not supported.");
        }

        argument = arguments[0];
        return true;
    }
}
