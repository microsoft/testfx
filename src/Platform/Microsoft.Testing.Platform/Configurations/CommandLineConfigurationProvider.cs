// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// <see cref="IConfigurationProvider"/> that exposes the parsed CLI options under the
/// <c>commandLineOptions:*</c> section.
/// </summary>
/// <remarks>
/// <para>Storage layout (mirrors the layout that <c>JsonConfigurationFileParser</c> produces for
/// the <c>commandLineOptions</c> JSON section, so the same lookup helpers serve both sources):</para>
/// <list type="bullet">
///   <item><description><b>Zero-arity option present</b> (e.g. <c>--hangdump</c>): stores
///   <c>commandLineOptions:&lt;name&gt;</c> = <c>"true"</c> as a presence marker.</description></item>
///   <item><description><b>Single- or multi-value option</b> (e.g. <c>--hangdump-timeout 5m</c> or
///   <c>--filter-uid a b</c>): stores one entry per argument under
///   <c>commandLineOptions:&lt;name&gt;:&lt;index&gt;</c>.</description></item>
/// </list>
/// <para>
/// Option names are stored without the leading <c>--</c> prefix and compared case-insensitively
/// to match <see cref="CommandLineParseResult.IsOptionSet(string)"/> semantics.
/// </para>
/// <para>
/// An option that appears multiple times on the command line is flattened into a single sequence
/// of arguments (matching <see cref="CommandLineParseResult.TryGetOptionArgumentList"/>), so the
/// resulting key/value layout is identical to what a JSON array under the same option name would
/// produce.
/// </para>
/// </remarks>
internal sealed class CommandLineConfigurationProvider : IConfigurationProvider
{
    private readonly Dictionary<string, string?> _data = [with(StringComparer.OrdinalIgnoreCase)];

    public CommandLineConfigurationProvider(CommandLineParseResult commandLineParseResult)
    {
        string sectionPrefix = PlatformConfigurationConstants.CommandLineOptionsSectionName + PlatformConfigurationConstants.KeyDelimiter;

        // Group by option name so that --filter-uid a --filter-uid b is exposed as a single
        // contiguous indexed list (commandLineOptions:filter-uid:0=a, :1=b), matching the existing
        // TryGetOptionArgumentList flattening behavior.
        foreach (IGrouping<string, CommandLineParseOption> grouping in commandLineParseResult.Options.GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            string baseKey = sectionPrefix + grouping.Key;
            int index = 0;
            foreach (CommandLineParseOption option in grouping)
            {
                foreach (string argument in option.Arguments)
                {
                    _data[baseKey + PlatformConfigurationConstants.KeyDelimiter + index.ToString(CultureInfo.InvariantCulture)] = argument;
                    index++;
                }
            }

            // Zero-arity flag: surface a boolean presence marker under the bare key so that
            // IsCommandLineOptionSet returns true. Multi-value options intentionally do NOT
            // populate the bare key (the indexed entries above represent the value).
            if (index == 0)
            {
                _data[baseKey] = bool.TrueString;
            }
        }
    }

    public Task LoadAsync() => Task.CompletedTask;

    public bool TryGet(string key, out string? value) => _data.TryGetValue(key, out value);
}
