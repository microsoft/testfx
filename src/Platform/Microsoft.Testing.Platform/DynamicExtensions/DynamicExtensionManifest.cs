// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// A parsed and validated extension manifest.
/// </summary>
internal sealed class DynamicExtensionManifest
{
    public DynamicExtensionManifest(string manifestPath, IReadOnlyList<DynamicExtensionEntry> extensions, IReadOnlyList<string> unknownProperties)
    {
        ManifestPath = manifestPath;
        Extensions = extensions;
        UnknownProperties = unknownProperties;
    }

    /// <summary>
    /// Gets the full path of the manifest file.
    /// </summary>
    public string ManifestPath { get; }

    /// <summary>
    /// Gets the declared extensions, in declaration order.
    /// </summary>
    public IReadOnlyList<DynamicExtensionEntry> Extensions { get; }

    /// <summary>
    /// Gets the properties that the manifest declares but the platform does not understand. They are ignored so
    /// manifests stay forward-compatible, but they are logged so a typo is still discoverable.
    /// </summary>
    public IReadOnlyList<string> UnknownProperties { get; }
}
