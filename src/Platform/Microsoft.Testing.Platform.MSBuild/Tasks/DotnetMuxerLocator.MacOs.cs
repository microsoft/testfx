// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace Microsoft.Testing.Platform.MSBuild.Tasks;

internal sealed partial class DotnetMuxerLocator
{
    // Mach-O magic numbers from https://en.wikipedia.org/wiki/Mach-O
    private const uint MachOMagic32BigEndian = 0xfeedface;    // 32-bit big-endian
    private const uint MachOMagic64BigEndian = 0xfeedfacf;    // 64-bit big-endian
    private const uint MachOMagic32LittleEndian = 0xcefaedfe; // 32-bit little-endian
    private const uint MachOMagic64LittleEndian = 0xcffaedfe; // 64-bit little-endian
    private const uint MachOMagicFatBigEndian = 0xcafebabe;   // Multi-architecture big-endian

    // See https://opensource.apple.com/source/xnu/xnu-2050.18.24/EXTERNAL_HEADERS/mach-o/loader.h
    // https://opensource.apple.com/source/xnu/xnu-4570.41.2/osfmk/mach/machine.h.auto.html
    // https://opensource.apple.com/source/xnu/xnu-4570.41.2/EXTERNAL_HEADERS/mach-o/fat.h.auto.html
    private PlatformArchitecture? GetMuxerArchitectureByMachoOnMac(string path)
    {
        try
        {
            using var headerReader = new FileStream(path, FileMode.Open, FileAccess.Read);
            uint magic = BinaryPrimitives.ReadUInt32BigEndian(ReadFourBytes(headerReader));

            // Validate magic bytes to ensure this is a valid Mach-O binary
            if (magic is not (MachOMagic32BigEndian or MachOMagic64BigEndian or MachOMagic32LittleEndian or MachOMagic64LittleEndian or MachOMagicFatBigEndian))
            {
                _resolutionLog($"DotnetHostHelper.GetMuxerArchitectureByMachoOnMac: Invalid Mach-O magic bytes: 0x{magic:X8}");
                return null;
            }

            if (magic == MachOMagicFatBigEndian)
            {
                // A fat (multi-architecture) header is followed by 'nfat_arch' (4 bytes) and then
                // one or more 'fat_arch' entries. The cputype we care about is the first field of
                // the first 'fat_arch' entry, i.e. at offset 8 (magic + nfat_arch), not offset 4.
                ReadFourBytes(headerReader);
            }

            byte[] cpuInfoBytes = ReadFourBytes(headerReader);
            uint cpuInfo = magic is MachOMagic32LittleEndian or MachOMagic64LittleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(cpuInfoBytes)
                : BinaryPrimitives.ReadUInt32BigEndian(cpuInfoBytes);
            PlatformArchitecture? architecture = (MacOsCpuType)cpuInfo switch
            {
                MacOsCpuType.Arm64Magic or MacOsCpuType.Arm64Cigam => PlatformArchitecture.ARM64,
                MacOsCpuType.X64Magic or MacOsCpuType.X64Cigam => PlatformArchitecture.X64,
                MacOsCpuType.X86Magic or MacOsCpuType.X86Cigam => PlatformArchitecture.X86,
                _ => null,
            };

            return architecture;
        }
        catch (Exception ex)
        {
            // In case of failure during header reading we must fallback to the next place(default installation path)
            _resolutionLog($"DotnetHostHelper.GetMuxerArchitectureByMachoOnMac: Failed to get architecture from Mach-O for '{path}'\n{ex}");
        }

        return null;
    }

    private static byte[] ReadFourBytes(FileStream stream)
    {
        byte[] buffer = new byte[4];
#pragma warning disable CA2022 // Avoid inexact read with 'Stream.Read'
        stream.Read(buffer, 0, buffer.Length);
#pragma warning restore CA2022 // Avoid inexact read with 'Stream.Read'
        return buffer;
    }

    internal enum MacOsCpuType : uint
    {
        /// <summary>
        /// Arm64Magic.
        /// </summary>
        Arm64Magic = 0x0100000c,

        /// <summary>
        /// Arm64Cigam.
        /// </summary>
        Arm64Cigam = 0x0c000001,

        /// <summary>
        /// X64Magic.
        /// </summary>
        X64Magic = 0x01000007,

        /// <summary>
        /// X64Cigam.
        /// </summary>
        X64Cigam = 0x07000001,

        /// <summary>
        /// X86Magic.
        /// </summary>
        X86Magic = 0x00000007,

        /// <summary>
        /// X86Cigam.
        /// </summary>
        X86Cigam = 0x07000000,
    }
}
