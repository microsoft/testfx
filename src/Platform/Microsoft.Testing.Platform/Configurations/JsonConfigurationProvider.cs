// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed class JsonConfigurationProvider(
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IFileSystem fileSystem,
        CommandLineParseResult commandLineParseResult,
        ILogger? logger) : IConfigurationProvider
    {
        private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
        private readonly IFileSystem _fileSystem = fileSystem;
        private readonly CommandLineParseResult _commandLineParseResult = commandLineParseResult;
        private readonly ILogger? _logger = logger;
        private Dictionary<string, string?>? _propertyToAllChildren;
        private Dictionary<string, string?>? _singleValueData;

        public string? ConfigurationFile { get; private set; }

        private Task LogInformationAsync(string message)
            => _logger?.LogInformationAsync(message) ?? Task.CompletedTask;

        private Task LogDebugAsync(string message)
            => _logger?.LogDebugAsync(message) ?? Task.CompletedTask;

        private Task LogErrorAsync(string message, Exception exception)
            => _logger?.LogErrorAsync(message, exception) ?? Task.CompletedTask;

        public async Task LoadAsync()
        {
            string configFileName;
            if (_commandLineParseResult.TryGetOptionArgumentList(PlatformCommandLineProvider.ConfigFileOptionKey, out string[]? configOptions))
            {
                configFileName = configOptions[0];
                if (!_fileSystem.ExistFile(configFileName))
                {
                    try
                    {
                        // Get the full path for better error messages.
                        // As this is only for the purpose of throwing an exception, ignore any exceptions during the GetFullPath call.
                        configFileName = Path.GetFullPath(configFileName);
                    }
                    catch (Exception ex)
                    {
                        // Best-effort path resolution; surface the failure at Debug so logs explain why the
                        // error message may show a relative path instead of an absolute one.
                        await LogDebugAsync($"Path.GetFullPath('{configFileName}') failed while preparing FileNotFoundException: {ex.GetType().FullName}: {ex.Message}").ConfigureAwait(false);
                    }

                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConfigurationFileNotFound, configFileName), configFileName);
                }
            }
            else
            {
                configFileName = _testApplicationModuleInfo.TryGetCurrentTestApplicationFullPath() is { } fullPath
                    ? $"{Path.Combine(
                        Path.GetDirectoryName(fullPath)!,
                        Path.GetFileNameWithoutExtension(fullPath))}{PlatformConfigurationConstants.PlatformConfigSuffixFileName}"
                    : $"{_testApplicationModuleInfo.TryGetAssemblyName()}{PlatformConfigurationConstants.PlatformConfigSuffixFileName}";

                if (!_fileSystem.ExistFile(configFileName))
                {
                    await LogDebugAsync($"Default JSON config file '{configFileName}' not found; skipping load.").ConfigureAwait(false);
                    return;
                }
            }

            await LogInformationAsync($"Config file '{configFileName}' loaded.").ConfigureAwait(false);

            ConfigurationFile = configFileName;

            using IFileStream fileStream = _fileSystem.NewFileStream(configFileName, FileMode.Open, FileAccess.Read);
            try
            {
                (_singleValueData, _propertyToAllChildren) = JsonConfigurationFileParser.Parse(fileStream.Stream);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Failed to parse configuration file '{configFileName}'", ex).ConfigureAwait(false);
                throw;
            }
        }

        public bool TryGet(string key, out string? value)
        {
            value = null;
            return (_singleValueData != null && _singleValueData.TryGetValue(key, out value)) || (_propertyToAllChildren != null && _propertyToAllChildren.TryGetValue(key, out value));
        }

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

        [DoesNotReturn]
        private void ThrowSectionMustBeAnObject(string sectionName)
            => throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.JsonConfigurationSectionMustBeAnObjectErrorMessage,
                sectionName,
                ConfigurationFile ?? "<unknown>"));

        [DoesNotReturn]
        private void ThrowEntryMustBeScalarOrArray(string fullKey, string sectionName)
            => throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.JsonCommandLineOptionsEntryMustBeScalarOrArrayErrorMessage,
                fullKey,
                sectionName,
                ConfigurationFile ?? "<unknown>"));

        private struct OptionBuilder
        {
            public string? Scalar { get; set; }

            public SortedList<int, string>? Indexed { get; set; }
        }
    }
}
