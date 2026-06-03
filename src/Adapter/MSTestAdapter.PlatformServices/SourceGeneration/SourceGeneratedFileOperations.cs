// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Source-generator-backed implementation of <see cref="IFileOperations"/>. Assembly loading is
/// served from the supplied <see cref="SourceGeneratedReflectionDataProvider"/>; the remaining
/// file-system operations are delegated to the regular <see cref="FileOperations"/> implementation.
/// </summary>
internal sealed class SourceGeneratedFileOperations : IFileOperations
{
    private readonly FileOperations _inner = new();

    public SourceGeneratedFileOperations(SourceGeneratedReflectionDataProvider dataProvider)
        => DataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));

    internal SourceGeneratedReflectionDataProvider DataProvider { get; }

    public Assembly LoadAssembly(string assemblyName)
        => DataProvider.TryGetAssembly(assemblyName, out Assembly? assembly)
            ? assembly
            : _inner.LoadAssembly(assemblyName);

    public bool DoesFileExist(string assemblyFileName) => _inner.DoesFileExist(assemblyFileName);

    public string GetFullFilePath(string assemblyFileName) => _inner.GetFullFilePath(assemblyFileName);
}
