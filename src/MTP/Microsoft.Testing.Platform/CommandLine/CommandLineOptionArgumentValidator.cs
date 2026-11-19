// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Provides validation helpers for command line option arguments that accept boolean-like values.
/// </summary>
internal static class CommandLineOptionArgumentValidator
{
    private const string AutoValue = "auto";
    private static readonly string[] OnValues = ["on", "true", "enable", "1"];
    private static readonly string[] OffValues = ["off", "false", "disable", "0"];

    /// <summary>
    /// Determines whether the argument is one of the accepted on/off boolean values.
    /// </summary>
    public static bool IsValidBooleanArgument(string argument)
        => IsOnValue(argument) || IsOffValue(argument);

    /// <summary>
    /// Determines whether the argument is one of the accepted on/off/auto values.
    /// </summary>
    public static bool IsValidBooleanAutoArgument(string argument)
        => AutoValue.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || IsValidBooleanArgument(argument);

    /// <summary>
    /// Determines whether the argument represents an enabled state (<c>on</c>, <c>true</c>, <c>enable</c>, <c>1</c>).
    /// </summary>
    public static bool IsOnValue(string argument)
        => OnValues.Any(onValue => onValue.Equals(argument, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines whether the argument represents a disabled state (<c>off</c>, <c>false</c>, <c>disable</c>, <c>0</c>).
    /// </summary>
    public static bool IsOffValue(string argument)
        => OffValues.Any(offValue => offValue.Equals(argument, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines whether the argument represents the <c>auto</c> value.
    /// </summary>
    public static bool IsAutoValue(string argument)
        => AutoValue.Equals(argument, StringComparison.OrdinalIgnoreCase);
}
