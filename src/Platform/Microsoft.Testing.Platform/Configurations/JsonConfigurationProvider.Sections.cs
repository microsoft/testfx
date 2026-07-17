// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed partial class JsonConfigurationProvider
    {
        /// <summary>
        /// Returns the immediate (one-level) string entries under <paramref name="sectionName"/>
        /// from the loaded JSON configuration file.
        /// </summary>
        /// <remarks>
        /// The method enforces a strict schema for the section:
        /// <list type="bullet">
        ///   <item><description>The section value must be a JSON object (not a scalar, an array, etc.).</description></item>
        ///   <item><description>Each direct child must be a scalar (string/number/bool/null) — the parser converts non-strings to text.</description></item>
        ///   <item><description>Nested objects or arrays are rejected; an explicit empty object/array entry is also rejected.</description></item>
        /// </list>
        /// Throws <see cref="FormatException"/> with a descriptive message (including the configuration
        /// file path) when the schema is violated.
        /// </remarks>
        internal IReadOnlyList<KeyValuePair<string, string?>> GetSection(string sectionName)
        {
            // Treat unloaded dictionaries as empty independently so a partially-populated state cannot
            // silently mask a malformed section (e.g. a scalar value should still throw even if
            // _propertyToAllChildren happens to be null).
            Dictionary<string, string?> singleValueData = _singleValueData ?? [];
            Dictionary<string, string?> propertyToAllChildren = _propertyToAllChildren ?? [];

            // The JsonConfigurationFileParser flattens scalars into _singleValueData and stores object/array
            // bodies into _propertyToAllChildren. Empty objects and empty arrays are encoded as a null entry
            // in _singleValueData at the property's key (see JsonConfigurationFileParser.SetNullIfElementIsEmpty),
            // and the raw JSON literal "{}" or "[]" is recorded in _propertyToAllChildren.
            //
            // Cases for the section key itself:
            //   - Not present anywhere: section absent -> return empty.
            //   - Present in _singleValueData with non-null value: section is a scalar (e.g. "environmentVariables": "oops") -> reject.
            //   - Present in _singleValueData with null value: section is an empty object {} (return empty) or an empty array [] (reject).
            //   - Not in _singleValueData but in _propertyToAllChildren: section is a non-empty object or array (validated below).
            if (singleValueData.TryGetValue(sectionName, out string? sectionScalar))
            {
                if (sectionScalar is null)
                {
                    // The section value was either an empty object or an empty array. Disambiguate by
                    // looking at the raw JSON text recorded in _propertyToAllChildren: arrays must be
                    // rejected even when empty because the schema requires a JSON object.
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

            // The parser stores the raw JSON of object/array values under the property's key. An array
            // section (e.g. "environmentVariables": [...]) is not a valid object section and must be rejected.
            // We detect arrays by the first non-whitespace character of the raw text.
            if (sectionRaw is not null && StartsWithChar(sectionRaw, '['))
            {
                ThrowSectionMustBeAnObject(sectionName);
            }

            string sectionPrefix = sectionName + PlatformConfigurationConstants.KeyDelimiter;
            List<KeyValuePair<string, string?>> entries = [];

            foreach (KeyValuePair<string, string?> kvp in singleValueData)
            {
                if (!kvp.Key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string remainder = kvp.Key.Substring(sectionPrefix.Length);

                // Reject nested objects/arrays: their leaves produce keys like "environmentVariables:FOO:BAR"
                // or "environmentVariables:FOO:0" (array index). We deliberately allow an empty remainder
                // (i.e. an empty child key such as {"environmentVariables": {"": "x"}}) through so the
                // consumer can run its own name validation (and surface a dedicated error message).
                if (remainder.IndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal) >= 0)
                {
                    throw new FormatException(string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.JsonConfigurationSectionEntryMustBeScalarErrorMessage,
                        kvp.Key,
                        sectionName,
                        ConfigurationFile ?? "<unknown>"));
                }

                // A null value at this level (and not a scalar JSON null, which serializes to "") means
                // the entry was an empty object/array such as "FOO": {} or "FOO": []. Reject it because the
                // schema requires scalar entries only.
                if (kvp.Value is null)
                {
                    throw new FormatException(string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.JsonConfigurationSectionEntryMustBeScalarErrorMessage,
                        kvp.Key,
                        sectionName,
                        ConfigurationFile ?? "<unknown>"));
                }

                entries.Add(new KeyValuePair<string, string?>(remainder, kvp.Value));
            }

            return entries;
        }

        [DoesNotReturn]
        private void ThrowSectionMustBeAnObject(string sectionName)
            => throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.JsonConfigurationSectionMustBeAnObjectErrorMessage,
                sectionName,
                ConfigurationFile ?? "<unknown>"));
    }
}
