// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Shared allowlist of MSTest-owned source file names that must not appear in trim/AOT publish output.
/// </summary>
/// <remarks>
/// MSTest's reflection-mode adapter transitively depends on the vstest
/// Microsoft.TestPlatform.ObjectModel submodule and System.Private.DataContractSerialization, both of
/// which emit trim warnings (IL20xx/IL30xx) outside this repo's control. We therefore cannot fail
/// publish on any trim warning. Instead, we assert that MSTest-owned source file names do not appear
/// in the publish output: the trimmer prints the originating source file path on every IL20xx/IL30xx
/// warning, so the absence of these file names in publish output is evidence that the suppressions in
/// this repo are still effective. Adding new MSTest code that produces trim warnings will surface its
/// source file here and fail any test that uses this list.
/// </remarks>
internal static class TrimAndAotAssertions
{
    /// <summary>
    /// MSTest-owned source files whose trim warnings are explicitly suppressed by this repo
    /// (see https://github.com/microsoft/testfx/pull/8686), plus the source-generator output file
    /// emitted by MSTest.SourceGeneration (see https://github.com/microsoft/testfx/pull/8586).
    /// None of these should appear in the publish output of a trim/AOT-enabled consumer.
    /// </summary>
    public static readonly string[] MSTestOwnedSourceFiles =
    [
        "TestSourceHost.cs",
        "DeploymentUtilityBase.cs",
        "ReflectionOperations.cs",
        "AssemblyResolver.cs",
        "DataSerializationHelper.cs",
        "ManagedNameHelper.cs",
        "MethodInfoExtensions.cs",
        "TestMethodFilter.cs",
        "SynchronizedSingleSessionVSTestAndTestAnywhereAdapter.cs",
        "ReflectionTestMethodInfo.cs",

        // MSTest.SourceGeneration emitted output. Its presence in trim/AOT publish output would
        // indicate the source generator emits IL-unsafe reflection calls. Both generator modes are
        // covered: the rooting generator emits '<AssemblyName>.MSTestReflectionMetadata.g.cs' (matched
        // via the '.MSTestReflectionMetadata.g.cs' suffix) and the reflection-free generator (the
        // shipped default) emits the 'MSTestReflectionMetadata.*.g.cs' registry files.
        ".MSTestReflectionMetadata.g.cs",
        "MSTestReflectionMetadata.Registry.g.cs",
        "MSTestReflectionMetadata.SupportTypes.g.cs",
        "MSTestReflectionMetadata.Registration.g.cs",
    ];
}
