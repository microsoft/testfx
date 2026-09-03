// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents a configuration provider that exposes hierarchical configuration sections.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IHierarchicalConfigurationProvider : IConfigurationProvider
{
    /// <summary>
    /// Tries to get the scalar value for the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The associated scalar value.</param>
    /// <returns><c>true</c> if a scalar value was found; <c>false</c> if the key is absent or represents only a container.</returns>
    bool TryGetScalar(string key, out string? value);

    /// <summary>
    /// Gets the immediate child keys below the specified configuration path.
    /// </summary>
    /// <param name="parentPath">The parent path, or <see langword="null"/> for the configuration root.</param>
    /// <returns>The immediate child keys.</returns>
    IEnumerable<string> GetChildKeys(string? parentPath);
}
