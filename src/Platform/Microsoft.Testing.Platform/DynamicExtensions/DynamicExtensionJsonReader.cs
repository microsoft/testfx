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
/// <remarks>
/// This reader rejects duplicate recognized properties. The netstandard2.0 reader cannot: Jsonite materializes
/// objects into a dictionary using indexer assignment, so a repeated key is already collapsed before the reader
/// runs, and detecting it would mean forking the vendored parser that the server-mode JSON-RPC stack also uses.
/// A manifest with duplicate keys is therefore rejected on .NET and silently last-wins on .NET Framework. That
/// asymmetry is deliberate: it is better for the platform that can detect the problem to say so than for both
/// to stay quiet for the sake of matching.
/// </remarks>
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

                // JsonDocument surfaces every occurrence of a duplicated key. Recording it lets the parser
                // reject the manifest rather than quietly keeping one of the two, which would drop extensions
                // somebody deliberately deployed -- the exact failure this feature exists to prevent.
                if (manifest.HasExtensionsProperty)
                {
                    if (!manifest.DuplicateProperties.Contains(property.Name))
                    {
                        manifest.DuplicateProperties.Add(property.Name);
                    }

                    continue;
                }

                manifest.HasExtensionsProperty = true;

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
        HashSet<string> seen = [with(StringComparer.Ordinal)];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            // See the duplicate-key note above. A repeated recognized property is recorded and skipped so the
            // parser can reject it, rather than silently substituting one value for the other.
            if (!seen.Add(property.Name))
            {
                if (IsRecognized(property.Name) && !entry.DuplicateProperties.Contains(property.Name))
                {
                    entry.DuplicateProperties.Add(property.Name);
                }

                continue;
            }

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

    private static bool IsRecognized(string propertyName)
        => propertyName is DynamicExtensionConstants.IdPropertyName
            or DynamicExtensionConstants.DisplayNamePropertyName
            or DynamicExtensionConstants.AssemblyPathPropertyName
            or DynamicExtensionConstants.TypeFullNamePropertyName
            or DynamicExtensionConstants.EnabledPropertyName;

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
