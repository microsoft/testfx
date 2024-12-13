// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SourceGeneration;

internal sealed class SourceGeneratedFileOperations : IFileOperations
{
    // Not great, but the inner class does some complicated stuff on checking if files exist, better would be to extract the functionality to a class that provides it to both these implementations.
    private readonly FileOperations _fileOperationsInner = new(skipSourceGeneratorCheck: true);

    // null is allowed here because the ReflectionDataProvider is set by the source generator.
    public SourceGeneratedReflectionDataProvider ReflectionDataProvider { get; set; } = null!;

    public object CreateNavigationSession(string source) =>
        // locations are in static metadata, nothing to do here.
        // But don't return null so the consumer thinks we are doing something.
        new();

    public void DisposeNavigationSession(object? navigationSession)
    {
        // locations are in static metadata, nothing to do here.
    }

    public bool DoesFileExist(string assemblyFileName) => _fileOperationsInner.DoesFileExist(assemblyFileName);

    public string GetFullFilePath(string assemblyFileName) => _fileOperationsInner.GetFullFilePath(assemblyFileName);

    public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string? fileName)
        => ReflectionDataProvider.GetNavigationData(className, methodName, out minLineNumber, out fileName);

    public string GetAssemblyPath(Assembly assembly)
        => throw new NotSupportedException("Only tests within the same assembly are allowed in source gen mode");

    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly) => isReflectionOnly
            ? throw new InvalidOperationException("Reflection only mode is not allowed")
            : ReflectionDataProvider.GetAssembly(assemblyName);
}
#endif
