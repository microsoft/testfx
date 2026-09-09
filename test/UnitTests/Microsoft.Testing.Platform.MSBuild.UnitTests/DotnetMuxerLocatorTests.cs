// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.ExceptionServices;

using Microsoft.Testing.Platform.MSBuild.Tasks;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class DotnetMuxerLocatorTests
{
    private const uint MachOMagic32BigEndian = 0xfeedface;
    private const uint MachOMagic64BigEndian = 0xfeedfacf;
    private const uint MachOMagic32LittleEndian = 0xcefaedfe;
    private const uint MachOMagic64LittleEndian = 0xcffaedfe;
    private const uint MachOMagicFatBigEndian = 0xcafebabe;

    [TestMethod]
    [DataRow(0x014c, (int)DotnetMuxerLocator.PlatformArchitecture.X86)]
    [DataRow(0x8664, (int)DotnetMuxerLocator.PlatformArchitecture.X64)]
    [DataRow(0x0200, (int)DotnetMuxerLocator.PlatformArchitecture.X64)]
    [DataRow(0xAA64, (int)DotnetMuxerLocator.PlatformArchitecture.ARM64)]
    [DataRow(0x01c0, (int)DotnetMuxerLocator.PlatformArchitecture.ARM)]
    [DataRow(0x01c2, (int)DotnetMuxerLocator.PlatformArchitecture.ARM)]
    [DataRow(0x01c4, (int)DotnetMuxerLocator.PlatformArchitecture.ARM)]
    public void GetMuxerArchitectureByPEHeaderOnWin_MapsMachineType(
        int machine,
        int expectedArchitecture)
    {
        using TemporaryFile file = new(CreatePEHeader((ushort)machine));

        Assert.AreEqual((DotnetMuxerLocator.PlatformArchitecture)expectedArchitecture, InvokePEHeaderParser(file.Path, _ => { }));
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_ThrowsForInvalidOffset()
    {
        byte[] bytes = new byte[64];
        BitConverter.GetBytes(128u).CopyTo(bytes, 0x3C);
        using TemporaryFile file = new(bytes);
        List<string> logs = [];

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokePEHeaderParser(file.Path, logs.Add));
        Assert.Contains("[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset", logs);
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_ThrowsForMissingPESignature()
    {
        using TemporaryFile file = new(CreatePEHeader(0x014c, signature: 0));
        List<string> logs = [];

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokePEHeaderParser(file.Path, logs.Add));
        Assert.Contains("[GetMuxerArchitectureByPEHeaderOnWin]Missing PE signature", logs);
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_ThrowsForUnsupportedMagic()
    {
        using TemporaryFile file = new(CreatePEHeader(0x014c, magic: 0));

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokePEHeaderParser(file.Path, _ => { }));
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_ThrowsForUnknownMachineType()
    {
        using TemporaryFile file = new(CreatePEHeader(0xffff));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => InvokePEHeaderParser(file.Path, _ => { }));
        Assert.Contains("Could not determine the CPU architecture", exception.Message);
    }

    [TestMethod]
    [DataRow(MachOMagic32BigEndian)]
    [DataRow(MachOMagic64BigEndian)]
    [DataRow(MachOMagic32LittleEndian)]
    [DataRow(MachOMagic64LittleEndian)]
    [DataRow(MachOMagicFatBigEndian)]
    public void GetMuxerArchitectureByMachoOnMac_MapsKnownCpuTypes(uint magic)
    {
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.Arm64Magic, DotnetMuxerLocator.PlatformArchitecture.ARM64);
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.Arm64Cigam, DotnetMuxerLocator.PlatformArchitecture.ARM64);
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.X64Magic, DotnetMuxerLocator.PlatformArchitecture.X64);
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.X64Cigam, DotnetMuxerLocator.PlatformArchitecture.X64);
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.X86Magic, DotnetMuxerLocator.PlatformArchitecture.X86);
        AssertMachoArchitecture(magic, DotnetMuxerLocator.MacOsCpuType.X86Cigam, DotnetMuxerLocator.PlatformArchitecture.X86);
    }

    [TestMethod]
    public void GetMuxerArchitectureByMachoOnMac_ReturnsNullForInvalidMagic()
    {
        using TemporaryFile file = new(CreateMachoHeader(0, DotnetMuxerLocator.MacOsCpuType.X64Magic));
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);

        Assert.IsNull(InvokeMachoParser(locator, file.Path));
        Assert.Contains("DotnetHostHelper.GetMuxerArchitectureByMachoOnMac: Invalid Mach-O magic bytes: 0x00000000", logs);
    }

    [TestMethod]
    public void GetMuxerArchitectureByMachoOnMac_ReturnsNullForUnknownCpuType()
    {
        using TemporaryFile file = new(CreateMachoHeader(MachOMagic64LittleEndian, 0));
        DotnetMuxerLocator locator = new(_ => { });

        Assert.IsNull(InvokeMachoParser(locator, file.Path));
    }

    [TestMethod]
    public void GetMuxerArchitectureByMachoOnMac_ReturnsNullWhenFileCannotBeRead()
    {
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);

        Assert.IsNull(InvokeMachoParser(locator, Path.GetTempPath()));
        Assert.IsTrue(logs.Single().StartsWith("DotnetHostHelper.GetMuxerArchitectureByMachoOnMac: Failed to get architecture from Mach-O", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GetCurrentProcessArchitecture_MatchesRuntimeInformation()
    {
        DotnetMuxerLocator.PlatformArchitecture expected = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => DotnetMuxerLocator.PlatformArchitecture.X86,
            Architecture.X64 => DotnetMuxerLocator.PlatformArchitecture.X64,
            Architecture.Arm => DotnetMuxerLocator.PlatformArchitecture.ARM,
            Architecture.Arm64 => DotnetMuxerLocator.PlatformArchitecture.ARM64,
            _ => throw new NotSupportedException(),
        };

        Assert.AreEqual(expected, InvokeStaticMethod<DotnetMuxerLocator.PlatformArchitecture>("GetCurrentProcessArchitecture"));
    }

    [TestMethod]
    public void GetOSArchitecture_MatchesRuntimeInformation()
    {
        DotnetMuxerLocator.PlatformArchitecture expected = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => DotnetMuxerLocator.PlatformArchitecture.X86,
            Architecture.X64 => DotnetMuxerLocator.PlatformArchitecture.X64,
            Architecture.Arm => DotnetMuxerLocator.PlatformArchitecture.ARM,
            Architecture.Arm64 => DotnetMuxerLocator.PlatformArchitecture.ARM64,
            _ => throw new NotSupportedException(),
        };

        Assert.AreEqual(expected, InvokeStaticMethod<DotnetMuxerLocator.PlatformArchitecture>("GetOSArchitecture"));
    }

    [TestMethod]
    public void GetOperatingSystem_MatchesRuntimeInformation()
    {
        string expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX" : "Unix";

        Assert.AreEqual(expected, InvokeStaticMethod<object>("GetOperatingSystem").ToString());
    }

    [TestMethod]
    public void IsValidArchitectureMuxer_LogsCompatibleArchitecture()
    {
        if (!SupportsMuxerHeaderParsing())
        {
            Assert.Inconclusive("Architecture validation parses muxer headers only on Windows and macOS.");

            return;
        }

        using TemporaryFile file = new(CreateX64Header());
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);

        Assert.IsTrue(InvokeIsValidArchitectureMuxer(locator, DotnetMuxerLocator.PlatformArchitecture.X64, file.Path));
        Assert.Contains("DotnetHostHelper.IsValidArchitectureMuxer: Compatible architecture muxer, target architecture 'X64', actual 'X64'", logs);
    }

    [TestMethod]
    public void IsValidArchitectureMuxer_LogsIncompatibleArchitecture()
    {
        using TemporaryFile file = new(CreateX64Header());
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);

        Assert.IsFalse(InvokeIsValidArchitectureMuxer(locator, DotnetMuxerLocator.PlatformArchitecture.X86, file.Path));
        string actualArchitecture = SupportsMuxerHeaderParsing() ? "X64" : string.Empty;
        Assert.Contains($"DotnetHostHelper.IsValidArchitectureMuxer: Incompatible architecture muxer, target architecture 'X86', actual '{actualArchitecture}'", logs);
    }

    private static void AssertMachoArchitecture(
        uint magic,
        DotnetMuxerLocator.MacOsCpuType cpuType,
        DotnetMuxerLocator.PlatformArchitecture expectedArchitecture)
    {
        using TemporaryFile file = new(CreateMachoHeader(magic, cpuType));
        DotnetMuxerLocator locator = new(_ => { });

        Assert.AreEqual(expectedArchitecture, InvokeMachoParser(locator, file.Path));
    }

    private static byte[] CreatePEHeader(ushort machine, uint signature = 0x00004550, ushort magic = 0x020B)
    {
        const int peHeaderOffset = 0x40;
        byte[] bytes = new byte[peHeaderOffset + 26];
        BitConverter.GetBytes((uint)peHeaderOffset).CopyTo(bytes, 0x3C);
        BitConverter.GetBytes(signature).CopyTo(bytes, peHeaderOffset);
        BitConverter.GetBytes(machine).CopyTo(bytes, peHeaderOffset + 4);
        BitConverter.GetBytes(magic).CopyTo(bytes, peHeaderOffset + 24);
        return bytes;
    }

    private static byte[] CreateMachoHeader(uint magic, DotnetMuxerLocator.MacOsCpuType cpuType)
    {
        byte[] bytes = new byte[8];
        BitConverter.GetBytes(magic).CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)cpuType).CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] CreateX64Header()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? CreatePEHeader(0x8664)
            : CreateMachoHeader(MachOMagic64LittleEndian, DotnetMuxerLocator.MacOsCpuType.X64Magic);

    private static bool SupportsMuxerHeaderParsing()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static DotnetMuxerLocator.PlatformArchitecture? InvokePEHeaderParser(string path, Action<string> resolutionLog)
        => InvokeMethod<DotnetMuxerLocator.PlatformArchitecture?>(
            instance: null,
            "GetMuxerArchitectureByPEHeaderOnWin",
            path,
            resolutionLog);

    private static DotnetMuxerLocator.PlatformArchitecture? InvokeMachoParser(DotnetMuxerLocator locator, string path)
        => InvokeMethod<DotnetMuxerLocator.PlatformArchitecture?>(
            locator,
            "GetMuxerArchitectureByMachoOnMac",
            path);

    private static bool InvokeIsValidArchitectureMuxer(
        DotnetMuxerLocator locator,
        DotnetMuxerLocator.PlatformArchitecture targetArchitecture,
        string path)
        => InvokeMethod<bool>(
            locator,
            "IsValidArchitectureMuxer",
            targetArchitecture,
            path);

    private static T InvokeStaticMethod<T>(string methodName)
        => InvokeMethod<T>(instance: null, methodName);

    private static T InvokeMethod<T>(object? instance, string methodName, params object?[] parameters)
    {
        MethodInfo method = typeof(DotnetMuxerLocator).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find {methodName}.");

        try
        {
            return (T)method.Invoke(instance, parameters)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(byte[] content)
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllBytes(Path, content);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
