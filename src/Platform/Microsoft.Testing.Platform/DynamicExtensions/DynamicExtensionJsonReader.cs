// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP

using System.Text.Json;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Reads an extension manifest into the target-framework-neutral <see cref="RawExtensionManifest"/> shape using
/// <c>System.Text.Json</c>. The netstandard2.0 asset uses the Jsonite-based reader instead.
/// </summary>
internal static class DynamicExtensionJsonReader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RawExtensionManifest Read(string manifestPath, string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.DynamicExtensionManifestInvalidJsonErrorMessage, manifestPath, ex.Message),
                ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionManifestRootNotObjectErrorMessage,
                    manifestPath,
                    document.RootElement.ValueKind));
            }

            RawExtensionManifest manifest = new();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, DynamicExtensionConstants.ExtensionsPropertyName, StringComparison.Ordinal))
                {
                    manifest.UnknownProperties.Add(property.Name);
                    continue;
                }

                // JsonDocument surfaces every occurrence of a duplicated key, whereas the Jsonite-based reader
                // used on netstandard2.0 keeps only the last. Reset here so both readers are last-wins and a
                // pathological manifest cannot behave differently per target framework.
                manifest.HasExtensionsProperty = true;
                manifest.IsExtensionsPropertyAnArray = false;
                manifest.Entries.Clear();

                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    // Leave IsExtensionsPropertyAnArray false; the parser owns the error message.
                    continue;
                }

                manifest.IsExtensionsPropertyAnArray = true;
                foreach (JsonElement element in property.Value.EnumerateArray())
                {
                    manifest.Entries.Add(element.ValueKind == JsonValueKind.Object ? ReadEntry(element) : null);
                }
            }

            return manifest;
        }
    }

    private static RawExtensionEntry ReadEntry(JsonElement element)
    {
        RawExtensionEntry entry = new();
        foreach (JsonProperty property in element.EnumerateObject())
        {
            // See the duplicate-key note above: a repeated property must end up last-wins on both readers, so
            // any earlier "wrong type" verdict for the same name is discarded first.
            entry.InvalidProperties.Remove(property.Name);

            switch (property.Name)
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
                    entry.Enabled = property.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => RecordInvalid<bool?>(entry, property.Name),
                    };
                    break;

                default:
                    if (!entry.UnknownProperties.Contains(property.Name))
                    {
                        entry.UnknownProperties.Add(property.Name);
                    }

                    break;
            }
        }

        return entry;
    }

    private static string? ReadString(RawExtensionEntry entry, JsonProperty property)
        => property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()
            : RecordInvalid<string?>(entry, property.Name);

    private static T? RecordInvalid<T>(RawExtensionEntry entry, string propertyName)
    {
        entry.InvalidProperties.Add(propertyName);
        return default;
    }
}

#endif
