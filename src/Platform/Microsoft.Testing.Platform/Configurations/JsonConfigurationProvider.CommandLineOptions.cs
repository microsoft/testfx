// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed partial class JsonConfigurationProvider
    {
        /// <summary>
        /// Enumerates the <c>commandLineOptions</c> section of the loaded testconfig.json file as a
        /// list of typed entries, applying the strict schema documented at
        /// <see cref="PlatformConfigurationConstants.CommandLineOptionsSectionName"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Supported per-option JSON shapes:
        /// <list type="bullet">
        ///   <item><description><c>"foo": "bar"</c> — scalar argument (single value).</description></item>
        ///   <item><description><c>"foo": true</c> — presence marker, equivalent to <c>--foo</c> on the CLI.</description></item>
        ///   <item><description><c>"foo": false</c> — explicit disable; surfaced via <see cref="JsonCommandLineOptionEntry.IsDisabled"/>.</description></item>
        ///   <item><description><c>"foo": ["a", "b"]</c> — multi-value argument list.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Rejected shapes (throw <see cref="FormatException"/>):
        /// <list type="bullet">
        ///   <item><description>Nested objects like <c>"foo": { "bar": "x" }</c>.</description></item>
        ///   <item><description>Array of objects like <c>"foo": [ { "bar": "x" } ]</c>.</description></item>
        ///   <item><description>Mixing scalar and indexed entries for the same option name (defensive guard; cannot happen from JSON input but possible if a custom provider mutates the underlying maps).</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Empty containers (<c>"foo": []</c>) are skipped (treated as absent). Empty objects (<c>"foo": {}</c>)
        /// are rejected because that shape is otherwise indistinguishable from a malformed nested entry.
        /// </para>
        /// </remarks>
        internal IReadOnlyList<JsonCommandLineOptionEntry> EnumerateCommandLineOptions()
        {
            const string sectionName = PlatformConfigurationConstants.CommandLineOptionsSectionName;

            Dictionary<string, string?> singleValueData = _singleValueData ?? [];
            Dictionary<string, string?> propertyToAllChildren = _propertyToAllChildren ?? [];

            if (singleValueData.TryGetValue(sectionName, out string? sectionScalar))
            {
                if (sectionScalar is null)
                {
                    if (propertyToAllChildren.TryGetValue(sectionName, out string? emptyRaw)
                        && emptyRaw is not null
                        && StartsWithChar(emptyRaw, '['))
                    {
                        ThrowSectionMustBeAnObject(sectionName);
                    }

                    return [];
                }

                ThrowSectionMustBeAnObject(sectionName);
            }

            if (!propertyToAllChildren.TryGetValue(sectionName, out string? sectionRaw))
            {
                return [];
            }

            if (sectionRaw is not null && StartsWithChar(sectionRaw, '['))
            {
                ThrowSectionMustBeAnObject(sectionName);
            }

            string sectionPrefix = sectionName + PlatformConfigurationConstants.KeyDelimiter;

            // Collect scalar/indexed data per option name. JSON storage is case-insensitive elsewhere in
            // the platform (see TryGetCommandLineOptionFromProviders), so we use OrdinalIgnoreCase here
            // to keep the grouping consistent (and so options like "Timeout" vs "timeout" are treated as
            // one entry rather than silently producing two separate validations).
            var byOption = new Dictionary<string, OptionBuilder>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string?> kvp in singleValueData)
            {
                if (!kvp.Key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string remainder = kvp.Key.Substring(sectionPrefix.Length);

                int firstColon = remainder.IndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal);
                string optionName;
                string? subKey;

                if (firstColon < 0)
                {
                    optionName = remainder;
                    subKey = null;
                }
                else
                {
                    optionName = remainder.Substring(0, firstColon);
                    subKey = remainder.Substring(firstColon + 1);
                }

                if (!byOption.TryGetValue(optionName, out OptionBuilder builder))
                {
                    builder = default;
                }

                if (subKey is null)
                {
                    if (kvp.Value is null)
                    {
                        // Empty container at the option key. The parser cannot tell the difference between
                        // {} and [] at write time, so we disambiguate via the raw text recorded for object
                        // values. Empty arrays are silently dropped (matches runtime behavior — no entries
                        // means "absent"); empty objects are rejected as the user almost certainly meant a
                        // typo and the entire commandLineOptions section is supposed to hold leaf values.
                        if (propertyToAllChildren.TryGetValue(kvp.Key, out string? rawEntry)
                            && rawEntry is not null
                            && StartsWithChar(rawEntry, '{'))
                        {
                            ThrowEntryMustBeScalarOrArray(kvp.Key, sectionName);
                        }

                        // Empty array — treat as absent.
                        continue;
                    }

                    if (builder.Scalar is not null)
                    {
                        // Two scalars at the same key cannot happen through valid JSON (duplicate key
                        // would have failed earlier in JsonConfigurationFileParser). Treat this as a
                        // schema violation if it ever occurs.
                        ThrowEntryMustBeScalarOrArray(kvp.Key, sectionName);
                    }

                    builder.Scalar = kvp.Value;
                }
                else
                {
                    // Indexed entry. The sub-key MUST be a single non-negative integer; anything else
                    // (a name, or a name plus more colons, or an integer plus more colons) implies a
                    // nested object or an array of objects, neither of which is supported.
                    int nestedColon = subKey.IndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal);
                    if (nestedColon >= 0)
                    {
                        ThrowEntryMustBeScalarOrArray(kvp.Key, sectionName);
                    }

                    if (!int.TryParse(subKey, NumberStyles.None, CultureInfo.InvariantCulture, out int idx))
                    {
                        ThrowEntryMustBeScalarOrArray(kvp.Key, sectionName);
                    }

                    if (kvp.Value is null)
                    {
                        // An indexed slot was an empty object/array — reject (arrays must hold scalars).
                        ThrowEntryMustBeScalarOrArray(kvp.Key, sectionName);
                    }

                    builder.Indexed ??= [];
                    builder.Indexed[idx] = kvp.Value!;
                }

                byOption[optionName] = builder;
            }

            List<JsonCommandLineOptionEntry> result = [];
            foreach (KeyValuePair<string, OptionBuilder> entry in byOption)
            {
                string optionName = entry.Key;
                OptionBuilder builder = entry.Value;

                if (builder.Scalar is not null && builder.Indexed is { Count: > 0 })
                {
                    // Defensive: cannot happen through valid JSON input.
                    ThrowEntryMustBeScalarOrArray(sectionPrefix + optionName, sectionName);
                }

                if (builder.Scalar is not null)
                {
                    if (bool.TryParse(builder.Scalar, out bool boolValue))
                    {
                        result.Add(new JsonCommandLineOptionEntry(optionName, [], isDisabled: !boolValue));
                    }
                    else
                    {
                        result.Add(new JsonCommandLineOptionEntry(optionName, [builder.Scalar], isDisabled: false));
                    }
                }
                else if (builder.Indexed is { Count: > 0 } indexed)
                {
                    // JSON arrays always serialize to contiguous indices starting at 0, but verify
                    // defensively so a malformed in-memory mutation cannot silently truncate.
                    string[] args = new string[indexed.Count];
                    int expected = 0;
                    foreach (KeyValuePair<int, string> kvp in indexed)
                    {
                        if (kvp.Key != expected)
                        {
                            ThrowEntryMustBeScalarOrArray(sectionPrefix + optionName, sectionName);
                        }

                        args[expected++] = kvp.Value;
                    }

                    result.Add(new JsonCommandLineOptionEntry(optionName, args, isDisabled: false));
                }
            }

            return result;
        }

        [DoesNotReturn]
        private void ThrowEntryMustBeScalarOrArray(string fullKey, string sectionName)
        {
            // Callers pass the full flattened configuration key (e.g. "commandLineOptions:foo" or
            // "commandLineOptions:foo:0"). The resource message reads "The entry '{0}' under section
            // '{1}'..." so strip the leading "<sectionName>:" prefix to keep {0} as the entry name
            // relative to the section (e.g. "foo" or "foo:0") and avoid the redundant/confusing
            // "The entry 'commandLineOptions:foo' under section 'commandLineOptions'..." rendering.
            string entryName = fullKey;
            string prefix = sectionName + PlatformConfigurationConstants.KeyDelimiter;
            if (entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                entryName = entryName.Substring(prefix.Length);
            }

            throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.JsonCommandLineOptionsEntryMustBeScalarOrArrayErrorMessage,
                entryName,
                sectionName,
                ConfigurationFile ?? "<unknown>"));
        }
    }
}
