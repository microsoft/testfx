// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SourceGeneration;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class SourceGeneratedFileOperations : IFileOperations
{
    // Not great, but the inner class does some complicated stuff on checking if files exist, better would be to extract the functionality to a class that provides it to both these implementations.
    private readonly FileOperations _fileOperationsInner = new(skipNativeCheck: true);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public SourceGeneratedReflectionDataProvider ReflectionDataProvider { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public object? CreateNavigationSession(string source) =>
        // locations are in static metadata, nothing to do here.
        // But don't return null so the consumer thinks we are doing something.
        new();

    public void DisposeNavigationSession(object? navigationSession)
    {
        // locations are in static metadata, nothing to do here.
    }

    public bool DoesFileExist(string assemblyFileName) => ((IFileOperations)_fileOperationsInner).DoesFileExist(assemblyFileName);

    public string GetFullFilePath(string assemblyFileName) => ((IFileOperations)_fileOperationsInner).GetFullFilePath(assemblyFileName);

    public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string? fileName)
        => ReflectionDataProvider!.GetNavigationData(className, methodName, out minLineNumber, out fileName);

    public string? GetAssemblyPath(Assembly assembly)
        => throw new NotSupportedException("Only tests within the same assembly are allowed in source gen mode");

    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly) => isReflectionOnly
            ? throw new InvalidOperationException("Reflection only mode is not allowed")
            : ReflectionDataProvider!.GetAssembly(assemblyName);
}
#pragma warning restore RS0016 // Add public types and members to the declared API
#endif
