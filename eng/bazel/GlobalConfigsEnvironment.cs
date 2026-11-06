// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

internal static class GlobalConfigsEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Bazel action paths are relative; AnalyzerFileReference requires an absolute path.
        if (Environment.GetEnvironmentVariable("OUTPUT_PATH") is { } outputPath)
        {
            Environment.SetEnvironmentVariable("OUTPUT_PATH", Path.GetFullPath(outputPath));
        }
    }
}
