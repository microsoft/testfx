// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

internal static class Constants
{
    /// <summary>
    /// Use a constant newline to make the generator output stable across operating systems.
    /// </summary>
    public const string NewLine = "\n";

    public const string TestClassAttributeFullName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute";

    public const string ReflectionMetadataHookFullName =
        "global::Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.ReflectionMetadataHook";

    public const string SourceGeneratedReflectionDataProviderFullName =
        "global::Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.SourceGeneratedReflectionDataProvider";

    public const string GeneratedFileSuffix = ".MSTestReflectionMetadata.g.cs";
}
