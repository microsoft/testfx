// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed partial class JsonConfigurationProvider
    {
        /// <summary>
        /// Rewrites scalar <c>commandLineOptions:&lt;name&gt;</c> entries to the indexed shape
        /// (<c>commandLineOptions:&lt;name&gt;:0</c>) for options registered with
        /// <see cref="ArgumentArity.Min"/> &gt;= 1, using <paramref name="optionByName"/> as the
        /// option registry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The CLI-backed configuration provider stores zero-arity flags at the bare option key and
        /// arg-bearing options under indexed keys (one per argument). The JSON provider cannot
        /// distinguish these shapes at parse time because the user freely writes either
        /// <c>"foo": "value"</c> (scalar) or <c>"foo": ["value"]</c> (array). For arg-bearing
        /// options that always require at least one argument, a scalar value is always the first
        /// argument — never a presence marker. Storing it under the indexed key normalizes the
        /// shape so both <see cref="EnumerateCommandLineOptions"/> and
        /// <see cref="AggregatedConfiguration.TryGetCommandLineOptionFromProviders"/> see one
        /// consistent representation.
        /// </para>
        /// <para>
        /// In particular, this fixes the JSON scalar/bool ambiguity from #6349/#8830: a value like
        /// <c>"my-option": "true"</c> for an arg-bearing option used to be misinterpreted as a
        /// presence marker (zero arguments) instead of being passed as the first argument value.
        /// </para>
        /// <para>
        /// Optional-arg options (<c>Min == 0 &amp;&amp; Max &gt;= 1</c>) are left untouched because
        /// either interpretation (presence vs. scalar argument) is semantically valid; users who
        /// need to disambiguate should use the explicit array form.
        /// </para>
        /// </remarks>
        internal void NormalizeCommandLineOptionScalars(IReadOnlyDictionary<string, CommandLineOption> optionByName)
        {
            if (_singleValueData is null || _singleValueData.Count == 0)
            {
                return;
            }

            const string sectionName = PlatformConfigurationConstants.CommandLineOptionsSectionName;
            string sectionPrefix = sectionName + PlatformConfigurationConstants.KeyDelimiter;

            List<(string OldKey, string NewKey, string Value)>? rewrites = null;

            foreach (KeyValuePair<string, string?> kvp in _singleValueData)
            {
                if (!kvp.Key.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Bare key only: "commandLineOptions:<name>" with no further colon. Indexed entries
                // already use the canonical shape and are not subject to the scalar/bool ambiguity.
                string remainder = kvp.Key.Substring(sectionPrefix.Length);
                if (remainder.Length == 0
                    || remainder.IndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                // A null value at the bare key represents an empty object/array; the schema
                // validator in EnumerateCommandLineOptions will reject it later. Skip here so we
                // never promote a placeholder to an indexed slot.
                if (kvp.Value is null)
                {
                    continue;
                }

                if (!optionByName.TryGetValue(remainder, out CommandLineOption? option))
                {
                    // Unknown option name. Leave alone so the unknown-option validator pass can
                    // surface a clear error referencing testconfig.json.
                    continue;
                }

                if (option.Arity.Min < 1)
                {
                    continue;
                }

                string newKey = kvp.Key + PlatformConfigurationConstants.KeyDelimiter + "0";

                // Defensive: if the indexed slot already exists, the JSON contained both a scalar
                // and an array entry for the same option, which the schema validator will flag as
                // malformed. Don't clobber.
                if (_singleValueData.ContainsKey(newKey))
                {
                    continue;
                }

                (rewrites ??= []).Add((kvp.Key, newKey, kvp.Value));
            }

            if (rewrites is null)
            {
                return;
            }

            foreach ((string oldKey, string newKey, string value) in rewrites)
            {
                _singleValueData.Remove(oldKey);
                _singleValueData[newKey] = value;
            }
        }

        private static bool StartsWithChar(string text, char c)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return text[i] == c;
                }
            }

            return false;
        }

        private struct OptionBuilder
        {
            public string? Scalar { get; set; }

            public SortedList<int, string>? Indexed { get; set; }
        }
    }
}
