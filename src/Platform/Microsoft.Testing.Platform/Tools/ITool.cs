// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Tools;

/// <summary>
/// Represents a non-test command that can be invoked by passing its <see cref="Name"/>
/// as the first positional command-line argument.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITool : IExtension
{
    /// <summary>
    /// Gets the command-line name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the tool is omitted from informational output.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}
