// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// A single validated extension declaration coming from an extension manifest.
/// </summary>
internal sealed class DynamicExtensionEntry
{
    public DynamicExtensionEntry(
        string manifestPath,
        int index,
        string id,
        bool hasExplicitId,
        string displayName,
        string assemblyPath,
        string resolvedAssemblyPath,
        string typeFullName,
        bool isEnabled)
    {
        ManifestPath = manifestPath;
        Index = index;
        Id = id;
        HasExplicitId = hasExplicitId;
        DisplayName = displayName;
        AssemblyPath = assemblyPath;
        ResolvedAssemblyPath = resolvedAssemblyPath;
        TypeFullName = typeFullName;
        IsEnabled = isEnabled;

        // Explicit ids are opaque and case-insensitive; generated ones carry a path whose case may be
        // significant. Normalising both into one ordinal key keeps a single dictionary correct for both.
        DeduplicationKey = hasExplicitId
            ? "id:" + id.ToUpperInvariant()
            : "identity:" + (DynamicExtensionConstants.PathComparison == StringComparison.OrdinalIgnoreCase
                ? resolvedAssemblyPath.ToUpperInvariant()
                : resolvedAssemblyPath) + "|" + typeFullName;
    }

    /// <summary>
    /// Gets the full path of the manifest that declared this entry. Used in diagnostics so that a failure can
    /// always be traced back to the file that has to be fixed.
    /// </summary>
    public string ManifestPath { get; }

    /// <summary>
    /// Gets the zero-based index of this entry inside the manifest's <c>extensions</c> array.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the stable identifier used to de-duplicate the same extension declared by several manifests.
    /// Defaults to the resolved assembly path combined with the type name when the manifest omits it.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the key used to de-duplicate this entry against others.
    /// </summary>
    /// <remarks>
    /// An explicit <c>id</c> is an opaque token the author chose, so it is matched case-insensitively. A
    /// generated identity is not: it embeds the resolved assembly path, and on a case-sensitive file system
    /// <c>/app/Foo.dll</c> and <c>/app/foo.dll</c> are two different extensions that must not collide. Folding
    /// both into one case-insensitive dictionary would report those as a conflicting duplicate id.
    /// </remarks>
    public string DeduplicationKey { get; }

    /// <summary>
    /// Gets a value indicating whether the manifest declared an explicit <c>id</c>.
    /// </summary>
    public bool HasExplicitId { get; }

    /// <summary>
    /// Gets the human-readable name used in diagnostics. Defaults to the type full name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the assembly path exactly as written in the manifest. Kept for diagnostics so error messages can
    /// echo what the author wrote as well as what it resolved to.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the absolute path of the extension assembly, resolved against the manifest's directory.
    /// </summary>
    public string ResolvedAssemblyPath { get; }

    /// <summary>
    /// Gets the full name of the type declaring the static hook method.
    /// </summary>
    public string TypeFullName { get; }

    /// <summary>
    /// Gets a value indicating whether this extension should be loaded.
    /// </summary>
    public bool IsEnabled { get; }
}
