// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class CommandLineParseOption(string name, string[] arguments)
{
    public string Name { get; } = name;

    public string[] Arguments { get; } = arguments;
}
