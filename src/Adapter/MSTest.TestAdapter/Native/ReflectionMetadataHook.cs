// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Native;

#pragma warning disable RS0016 // Add public types and members to the declared API
public static class ReflectionMetadataHook
{
    public static void SetMetadata(SourceGeneratedReflectionDataProvider metadata)
    {
        Environment.SetEnvironmentVariable("MSTEST_NATIVE", "1");
        ((NativeFileOperations)PlatformServiceProvider.Instance.FileOperations).ReflectionDataProvider = metadata;
        ((NativeReflectionOperations)PlatformServiceProvider.Instance.ReflectionOperations).ReflectionDataProvider = metadata;
    }
}
#pragma warning restore RS0016 // Add public types and members to the declared API
