// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.Tools;

/// <summary>
/// Represents command-line options that apply to a specific <see cref="ITool"/>.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IToolCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    /// <summary>
    /// Gets the name of the tool to which these options apply.
    /// </summary>
    string ToolName { get; }
}
