// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Provides extension methods for the IConfiguration interface.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Gets the test result directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The test result directory.</returns>
    public static string GetTestResultDirectory(this IConfiguration configuration)
    {
        string? resultDirectory = configuration[PlatformConfigurationConstants.PlatformResultDirectory];
        return resultDirectory ?? throw ApplicationStateGuard.Unreachable();
    }

    /// <summary>
    /// Gets the current working directory from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The current working directory.</returns>
    public static string GetCurrentWorkingDirectory(this IConfiguration configuration)
    {
        string? workingDirectory = configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        return workingDirectory ?? throw ApplicationStateGuard.Unreachable();
    }

    /// <summary>
    /// Determines whether the given CLI option name is set, consulting all registered
    /// <see cref="IConfigurationProvider"/> instances (CLI, environment variables, JSON,
    /// ...) under the <c>commandLineOptions:*</c> section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="CommandLineHandler.IsOptionSet(string)"/> to implement Option C of
    /// issue #6349 (unified read model for command-line options). Callers reading from
    /// <see cref="ICommandLineOptions"/> transparently benefit from JSON/env-var-sourced
    /// values without any code change.
    /// </para>
    /// <para>
    /// Semantics:
    /// <list type="bullet">
    ///   <item><description>If any indexed entry (<c>commandLineOptions:&lt;name&gt;:0</c>) is
    ///   present, the option is considered set (multi-value option).</description></item>
    ///   <item><description>Otherwise, the bare key (<c>commandLineOptions:&lt;name&gt;</c>) is
    ///   consulted. A null value means not set. A boolean value (<c>"true"</c>/<c>"false"</c>,
    ///   case-insensitive) is interpreted as the presence indicator for a zero-arity flag — a
    ///   <c>"false"</c> in JSON explicitly disables the option. Any other value is treated as a
    ///   single-argument value and therefore "set".</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal static bool IsCommandLineOptionSet(this IConfiguration configuration, string optionName)
    {
        string baseKey = GetBaseKey(optionName);

        if (configuration[baseKey + PlatformConfigurationConstants.KeyDelimiter + "0"] is not null)
        {
            return true;
        }

        string? bare = configuration[baseKey];
        return bare is not null && (!bool.TryParse(bare, out bool boolValue) || boolValue);
    }

    /// <summary>
    /// Returns the argument list for the given CLI option name. Sees values from every
    /// registered <see cref="IConfigurationProvider"/> (CLI, env vars, JSON, ...).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="CommandLineHandler.TryGetOptionArgumentList(string, out string[])"/>
    /// to implement Option C of issue #6349.
    /// </para>
    /// <para>
    /// Behavior is intentionally a superset of
    /// <see cref="CommandLineParseResult.TryGetOptionArgumentList(string, out string[])"/> so
    /// existing consumers continue to work:
    /// <list type="bullet">
    ///   <item><description>Walks <c>commandLineOptions:&lt;name&gt;:0</c>, <c>:1</c>, ... until a
    ///   gap is encountered, returning the collected list when non-empty.</description></item>
    ///   <item><description>Otherwise consults the bare key <c>commandLineOptions:&lt;name&gt;</c>:
    ///   null => option absent; a boolean (<c>"true"</c>/<c>"false"</c>, case-insensitive) =>
    ///   empty argument list (zero-arity flag); any other string => single-element array.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// LIMITATION (intentionally exposed for review in Option C): for a multi-value option
    /// defined in <c>testconfig.json</c> as a JSON array, this helper relies on contiguous
    /// indices starting at 0. A user-authored mistake such as a non-array scalar where an
    /// array is expected silently yields a single-element list rather than an arity error;
    /// real arity validation against the option's <c>ArgumentArity</c> is performed by
    /// <see cref="CommandLineOptionsValidator"/> on the <c>parseResult</c> only.
    /// </para>
    /// </remarks>
    internal static bool TryGetCommandLineOptionArguments(this IConfiguration configuration, string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        string baseKey = GetBaseKey(optionName);

        List<string>? collected = null;
        int index = 0;
        while (true)
        {
            string? indexed = configuration[baseKey + PlatformConfigurationConstants.KeyDelimiter + index.ToString(CultureInfo.InvariantCulture)];
            if (indexed is null)
            {
                break;
            }

            collected ??= [];
            collected.Add(indexed);
            index++;
        }

        if (collected is { Count: > 0 })
        {
            arguments = [.. collected];
            return true;
        }

        string? bare = configuration[baseKey];
        if (bare is null)
        {
            arguments = null;
            return false;
        }

        if (bool.TryParse(bare, out bool boolValue))
        {
            if (!boolValue)
            {
                // "commandLineOptions": { "hangdump": false } explicitly disables the option.
                arguments = null;
                return false;
            }

            arguments = [];
            return true;
        }

        arguments = [bare];
        return true;
    }

    private static string GetBaseKey(string optionName)
    {
        // Match CommandLineParseResult.IsOptionSet/TryGetOptionArgumentList: callers may pass
        // either "--foo" or "foo", but storage is keyed by the bare name.
        string trimmed = optionName.Trim(CommandLineParseResult.OptionPrefix);
        return PlatformConfigurationConstants.CommandLineOptionsSectionName + PlatformConfigurationConstants.KeyDelimiter + trimmed;
    }
}
