// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class NativeFileOperations : IFileOperations
{
    private readonly FileOperations _fileOperationsInner = new();

    public SourceGeneratedReflectionDataProvider? ReflectionDataProvider { get; set; }

    public object? CreateNavigationSession(string source) => ((IFileOperations)_fileOperationsInner).CreateNavigationSession(source);

    public void DisposeNavigationSession(object? navigationSession) => ((IFileOperations)_fileOperationsInner).DisposeNavigationSession(navigationSession);

    public bool DoesFileExist(string assemblyFileName) => ((IFileOperations)_fileOperationsInner).DoesFileExist(assemblyFileName);

    public string GetFullFilePath(string assemblyFileName) => ((IFileOperations)_fileOperationsInner).GetFullFilePath(assemblyFileName);

    public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string? fileName) => ((IFileOperations)_fileOperationsInner).GetNavigationData(navigationSession, className, methodName, out minLineNumber, out fileName);

    public string? GetAssemblyPath(Assembly assembly) => throw new NotImplementedException();

    public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly) => isReflectionOnly
            ? throw new InvalidOperationException("Reflection only mode is not allowed")
            : ReflectionDataProvider!.GetAssembly(assemblyName);
}
#pragma warning restore RS0016 // Add public types and members to the declared API
