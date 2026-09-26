// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.MSBuild.Tasks;

internal sealed partial class DotnetMuxerLocator
{
    private static PlatformArchitecture? GetMuxerArchitectureByPEHeaderOnWin(string path, Action<string> resolutionLog)
    {
        // For details refer to below code available on MSDN.
        // https://code.msdn.microsoft.com/windowsapps/CSCheckExeType-aab06100#content
        PlatformArchitecture? archType = null;
        ushort machine = 0;

        uint peHeader;
        const int imageFileMachineAmd64 = 0x8664;
        const int imageFileMachineIa64 = 0x200;
        const int imageFileMachineI386 = 0x14c;
        const int imageFileMachineArm = 0x01c0; // ARM Little-Endian
        const int imageFileMachineThumb = 0x01c2; // ARM Thumb/Thumb-2 Little-Endian
        const int imageFileMachineArmnt = 0x01c4; // ARM Thumb-2 Little-Endian
        const int imageFileMachineArm64 = 0xAA64;

        // get the input stream
        using Stream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        bool validImage = true;

        // PE Header starts @ 0x3C (60). Its a 4 byte header.
        fs.Position = 0x3C;
        peHeader = reader.ReadUInt32();

        // Check if the offset is invalid
        if (peHeader > fs.Length - 5)
        {
            resolutionLog("[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset");
            validImage = false;
        }

        if (validImage)
        {
            // Moving to PE Header start location...
            fs.Position = peHeader;

            // peHeaderSignature
            // 0x00004550 is the letters "PE" followed by two terminating zeros.
            if (reader.ReadUInt32() != 0x00004550)
            {
                validImage = false;
                resolutionLog("[GetMuxerArchitectureByPEHeaderOnWin]Missing PE signature");
            }

            if (validImage)
            {
                // Read the image file header.
                machine = reader.ReadUInt16();
                reader.ReadUInt16(); // NumberOfSections
                reader.ReadUInt32(); // TimeDateStamp
                reader.ReadUInt32(); // PointerToSymbolTable
                reader.ReadUInt32(); // NumberOfSymbols
                reader.ReadUInt16(); // SizeOfOptionalHeader
                reader.ReadUInt16(); // Characteristics

                // magic number.32bit or 64bit assembly.
                ushort magic = reader.ReadUInt16();
                if (magic is not 0x010B and not 0x020B)
                {
                    validImage = false;
                }
            }

            if (validImage)
            {
                switch (machine)
                {
                    case imageFileMachineI386:
                        archType = PlatformArchitecture.X86;
                        break;

                    case imageFileMachineAmd64:
                    case imageFileMachineIa64:
                        archType = PlatformArchitecture.X64;
                        break;

                    case imageFileMachineArm64:
                        archType = PlatformArchitecture.ARM64;
                        break;

                    case imageFileMachineArm:
                    case imageFileMachineThumb:
                    case imageFileMachineArmnt:
                        archType = PlatformArchitecture.ARM;
                        break;
                }
            }
        }

        return archType ?? throw new InvalidOperationException($"Could not determine the CPU architecture from the PE (Portable Executable) file at '{path}'. The file may be corrupt or contain an unrecognized machine type.");
    }
}
