// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents a test application builder that supports registering artifact post-processors and tools.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IArtifactPostProcessingApplicationBuilder : ITestApplicationBuilder
{
    /// <summary>
    /// Gets the artifact post-processing manager.
    /// </summary>
    IArtifactPostProcessingManager ArtifactPostProcessing { get; }

    /// <summary>
    /// Gets the tools manager.
    /// </summary>
    IToolsManager Tools { get; }
}
