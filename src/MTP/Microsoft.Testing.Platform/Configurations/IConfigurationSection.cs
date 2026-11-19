// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents a section of an aggregated hierarchical configuration.
/// </summary>
public interface IConfigurationSection : IConfigurationRoot
{
    /// <summary>
    /// Gets the key occupying this section's final path segment.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the full path to this section from the configuration root.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Gets a value indicating whether a provider supplied a scalar value for this section.
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    /// Gets the scalar value associated with this section.
    /// </summary>
    string? Value { get; }
}
