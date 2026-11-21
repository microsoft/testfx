// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class CurrentTestApplicationModuleInfoFallbackTests
{
#if NETCOREAPP
    [TestMethod]
    [DataRow("mono")]
    [DataRow("mono-sgen")]
    public void IsCurrentTestApplicationHostMonoMuxer_RecognizesBothMonoExecutableNames(string executable)
    {
        CurrentTestApplicationModuleInfo info = new(CreateEnvironment(Path.Combine("some", "directory", executable + ".exe"), []).Object, new SystemProcessHandler());

        Assert.IsTrue(info.IsCurrentTestApplicationHostMonoMuxer);
        Assert.IsFalse(info.IsCurrentTestApplicationHostDotnetMuxer);
    }

    [TestMethod]
    public void IsCurrentTestApplicationHostMonoMuxer_OtherExecutable_IsFalse()
    {
        CurrentTestApplicationModuleInfo info = new(CreateEnvironment(Path.Combine("some", "directory", "monolith.exe"), []).Object, new SystemProcessHandler());

        Assert.IsFalse(info.IsCurrentTestApplicationHostMonoMuxer);
    }

    [TestMethod]
    public void GetProcessPath_WhenTheEnvironmentHasNoProcessPath_UsesTheFirstCommandLineArgument()
    {
        string expected = Path.Combine("custom", "app.exe");
        CurrentTestApplicationModuleInfo info = new(CreateEnvironment(processPath: null, [expected, "--filter", "x"]).Object, new SystemProcessHandler());

        Assert.AreEqual(expected, info.GetProcessPath());
    }

    [TestMethod]
    public void TryGetAssemblyName_PrefersTheEntryAssemblyNameOverTheProcessFileName()
    {
        string? entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        Assert.IsNotNull(entryAssemblyName);
        CurrentTestApplicationModuleInfo info = new(CreateEnvironment(Path.Combine("some", "directory", "different-name.exe"), []).Object, new SystemProcessHandler());

        Assert.AreEqual(entryAssemblyName, info.TryGetAssemblyName());
    }
#endif

    [TestMethod]
    public void GetDisplayName_IsTheEntryAssemblyFullPath()
    {
        string? location = Assembly.GetEntryAssembly()?.Location;
        Assert.IsFalse(RoslynString.IsNullOrEmpty(location));
        CurrentTestApplicationModuleInfo info = new(new SystemEnvironment(), new SystemProcessHandler());

        Assert.AreEqual(location, info.GetDisplayName());
    }

    [TestMethod]
    public void GetCurrentTestApplicationDirectory_IsTheEntryAssemblyDirectoryWithoutTrailingSeparator()
    {
        string? location = Assembly.GetEntryAssembly()?.Location;
        Assert.IsFalse(RoslynString.IsNullOrEmpty(location));
        CurrentTestApplicationModuleInfo info = new(new SystemEnvironment(), new SystemProcessHandler());

        Assert.AreEqual(Path.GetDirectoryName(location), info.GetCurrentTestApplicationDirectory());
    }

#if NETCOREAPP
    private static Mock<IEnvironment> CreateEnvironment(string? processPath, string[] commandLineArgs)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(e => e.ProcessPath).Returns(processPath);
        environment.Setup(e => e.GetCommandLineArgs()).Returns(commandLineArgs);
        return environment;
    }
#endif
}
