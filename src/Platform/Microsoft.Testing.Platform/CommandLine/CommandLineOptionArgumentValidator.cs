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
    /// <param name="onValues">The values representing on/enabled state (default: ["on", "true", "enable", "1"]).</param>
    /// <param name="offValues">The values representing off/disabled state (default: ["off", "false", "disable", "0"]).</param>
    /// <returns>True if the argument is valid; otherwise, false.</returns>
    public static bool IsValidBooleanArgument(
        string argument,
        string[]? onValues = null,
        string[]? offValues = null)
    {
        onValues ??= DefaultOnValues;
        offValues ??= DefaultOffValues;

        foreach (string onValue in onValues)
        {
            if (onValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (string offValue in offValues)
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
    /// <param name="autoValue">The value representing auto mode (default: "auto").</param>
    /// <param name="onValues">The values representing on/enabled state (default: ["on", "true", "enable", "1"]).</param>
    /// <param name="offValues">The values representing off/disabled state (default: ["off", "false", "disable", "0"]).</param>
    /// <returns>True if the argument is valid; otherwise, false.</returns>
    public static bool IsValidBooleanAutoArgument(
        string argument,
        string? autoValue = "auto",
        string[]? onValues = null,
        string[]? offValues = null)
    {
        onValues ??= DefaultOnValues;
        offValues ??= DefaultOffValues;

        if (autoValue is not null && autoValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsValidBooleanArgument(argument, onValues, offValues);
    }

    /// <summary>
    /// Determines if an argument represents an "on/enabled" state.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <param name="onValues">The values representing on/enabled state (default: ["on", "true", "enable", "1"]).</param>
    /// <returns>True if the argument represents an enabled state; otherwise, false.</returns>
    public static bool IsOnValue(string argument, string[]? onValues = null)
    {
        onValues ??= DefaultOnValues;

        foreach (string onValue in onValues)
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
    /// <param name="offValues">The values representing off/disabled state (default: ["off", "false", "disable", "0"]).</param>
    /// <returns>True if the argument represents a disabled state; otherwise, false.</returns>
    public static bool IsOffValue(string argument, string[]? offValues = null)
    {
        offValues ??= DefaultOffValues;

        foreach (string offValue in offValues)
        {
            if (offValue.Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
