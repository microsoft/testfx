// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Runtime.InteropServices.WindowsRuntime;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal partial class AssemblyResolver
{
    /// <summary>
    /// Event handler for windows winmd resolution.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    private void WindowsRuntimeMetadataReflectionOnlyNamespaceResolve(object sender, NamespaceResolveEventArgs args)
    {
        // Note: This will throw on pre-Win8 OS versions
        IEnumerable<string> fileNames = WindowsRuntimeMetadata.ResolveNamespace(
            args.NamespaceName,
            null,   // Will use OS installed .winmd files, you can pass explicit Windows SDK path here for searching 1st party WinRT types
            _searchDirectories);  // You can pass package graph paths, they will be used for searching .winmd files with 3rd party WinRT types

        foreach (string fileName in fileNames)
        {
            args.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(fileName));
        }
    }
}
#endif
