// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

/// <summary>
/// Registers artifact post-processors with a test application.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IArtifactPostProcessingManager
{
    /// <summary>
    /// Adds an artifact post-processor factory.
    /// </summary>
    /// <param name="factory">The factory used to create the post-processor.</param>
    void AddArtifactPostProcessor(Func<IServiceProvider, IArtifactPostProcessor> factory);
}
