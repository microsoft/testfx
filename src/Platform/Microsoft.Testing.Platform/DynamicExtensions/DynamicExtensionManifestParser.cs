// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Validates the target-framework-neutral output of <see cref="DynamicExtensionJsonReader"/> and turns it into a
/// <see cref="DynamicExtensionManifest"/>.
/// </summary>
/// <remarks>
/// Every validation failure throws. A manifest exists because someone decided that every run must be affected by
/// it, so a run that quietly ignores a broken manifest produces results that look valid but are not the results
/// that were asked for.
/// </remarks>
internal static class DynamicExtensionManifestParser
{
    public static DynamicExtensionManifest Parse(string manifestPath, string content)
    {
        RawExtensionManifest raw = DynamicExtensionJsonReader.Read(manifestPath, content);

        if (!raw.HasExtensionsProperty)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestMissingExtensionsPropertyErrorMessage,
                manifestPath,
                DynamicExtensionConstants.ExtensionsPropertyName));
        }

        if (raw.DuplicateProperties.Count > 0)
        {
            // Keeping one of the two would drop extensions somebody deliberately declared, which is the silent
            // degradation this feature exists to avoid. Report the first offender, as elsewhere.
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestDuplicatePropertyErrorMessage,
                manifestPath,
                raw.DuplicateProperties[0]));
        }

        if (!raw.IsExtensionsPropertyAnArray)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestExtensionsPropertyNotArrayErrorMessage,
                manifestPath,
                DynamicExtensionConstants.ExtensionsPropertyName));
        }

        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        var entries = new List<DynamicExtensionEntry>(raw.Entries.Count);
        List<string> unknownProperties = [.. raw.UnknownProperties];
        for (int index = 0; index < raw.Entries.Count; index++)
        {
            RawExtensionEntry? rawEntry = raw.Entries[index];
            entries.Add(CreateEntry(manifestPath, manifestDirectory, index, rawEntry));

            // Surfaced with the entry index so that a typo such as "enabeld" — which would otherwise silently
            // run an extension the author believed was switched off — is discoverable in the diagnostic log.
            foreach (string unknownProperty in rawEntry!.UnknownProperties)
            {
                unknownProperties.Add($"{DynamicExtensionConstants.ExtensionsPropertyName}[{index.ToString(CultureInfo.InvariantCulture)}].{unknownProperty}");
            }
        }

        return new DynamicExtensionManifest(manifestPath, entries, unknownProperties);
    }

    private static DynamicExtensionEntry CreateEntry(string manifestPath, string manifestDirectory, int index, RawExtensionEntry? raw)
    {
        if (raw is null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestEntryNotObjectErrorMessage,
                manifestPath,
                index,
                DynamicExtensionConstants.ExtensionsPropertyName));
        }

        if (raw.DuplicateProperties.Count > 0)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestEntryDuplicatePropertyErrorMessage,
                manifestPath,
                index,
                raw.DuplicateProperties[0]));
        }

        if (raw.InvalidProperties.Count > 0)
        {
            // Report the first offender only: fixing it usually makes the author re-read the whole entry, and a
            // single precise message is easier to act on than a list.
            string propertyName = raw.InvalidProperties[0];
            string format = propertyName == DynamicExtensionConstants.EnabledPropertyName
                ? PlatformResources.DynamicExtensionManifestEntryPropertyNotBooleanErrorMessage
                : PlatformResources.DynamicExtensionManifestEntryPropertyNotStringErrorMessage;
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, format, manifestPath, index, propertyName));
        }

        string assemblyPath = RequireNonEmpty(raw.AssemblyPath, DynamicExtensionConstants.AssemblyPathPropertyName, manifestPath, index);
        string typeFullName = RequireNonEmpty(raw.TypeFullName, DynamicExtensionConstants.TypeFullNamePropertyName, manifestPath, index);

        string resolvedAssemblyPath;
        try
        {
            resolvedAssemblyPath = Path.GetFullPath(Path.Combine(manifestDirectory, assemblyPath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionManifestEntryInvalidAssemblyPathErrorMessage,
                    manifestPath,
                    index,
                    assemblyPath),
                ex);
        }

        string displayName = RoslynString.IsNullOrWhiteSpace(raw.DisplayName) ? typeFullName : raw.DisplayName!;
        bool hasExplicitId = !RoslynString.IsNullOrWhiteSpace(raw.Id);
        string id = hasExplicitId
            ? raw.Id!
            : $"{resolvedAssemblyPath}|{typeFullName}";

        return new DynamicExtensionEntry(
            manifestPath,
            index,
            id,
            hasExplicitId,
            displayName,
            assemblyPath,
            resolvedAssemblyPath,
            typeFullName,
            raw.Enabled ?? true);
    }

    private static string RequireNonEmpty(string? value, string propertyName, string manifestPath, int index)
        => RoslynString.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionManifestEntryMissingPropertyErrorMessage,
                manifestPath,
                index,
                propertyName))
            : value!;
}
