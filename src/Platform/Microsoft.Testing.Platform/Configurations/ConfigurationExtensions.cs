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
    /// Used by <see cref="CommandLineHandler.IsOptionSet(string)"/> so JSON-sourced options are
    /// transparently visible to all <see cref="ICommandLineOptions"/> consumers (issue #6349).
    /// </para>
    /// <para>
    /// Resolution is performed at the option granularity by walking the configuration providers
    /// in registration order via <see cref="AggregatedConfiguration.TryGetCommandLineOptionFromProviders"/>:
    /// the first provider that has any data for the option wins outright and its sibling entries
    /// are not merged with later providers. This guarantees that, e.g., an explicit zero-arity
    /// <c>--list-tests</c> on the CLI is not silently overridden by an indexed JSON array under
    /// the same option.
    /// </para>
    /// <para>
    /// When the <see cref="IConfiguration"/> instance is not an <see cref="AggregatedConfiguration"/>
    /// (test mocks etc.), falls back to a merged-view lookup via the <c>IConfiguration[key]</c>
    /// indexer. In that path the providers cannot be walked, so precedence is per-key rather than
    /// per-option.
    /// </para>
    /// <para>
    /// Note: a JSON bare scalar that happens to be the string <c>"true"</c> or
    /// <c>"false"</c> for an option that takes arguments is ambiguous — it is interpreted here as
    /// a presence/absence marker rather than as the argument value. Authors should use the array
    /// form (<c>["true"]</c>, <c>["false"]</c>) when the literal string is intended.
    /// </para>
    /// </remarks>
    internal static bool IsCommandLineOptionSet(this IConfiguration configuration, string optionName)
    {
        if (configuration is AggregatedConfiguration aggregated)
        {
            return aggregated.TryGetCommandLineOptionFromProviders(optionName, out bool isSet, out _) && isSet;
        }

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
    /// so JSON-sourced options are transparently visible to all <see cref="ICommandLineOptions"/>
    /// consumers (issue #6349).
    /// </para>
    /// <para>
    /// Resolution mirrors <see cref="IsCommandLineOptionSet"/>: providers are walked in
    /// registration order and the first one with data for the option wins entirely, so
    /// CLI/JSON values for the same option never interleave.
    /// </para>
    /// <para>
    /// Behavior at the winning provider is intentionally a superset of
    /// <see cref="CommandLineParseResult.TryGetOptionArgumentList(string, out string[])"/> so
    /// existing consumers continue to work:
    /// <list type="bullet">
    ///   <item><description>Walks <c>commandLineOptions:&lt;name&gt;:0</c>, <c>:1</c>, ... until a
    ///   gap is encountered, returning the collected list when non-empty.</description></item>
    ///   <item><description>Otherwise consults the bare key <c>commandLineOptions:&lt;name&gt;</c>:
    ///   null =&gt; option absent at this provider; a boolean (<c>"true"</c>/<c>"false"</c>,
    ///   case-insensitive) =&gt; presence marker (true =&gt; set with zero args; false =&gt; explicitly
    ///   not set); any other string =&gt; single-element array.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// JSON-sourced entries are subject to arity, unknown-option, and per-argument validation
    /// in <see cref="CommandLineOptionsValidator"/> (the validator is given the same JSON view
    /// via <see cref="AggregatedConfiguration.EnumerateJsonCommandLineOptions"/>), so a JSON
    /// typo or bad value fails at startup with the same diagnostics as on the CLI.
    /// </para>
    /// </remarks>
    internal static bool TryGetCommandLineOptionArguments(this IConfiguration configuration, string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        if (configuration is AggregatedConfiguration aggregated)
        {
            if (aggregated.TryGetCommandLineOptionFromProviders(optionName, out bool isSet, out string[] args) && isSet)
            {
                arguments = args;
                return true;
            }

            arguments = null;
            return false;
        }

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
