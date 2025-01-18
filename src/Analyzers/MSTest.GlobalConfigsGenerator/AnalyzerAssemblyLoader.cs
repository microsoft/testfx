// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

internal sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
{
    public static IAnalyzerAssemblyLoader Instance { get; } = new AnalyzerAssemblyLoader();

    private AnalyzerAssemblyLoader()
    {
    }

    public void AddDependencyLocation(string fullPath)
    {
    }

    public Assembly LoadFromPath(string fullPath)
        => Assembly.LoadFrom(fullPath);
}
