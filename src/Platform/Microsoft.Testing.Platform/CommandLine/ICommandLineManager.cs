// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Represents a command line manager.
/// </summary>
public interface ICommandLineManager
{
    /// <summary>
    /// Adds a command line options provider.
    /// </summary>
    /// <param name="commandLineProviderFactory">The factory method for creating the command line options provider.</param>
    void AddProvider(Func<ICommandLineOptionsProvider> commandLineProviderFactory);

    /// <summary>
    /// Adds a command line options provider.
    /// </summary>
    /// <param name="commandLineProviderFactory">The factory method for creating the command line options provider, given a service provider.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddProvider(Func<IServiceProvider, ICommandLineOptionsProvider> commandLineProviderFactory);
}
