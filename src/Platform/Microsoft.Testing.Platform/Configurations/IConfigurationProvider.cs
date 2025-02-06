// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents a configuration provider.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IConfigurationProvider
{
    /// <summary>
    /// Loads the configuration.
    /// </summary>
    /// <returns>Async method.</returns>
    Task LoadAsync();

    /// <summary>
    /// Tries to get the value for the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The associated value.</param>
    /// <returns><c>true</c> if the key was found; <c>false</c> otherwise.</returns>
    bool TryGet(string key, out string? value);
}
