// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Provides validation helpers for command line option arguments.
/// </summary>
internal static class CommandLineOptionArgumentValidator
{
    private static readonly string[] DefaultOnValues = ["on", "true", "enable", "1"];
    private static readonly string[] DefaultOffValues = ["off", "false", "disable", "0"];

    /// <summary>
    /// Validates that an argument is one of the accepted on/off boolean values.
    /// </summary>
    /// <param name="argument">The argument to validate.</param>
    /// <returns>True if the argument is valid; otherwise, false.</returns>
    public static bool IsValidBooleanArgument(string argument)
    {
        foreach (string onValue in DefaultOnValues)
        {
            if (onValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (string offValue in DefaultOffValues)
        {
            if (offValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that an argument is one of the accepted on/off/auto values.
    /// </summary>
    /// <param name="argument">The argument to validate.</param>
    /// <returns>True if the argument is valid; otherwise, false.</returns>
    public static bool IsValidBooleanAutoArgument(string argument)
        => "auto".Equals(argument, StringComparison.OrdinalIgnoreCase)
            || IsValidBooleanArgument(argument);

    /// <summary>
    /// Determines if an argument represents an "on/enabled" state.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <returns>True if the argument represents an enabled state; otherwise, false.</returns>
    public static bool IsOnValue(string argument)
    {
        foreach (string onValue in DefaultOnValues)
        {
            if (onValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if an argument represents an "off/disabled" state.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <returns>True if the argument represents a disabled state; otherwise, false.</returns>
    public static bool IsOffValue(string argument)
    {
        foreach (string offValue in DefaultOffValues)
        {
            if (offValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
