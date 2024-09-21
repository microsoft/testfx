// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SourceGeneration;

/// <summary>
/// This is only for use of MSTest source generator, the shape of this API can change at any time. Do NOT depend on the shape of this API.
/// </summary>
public static class ReflectionMetadataHook
{
    public static void SetMetadata(SourceGeneratedReflectionDataProvider metadata)
    {
        SourceGeneratorToggle.UseSourceGenerator = true;
        ((SourceGeneratedFileOperations)PlatformServiceProvider.Instance.FileOperations).ReflectionDataProvider = metadata;
        ((SourceGeneratedReflectionOperations)PlatformServiceProvider.Instance.ReflectionOperations).ReflectionDataProvider = metadata;
    }
}

#endif
