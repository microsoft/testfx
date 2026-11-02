// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Helper that proves a published managed assembly contains ReadyToRun native code.
/// </summary>
/// <remarks>
/// This intentionally checks the non-composite image format produced by a standard RID-specific,
/// framework-dependent publish. Its CLR Managed Native Header directory points to a
/// READYTORUN_HEADER whose signature is the four bytes 'R', 'T', 'R', '\0'.
/// See https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/readytorun-format.md.
/// </remarks>
internal static class ReadyToRunAssertions
{
    // READYTORUN_SIGNATURE from the runtime's readytorun.h.
    private const uint ReadyToRunSignature = 0x00525452;

    /// <summary>
    /// Asserts that the assembly at <paramref name="assemblyPath"/> is a managed PE image that
    /// contains a genuine ReadyToRun native code header (and not merely IL).
    /// </summary>
    public static void AssertIsReadyToRunImage(string assemblyPath)
    {
        Assert.IsTrue(File.Exists(assemblyPath), $"Expected published managed assembly was not found at '{assemblyPath}'.");

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);

        CorHeader? corHeader = peReader.PEHeaders.CorHeader;
        Assert.IsNotNull(corHeader, $"'{assemblyPath}' has no CLR (COR20) header, so it is not a managed assembly at all.");

        DirectoryEntry managedNativeHeaderDirectory = corHeader.ManagedNativeHeaderDirectory;
        Assert.IsGreaterThanOrEqualTo(
            sizeof(uint),
            managedNativeHeaderDirectory.Size,
            $"'{assemblyPath}' does not contain a complete Managed Native Header directory entry.");

        PEMemoryBlock nativeHeaderBlock = peReader.GetSectionData(managedNativeHeaderDirectory.RelativeVirtualAddress);
        Assert.IsGreaterThanOrEqualTo(
            sizeof(uint),
            nativeHeaderBlock.Length,
            $"Could not locate a complete Managed Native Header in '{assemblyPath}'.");

        BlobReader blobReader = nativeHeaderBlock.GetReader();
        uint signature = blobReader.ReadUInt32();
        Assert.AreEqual(
            ReadyToRunSignature,
            signature,
            $"'{assemblyPath}' Managed Native Header does not start with the expected ReadyToRun 'RTR\\0' signature " +
            $"(found 0x{signature:X8} instead). The assembly may not genuinely be ReadyToRun-compiled.");
    }
}
