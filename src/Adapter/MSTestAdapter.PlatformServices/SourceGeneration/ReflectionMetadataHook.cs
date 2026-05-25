// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Entry point used by MSTest's source generator to register pre-computed reflection metadata
/// for the test assembly. Once <see cref="SetMetadata"/> has been called, MSTest's discovery and
/// execution paths will read metadata from the supplied <see cref="SourceGeneratedReflectionDataProvider"/>
/// instead of doing reflection at runtime.
/// </summary>
/// <remarks>
/// This API is intended to be invoked from a <c>[ModuleInitializer]</c> in the test assembly that
/// is emitted by the MSTest source generator. Hand-written code should not depend on it.
/// </remarks>
public static class ReflectionMetadataHook
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif
    private static readonly CompositeSourceGeneratedReflectionDataProvider Composite = new();

    /// <summary>
    /// Registers the source-generated reflection metadata for a test assembly. Safe to call from
    /// multiple module initializers — each call adds the supplied provider to a process-wide
    /// composite so that previously registered assemblies remain accessible.
    /// </summary>
    /// <param name="metadata">The metadata describing the test assembly.</param>
    public static void SetMetadata(SourceGeneratedReflectionDataProvider metadata)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        lock (Lock)
        {
            Composite.Add(metadata);

            SourceGeneratorToggle.Enable();

            var reflectionOperations = new SourceGeneratedReflectionOperations(Composite);
            var fileOperations = new SourceGeneratedFileOperations(Composite);

            if (PlatformServiceProvider.Instance is PlatformServiceProvider concreteProvider)
            {
                concreteProvider.SetSourceGeneratedOperations(reflectionOperations, fileOperations);
            }
        }
    }
}
