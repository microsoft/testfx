// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
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
    public void TryGetDotnetPathByArchitecture_CurrentMuxerWinsWithoutEnvironmentSearch()
    {
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);
        string name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        string expectedPath = Path.Combine("isolated", "current", name);
        typeof(DotnetMuxerLocator).GetField("_currentProcessFileName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(locator, expectedPath);
        DotnetMuxerLocator.PlatformArchitecture architecture = InvokeStaticMethod<DotnetMuxerLocator.PlatformArchitecture>("GetCurrentProcessArchitecture");

        bool found = locator.TryGetDotnetPathByArchitecture(architecture, out string? path);

        Assert.IsTrue(found);
        Assert.AreEqual(expectedPath, path);
        Assert.AreEqual(
            $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Target architecture is the same as the current process architecture '{architecture}', and the current process is a muxer, using that: '{expectedPath}'",
            Assert.ContainsSingle(logs));
    }

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
    [DataRow(64, 61u)]
    [DataRow(100, 97u)]
    public void GetMuxerArchitectureByPEHeaderOnWin_RejectsOffsetJustPastLastReadableSignature(int fileLength, uint offset)
    {
        byte[] bytes = new byte[fileLength];
        BitConverter.GetBytes(offset).CopyTo(bytes, 0x3C);
        using TemporaryFile file = new(bytes);
        List<string> logs = [];

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokePEHeaderParser(file.Path, logs.Add));
        Assert.Contains("[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset", logs);
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_OffsetAtLastReadableSignature_IsNotClassifiedAsInvalidOffset()
    {
        byte[] bytes = new byte[100];
        BitConverter.GetBytes(96u).CopyTo(bytes, 0x3C);
        BitConverter.GetBytes(0x00004550u).CopyTo(bytes, 96);
        using TemporaryFile file = new(bytes);
        List<string> logs = [];

        Assert.ThrowsExactly<EndOfStreamException>(() => InvokePEHeaderParser(file.Path, logs.Add));
        Assert.DoesNotContain("[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset", logs);
    }

    [TestMethod]
    public void GetMuxerArchitectureByPEHeaderOnWin_OffsetAtLastReadableSignatureInMinimalHeader_IsNotClassifiedAsInvalidOffset()
    {
        byte[] bytes = new byte[64];
        BitConverter.GetBytes(60u).CopyTo(bytes, 0x3C);
        using TemporaryFile file = new(bytes);
        List<string> logs = [];

        Assert.ThrowsExactly<InvalidOperationException>(() => InvokePEHeaderParser(file.Path, logs.Add));
        Assert.DoesNotContain("[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset", logs);
        Assert.Contains("[GetMuxerArchitectureByPEHeaderOnWin]Missing PE signature", logs);
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
    public void Constructor_UsesPlatformSpecificMuxerName()
    {
        DotnetMuxerLocator locator = new(_ => { });
        string muxerName = (string)typeof(DotnetMuxerLocator)
            .GetField("_muxerName", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(locator)!;

        Assert.AreEqual(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet",
            muxerName);
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DataRow("DOTNET_ROOT_X64")]
    [DataRow("DOTNET_ROOT")]
    public void TryGetDotnetPathByArchitecture_ConfiguredRoot_ResolvesCompatibleMuxer(string variable)
    {
        using LocatorEnvironment environment = new();
        string expected = environment.CreateMuxer("configured", 0x8664);
        Environment.SetEnvironmentVariable(variable, Path.GetDirectoryName(expected));
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        bool found = locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path);

        Assert.IsTrue(found);
        Assert.AreEqual(expected, path);
        Assert.Contains(
            $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer compatible with 'X64' resolved from env variable '{variable}' in '{expected}'",
            logs);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void TryGetDotnetPathByArchitecture_ArchitectureSpecificRoot_TakesPrecedenceOverDefaultAndPath()
    {
        using LocatorEnvironment environment = new();
        string expected = environment.CreateMuxer("architecture", 0x8664);
        string fallback = environment.CreateMuxer("default", 0x8664);
        string pathMuxer = environment.CreateMuxer("path", 0x8664);
        Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", Path.GetDirectoryName(expected));
        Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.GetDirectoryName(fallback));
        Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(pathMuxer));
        DotnetMuxerLocator locator = environment.CreateAppHostLocator([]);

        Assert.IsTrue(locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path));
        Assert.AreEqual(expected, path);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void TryGetDotnetPathByArchitecture_NonexistentSpecificRoot_UsesDefaultRoot()
    {
        using LocatorEnvironment environment = new();
        string expected = environment.CreateMuxer("default", 0x8664);
        Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", Path.Combine(environment.Root, "not-installed"));
        Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.GetDirectoryName(expected));
        DotnetMuxerLocator locator = environment.CreateAppHostLocator([]);

        Assert.IsTrue(locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path));
        Assert.AreEqual(expected, path);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void TryGetDotnetPathByArchitecture_X86VirtualizedRoot_PrecedesDefaultRoot()
    {
        using LocatorEnvironment environment = new();
        string expected = environment.CreateMuxer("x86", 0x014c);
        string fallback = environment.CreateMuxer("default", 0x014c);
        Environment.SetEnvironmentVariable("DOTNET_ROOT_X86", Path.Combine(environment.Root, "not-installed"));
        Environment.SetEnvironmentVariable("DOTNET_ROOT(x86)", Path.GetDirectoryName(expected));
        Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.GetDirectoryName(fallback));
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        Assert.IsTrue(locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X86, out string? path));
        Assert.AreEqual(expected, path);
        Assert.Contains(
            $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer compatible with 'X86' resolved from env variable 'DOTNET_ROOT(x86)' in '{expected}'",
            logs);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DataRow(false)]
    [DataRow(true)]
    public void TryGetDotnetPathByArchitecture_InvalidConfiguredMuxer_DoesNotFallBack(bool incompatibleArchitecture)
    {
        using LocatorEnvironment environment = new();
        string configured = Path.Combine(environment.Root, "configured");
        Directory.CreateDirectory(configured);
        if (incompatibleArchitecture)
        {
            environment.CreateMuxer("configured", 0x014c);
        }

        string fallback = environment.CreateMuxer("default", 0x8664);
        Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", configured);
        Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.GetDirectoryName(fallback));
        Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(fallback));
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        Assert.IsFalse(locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path));
        Assert.IsNull(path);
        Assert.Contains(
            incompatibleArchitecture
                ? $"DotnetHostHelper: Invalid muxer resolved using env var key 'DOTNET_ROOT_X64' in '{configured}'"
                : $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Folder specified by env variable does not contain any muxer: '{configured}=DOTNET_ROOT_X64'",
            logs);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DataRow(false)]
    [DataRow(true)]
    public void TryGetDotnetPathByArchitecture_PathFallback_UsesFirstExistingCompatibleMuxer(bool nonexistentDefaultRoot)
    {
        using LocatorEnvironment environment = new();
        string expected = environment.CreateMuxer("first", 0x8664);
        string second = environment.CreateMuxer("second", 0x8664);
        if (nonexistentDefaultRoot)
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", Path.Combine(environment.Root, "missing-root"));
        }

        Environment.SetEnvironmentVariable(
            "PATH",
            string.Join(Path.PathSeparator.ToString(), Path.Combine(environment.Root, "missing-path-entry"), Path.GetDirectoryName(expected), Path.GetDirectoryName(second)));
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        Assert.IsTrue(locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path));
        Assert.AreEqual(expected, path);
        Assert.Contains(
            $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer compatible with 'X64' resolved from PATH: '{expected}'",
            logs);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void TryGetDotnetPathByArchitecture_UnsupportedArchitecture_ReturnsNoMuxer()
    {
        using LocatorEnvironment environment = new();
        Environment.SetEnvironmentVariable("ProgramFiles", environment.Root);
        Environment.SetEnvironmentVariable("ProgramFiles(x86)", environment.Root);
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        Assert.IsFalse(locator.TryGetDotnetPathByArchitecture((DotnetMuxerLocator.PlatformArchitecture)int.MaxValue, out string? path));
        Assert.IsNull(path);
        Assert.Contains("DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer was not found using DOTNET_ROOT* env variables.", logs);
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
        if (magic == MachOMagicFatBigEndian)
        {
            return CreateFatMachoHeader(magic, cpuType);
        }

        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), magic);
        if (magic is MachOMagic32LittleEndian or MachOMagic64LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), (uint)cpuType);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4, 4), (uint)cpuType);
        }

        return bytes;
    }

    // Builds a structurally valid 'fat_header' followed by a single 'fat_arch' entry, matching
    // https://opensource.apple.com/source/xnu/xnu-4570.41.2/EXTERNAL_HEADERS/mach-o/fat.h.auto.html
    // fat_header: magic (4 bytes) + nfat_arch (4 bytes)
    // fat_arch: cputype (4 bytes) + cpusubtype (4 bytes) + offset (4 bytes) + size (4 bytes) + align (4 bytes)
    private static byte[] CreateFatMachoHeader(uint magic, DotnetMuxerLocator.MacOsCpuType cpuType)
    {
        byte[] bytes = new byte[28];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), magic);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4, 4), 1u); // nfat_arch
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), (uint)cpuType); // fat_arch[0].cputype
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12, 4), 0u); // fat_arch[0].cpusubtype
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), (uint)bytes.Length); // fat_arch[0].offset
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), 0u); // fat_arch[0].size
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(24, 4), 0u); // fat_arch[0].align
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

    private sealed class LocatorEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = [];

        public LocatorEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), nameof(DotnetMuxerLocatorTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            string[] keys =
            [
                "DOTNET_ROOT_X86", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM", "DOTNET_ROOT_ARM64",
                "DOTNET_ROOT(x86)", "DOTNET_ROOT", "DOTNET_ROOT_2147483647",
                "PATH", "ProgramFiles", "ProgramFiles(x86)",
            ];
            foreach (string key in keys)
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }

            Environment.SetEnvironmentVariable("PATH", Root);
        }

        public string Root { get; }

        public string CreateMuxer(string directory, ushort machine)
        {
            string path = Path.Combine(Root, directory);
            Directory.CreateDirectory(path);
            string muxer = Path.Combine(path, "dotnet.exe");
            File.WriteAllBytes(muxer, CreatePEHeader(machine));
            return muxer;
        }

        public DotnetMuxerLocator CreateAppHostLocator(List<string> logs)
        {
            DotnetMuxerLocator locator = new(logs.Add);
            typeof(DotnetMuxerLocator).GetField("_currentProcessFileName", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(locator, Path.Combine(Root, "application.exe"));
            return locator;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, string?> entry in _originalValues)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            Directory.Delete(Root, recursive: true);
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DataRow(false)]
    [DataRow(true)]
    public void TryGetDotnetPathByArchitecture_CorruptFirstCandidate_PropagatesWithoutTryingFallbacks(bool configuredRoot)
    {
        using LocatorEnvironment environment = new();
        DotnetMuxerLocator.PlatformArchitecture architecture = RuntimeInformation.ProcessArchitecture == Architecture.X86
            ? DotnetMuxerLocator.PlatformArchitecture.X64
            : DotnetMuxerLocator.PlatformArchitecture.X86;
        ushort machine = architecture == DotnetMuxerLocator.PlatformArchitecture.X86 ? (ushort)0x014c : (ushort)0x8664;
        string corrupt = environment.CreateMuxer("corrupt", machine);
        byte[] invalidHeader = new byte[64];
        BitConverter.GetBytes(128u).CopyTo(invalidHeader, 0x3C);
        File.WriteAllBytes(corrupt, invalidHeader);
        string fallback = environment.CreateMuxer("fallback", machine);
        Environment.SetEnvironmentVariable(
            "PATH",
            configuredRoot
                ? Path.GetDirectoryName(fallback)
                : string.Join(Path.PathSeparator.ToString(), Path.GetDirectoryName(corrupt), Path.GetDirectoryName(fallback)));
        if (configuredRoot)
        {
            Environment.SetEnvironmentVariable($"DOTNET_ROOT_{architecture.ToString().ToUpperInvariant()}", Path.GetDirectoryName(corrupt));
        }

        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);
        string? path = "before-resolution";

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => locator.TryGetDotnetPathByArchitecture(architecture, out path));

        Assert.AreEqual(
            $"Could not determine the CPU architecture from the PE (Portable Executable) file at '{corrupt}'. The file may be corrupt or contain an unrecognized machine type.",
            exception.Message);
        Assert.AreEqual(corrupt, path, "The selected candidate is assigned before its header is read; the exception must not clear or replace it.");
        string[] expectedLogs = configuredRoot
            ? [
                "DotnetHostHelper.TryGetDotnetPathByArchitecture: Searching for muxer named 'dotnet.exe'",
                "[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset",
            ]
            : [
                "DotnetHostHelper.TryGetDotnetPathByArchitecture: Searching for muxer named 'dotnet.exe'",
                "DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer was not found using DOTNET_ROOT* env variables.",
                $"DotnetMuxerLocator.GetMuxerFromPath: Found dotnet.exe using PATH environment variable: '{corrupt}'",
                "[GetMuxerArchitectureByPEHeaderOnWin]Invalid offset",
            ];
        Assert.AreSequenceEqual(expectedLogs, logs);
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void TryGetDotnetPathByArchitecture_NullPath_PreservesFailureBeforeGlobalRegistration()
    {
        using LocatorEnvironment environment = new();
        Environment.SetEnvironmentVariable("PATH", null);
        List<string> logs = [];
        DotnetMuxerLocator locator = new(logs.Add);
        string? path = "before-resolution";

        Assert.ThrowsExactly<NullReferenceException>(
            () => locator.TryGetDotnetPathByArchitecture((DotnetMuxerLocator.PlatformArchitecture)int.MaxValue, out path));

        Assert.IsNull(path);
        string muxerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        Assert.AreSequenceEqual(
            [
                $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Searching for muxer named '{muxerName}'",
                "DotnetHostHelper.TryGetDotnetPathByArchitecture: Muxer was not found using DOTNET_ROOT* env variables.",
            ],
            logs);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    [DataRow("DOTNET_ROOT_X64")]
    [DataRow("DOTNET_ROOT")]
    public void TryGetDotnetPathByArchitecture_LinuxConfiguredElfMuxer_IsRejectedWithoutFallback(string variable)
    {
        using LocatorEnvironment environment = new();
        byte[] elfHeader = new byte[64];
        elfHeader[0] = 0x7F;
        elfHeader[1] = (byte)'E';
        elfHeader[2] = (byte)'L';
        elfHeader[3] = (byte)'F';
        elfHeader[4] = 2; // ELFCLASS64
        elfHeader[5] = 1; // ELFDATA2LSB
        elfHeader[6] = 1; // EV_CURRENT
        BinaryPrimitives.WriteUInt16LittleEndian(elfHeader.AsSpan(16, 2), 2); // ET_EXEC
        BinaryPrimitives.WriteUInt16LittleEndian(elfHeader.AsSpan(18, 2), 62); // EM_X86_64
        BinaryPrimitives.WriteUInt32LittleEndian(elfHeader.AsSpan(20, 4), 1); // EV_CURRENT
        BinaryPrimitives.WriteUInt16LittleEndian(elfHeader.AsSpan(52, 2), 64); // e_ehsize
        string configuredRoot = Path.Combine(environment.Root, "configured");
        string fallbackRoot = Path.Combine(environment.Root, "fallback");
        Directory.CreateDirectory(configuredRoot);
        Directory.CreateDirectory(fallbackRoot);
        File.WriteAllBytes(Path.Combine(configuredRoot, "dotnet"), elfHeader);
        File.WriteAllBytes(Path.Combine(fallbackRoot, "dotnet"), elfHeader);
        Environment.SetEnvironmentVariable("DOTNET_ROOT", fallbackRoot);
        Environment.SetEnvironmentVariable(variable, configuredRoot);
        Environment.SetEnvironmentVariable("PATH", fallbackRoot);
        List<string> logs = [];
        DotnetMuxerLocator locator = environment.CreateAppHostLocator(logs);

        bool found = locator.TryGetDotnetPathByArchitecture(DotnetMuxerLocator.PlatformArchitecture.X64, out string? path);

        Assert.IsFalse(found, "Linux currently does not assign a muxer architecture, even for an x86_64 ELF candidate.");
        Assert.IsNull(path);
        List<string> expectedLogs = [];
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            expectedLogs.Add(
                $"DotnetHostHelper.TryGetDotnetPathByArchitecture: Target architecture is the same as the current process architecture 'X64', but the current process is not a muxer: '{Path.Combine(environment.Root, "application.exe")}'");
        }

        expectedLogs.AddRange(
            [
                "DotnetHostHelper.TryGetDotnetPathByArchitecture: Searching for muxer named 'dotnet'",
                "DotnetHostHelper.IsValidArchitectureMuxer: Incompatible architecture muxer, target architecture 'X64', actual ''",
                $"DotnetHostHelper: Invalid muxer resolved using env var key '{variable}' in '{configuredRoot}'",
            ]);
        Assert.AreSequenceEqual(expectedLogs, logs);
    }
}
