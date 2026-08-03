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
        try
        {
            document = Json.Deserialize(content, Settings);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.DynamicExtensionManifestInvalidJsonErrorMessage, manifestPath, ex.Message),
                ex);
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
}

#endif
