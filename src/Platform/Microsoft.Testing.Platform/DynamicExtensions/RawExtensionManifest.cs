// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Target-framework-neutral representation of a manifest, produced by <see cref="DynamicExtensionJsonReader"/>
/// and validated by <see cref="DynamicExtensionManifestParser"/>. Keeping the JSON readers "dumb" means the
/// validation rules and the resulting error messages exist in exactly one place for every target framework.
/// </summary>
internal sealed class RawExtensionManifest
{
    /// <summary>
    /// Gets or sets a value indicating whether the root object declares the <c>extensions</c> property.
    /// </summary>
    public bool HasExtensionsProperty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>extensions</c> property is a JSON array.
    /// </summary>
    public bool IsExtensionsPropertyAnArray { get; set; }

    /// <summary>
    /// Gets the raw entries. A <see langword="null"/> element means the JSON element at that index was not an object.
    /// </summary>
    public List<RawExtensionEntry?> Entries { get; } = [];

    /// <summary>
    /// Gets the root-level property names the platform does not understand.
    /// </summary>
    public List<string> UnknownProperties { get; } = [];
}

/// <summary>
/// Target-framework-neutral representation of a single <c>extensions</c> array element.
/// </summary>
internal sealed class RawExtensionEntry
{
    public string? Id { get; set; }

    public string? DisplayName { get; set; }

    public string? AssemblyPath { get; set; }

    public string? TypeFullName { get; set; }

    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets the names of known properties that are present but have the wrong JSON type. Recorded rather than
    /// thrown on so that the single validation pass owns every error message.
    /// </summary>
    public List<string> InvalidProperties { get; } = [];

    /// <summary>
    /// Gets the entry-level property names the platform does not understand.
    /// </summary>
    public List<string> UnknownProperties { get; } = [];
}
