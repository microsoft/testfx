// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Tools;

/// <summary>
/// Registers tools with a test application.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IToolsManager
{
    /// <summary>
    /// Adds a tool factory.
    /// </summary>
    /// <param name="toolFactory">The factory used to create the tool.</param>
    void AddTool(Func<IServiceProvider, ITool> toolFactory);
}
