// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Represents a command line parsed option.
/// </summary>
/// <param name="name">The name of the option.</param>
/// <param name="arguments">The arguments associated to this option.</param>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class CommandLineParseOption(string name, string[] arguments)
{
    /// <summary>
    /// Gets the name of the option.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the arguments of the option.
    /// </summary>
    public string[] Arguments { get; } = arguments;
}
