// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents the root of an aggregated hierarchical configuration.
/// </summary>
public interface IConfigurationRoot : IConfiguration
{
    /// <summary>
    /// Gets the configuration section at the specified path.
    /// </summary>
    /// <param name="key">The section path relative to the current configuration root or section.</param>
    /// <returns>The requested configuration section.</returns>
    IConfigurationSection GetSection(string key);

    /// <summary>
    /// Gets the immediate child sections of the current configuration root or section.
    /// </summary>
    /// <remarks>
    /// Configuration providers must implement <see cref="IHierarchicalConfigurationProvider"/> for their keys
    /// to be discoverable through enumeration. Values from other providers remain accessible by requesting a
    /// known section path.
    /// </remarks>
    /// <returns>The immediate child sections.</returns>
    IEnumerable<IConfigurationSection> GetChildren();
}
