// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP

using Jsonite;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Reads an extension manifest into the target-framework-neutral <see cref="RawExtensionManifest"/> shape using
/// the bundled Jsonite parser, because <c>System.Text.Json</c> is not available on the netstandard2.0 asset.
/// </summary>
/// <remarks>
/// Unlike the .NET reader, this one cannot detect duplicate keys: Jsonite fills a
/// <see cref="Dictionary{TKey, TValue}"/> using indexer assignment, so a repeated key has already overwritten
/// the earlier one by the time <see cref="Read"/> enumerates it. Making it detectable would mean forking the
/// vendored parser that the server-mode JSON-RPC stack also depends on, which is not worth it for a
/// pathological manifest. <see cref="RawExtensionManifest.DuplicateProperties"/> therefore stays empty here.
/// </remarks>
internal static class DynamicExtensionJsonReader
{
    private static readonly JsonSettings Settings = new()
    {
        AllowComments = true,
        AllowTrailingCommas = true,
    };

    public static RawExtensionManifest Read(string manifestPath, string content)
    {
        object? document;
        CountingStringReader reader = new(content);
        try
        {
            document = Json.Deserialize(reader, Settings);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.DynamicExtensionManifestInvalidJsonErrorMessage, manifestPath, ex.Message),
                ex);
        }

        // Jsonite returns as soon as it has parsed the root value and never checks for end of input, so
        // '{ ... } garbage' would be accepted here while System.Text.Json rejects it on the .NET asset. Left
        // alone that would make a malformed manifest run on .NET Framework only, breaking the cross-target
        // validation the two readers are supposed to share.
        if (!reader.ReachedEnd)
        {
            // At most one character is held as lookahead, so anything from the previous position onwards is
            // content the parser did not consume as part of the root value.
            string trailing = content.Substring(reader.Position - 1);
            if (trailing.Trim().Length > 0)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionManifestInvalidJsonErrorMessage,
                    manifestPath,
                    PlatformResources.DynamicExtensionManifestTrailingContentErrorMessage));
            }
        }

        if (document is not JsonObject root)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestRootNotObjectErrorMessage,
                manifestPath,
                DescribeKind(document)));
        }

        RawExtensionManifest manifest = new();
        foreach (KeyValuePair<string, object?> property in root)
        {
            if (!string.Equals(property.Key, DynamicExtensionConstants.ExtensionsPropertyName, StringComparison.Ordinal))
            {
                manifest.UnknownProperties.Add(property.Key);
                continue;
            }

            manifest.HasExtensionsProperty = true;
            if (property.Value is not JsonArray array)
            {
                // Leave IsExtensionsPropertyAnArray false; the parser owns the error message.
                continue;
            }

            manifest.IsExtensionsPropertyAnArray = true;
            foreach (object? element in array)
            {
                manifest.Entries.Add(element is JsonObject entry ? ReadEntry(entry) : null);
            }
        }

        return manifest;
    }

    private static RawExtensionEntry ReadEntry(JsonObject element)
    {
        RawExtensionEntry entry = new();
        foreach (KeyValuePair<string, object?> property in element)
        {
            switch (property.Key)
            {
                case DynamicExtensionConstants.IdPropertyName:
                    entry.Id = ReadString(entry, property);
                    break;

                case DynamicExtensionConstants.DisplayNamePropertyName:
                    entry.DisplayName = ReadString(entry, property);
                    break;

                case DynamicExtensionConstants.AssemblyPathPropertyName:
                    entry.AssemblyPath = ReadString(entry, property);
                    break;

                case DynamicExtensionConstants.TypeFullNamePropertyName:
                    entry.TypeFullName = ReadString(entry, property);
                    break;

                case DynamicExtensionConstants.EnabledPropertyName:
                    entry.Enabled = property.Value is bool enabled ? enabled : RecordInvalid<bool?>(entry, property.Key);
                    break;

                default:
                    entry.UnknownProperties.Add(property.Key);
                    break;
            }
        }

        return entry;
    }

    private static string? ReadString(RawExtensionEntry entry, KeyValuePair<string, object?> property)
        => property.Value is string value ? value : RecordInvalid<string?>(entry, property.Key);

    private static T? RecordInvalid<T>(RawExtensionEntry entry, string propertyName)
    {
        entry.InvalidProperties.Add(propertyName);
        return default;
    }

    private static string DescribeKind(object? value)
        => value switch
        {
            null => "Null",
            JsonArray => "Array",
            string => "String",
            // Match the System.Text.Json JsonValueKind names the .NET reader produces, so the same manifest
            // yields the same error text on every target framework.
            bool boolean => boolean ? "True" : "False",
            _ => "Number",
        };

    /// <summary>
    /// A <see cref="StringReader"/> replacement that reports how much of the input the parser consumed, which
    /// is what lets <see cref="Read"/> detect content trailing the root value. Jsonite itself offers no way to
    /// ask, and it keeps at most one character of lookahead.
    /// </summary>
    private sealed class CountingStringReader : TextReader
    {
        private readonly string _content;

        public CountingStringReader(string content) => _content = content;

        /// <summary>
        /// Gets the number of characters handed to the parser.
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the parser read past the end of the input, which means it consumed
        /// the whole string rather than stopping on a lookahead character.
        /// </summary>
        public bool ReachedEnd { get; private set; }

        public override int Peek()
            => Position < _content.Length ? _content[Position] : -1;

        public override int Read()
        {
            if (Position < _content.Length)
            {
                return _content[Position++];
            }

            ReachedEnd = true;
            return -1;
        }
    }
}

#endif
