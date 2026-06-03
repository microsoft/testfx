// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Entry point used by MSTest's source generator to register pre-computed reflection metadata
/// for the test assembly. Once a builder returned from <see cref="ForAssembly(Assembly)"/> has
/// been <see cref="MetadataBuilder.Register">registered</see>, MSTest's discovery and execution
/// paths read metadata from the source-generated data instead of doing reflection at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This API is intended to be invoked from a <c>[ModuleInitializer]</c> in the test assembly
/// that is emitted by the MSTest source generator. Hand-written code should not depend on it.
/// </para>
/// <para>
/// <b>Discovery limitation.</b> The MSTest source generator only enumerates types that carry
/// <c>[TestClass]</c> declared directly on the type. Test classes that inherit
/// <c>[TestClass]</c> from a base class are <i>not</i> registered through this hook and will
/// not be discovered when the source-generated provider is the active reflection backend.
/// Apply <c>[TestClass]</c> directly to the derived class to opt it back into discovery.
/// Analyzer <c>MSTEST0069</c> (shipped in the MSTest.SourceGeneration package) flags classes
/// that hit this limitation.
/// </para>
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
    /// Begins building a source-generated metadata registration for <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The test assembly the metadata describes.</param>
    /// <returns>
    /// A builder that the source-generated module initializer fills in and then publishes via
    /// <see cref="MetadataBuilder.Register"/>.
    /// </returns>
    public static MetadataBuilder ForAssembly(Assembly assembly)
        => assembly is null
            ? throw new ArgumentNullException(nameof(assembly))
            : new MetadataBuilder(assembly);

    // Invoked by MetadataBuilder.Register; serialized with the rest of process-wide state.
    internal static void Register(SourceGeneratedReflectionDataProvider provider)
    {
        lock (Lock)
        {
            Composite.Add(provider);

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
