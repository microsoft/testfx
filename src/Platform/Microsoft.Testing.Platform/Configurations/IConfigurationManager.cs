// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents a configuration manager.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IConfigurationManager
{
    /// <summary>
    /// Adds a configuration source.
    /// </summary>
    /// <param name="source">The source.</param>
    void AddConfigurationSource(Func<IConfigurationSource> source);
}
