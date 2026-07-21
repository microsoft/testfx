// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

internal static class ArtifactPostProcessingHandshakeProperties
{
    public static IReadOnlyDictionary<byte, string>? Create(IEnumerable<IArtifactPostProcessor> processors)
    {
        string kinds = string.Join(
            ";",
            processors.SelectMany(processor => processor.SupportedKinds)
                .Where(kind => !RoslynString.IsNullOrWhiteSpace(kind))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(kind => kind, StringComparer.Ordinal));
        string extensions = string.Join(
            ";",
            processors.SelectMany(processor => processor.SupportedFileExtensionsFallback)
                .Where(extension => !RoslynString.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(extension => extension, StringComparer.Ordinal));

        if (kinds.Length == 0 && extensions.Length == 0)
        {
            return null;
        }

        var properties = new Dictionary<byte, string>();
        if (kinds.Length > 0)
        {
            properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds] = kinds;
        }

        if (extensions.Length > 0)
        {
            properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy] = extensions;
        }

        return properties;
    }
}
