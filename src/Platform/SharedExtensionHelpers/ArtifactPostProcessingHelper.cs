// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions;

internal static class ArtifactPostProcessingHelper
{
    internal static IOrderedEnumerable<InputArtifact> OrderInputs(
        IEnumerable<InputArtifact> inputs,
        bool includeModuleMetadata)
    {
        IOrderedEnumerable<InputArtifact> orderedInputs =
            inputs.OrderBy(input => Path.GetFullPath(input.Path), StringComparer.Ordinal);

        return includeModuleMetadata
            ? orderedInputs
                .ThenBy(input => input.ProducingTestModule, StringComparer.Ordinal)
                .ThenBy(input => input.TargetFramework, StringComparer.Ordinal)
                .ThenBy(input => input.Architecture, StringComparer.Ordinal)
                .ThenBy(input => input.ExecutionId, StringComparer.Ordinal)
            : orderedInputs.ThenBy(input => input.ExecutionId, StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> is a symlink/junction, or when its
    /// attributes cannot be read. An unreadable directory is treated as unsafe because artifact
    /// post-processing must not write outside the orchestrator-supplied directory.
    /// </summary>
    internal static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
