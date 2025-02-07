// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// Represents a configuration source.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IConfigurationSource : IExtension
{
    /// <summary>
    /// Gets the order of the configuration source.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Builds the configuration provider.
    /// </summary>
    /// <param name="commandLineParseResult">The result of the command line parsing.</param>
    /// <returns>The configuration provider.</returns>
    Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult);
}
