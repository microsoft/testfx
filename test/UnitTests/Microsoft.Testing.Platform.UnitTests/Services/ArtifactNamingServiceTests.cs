// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Services;

[TestClass]
public sealed class ArtifactNamingServiceTests
{
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfo = new();
    private readonly Mock<IEnvironment> _environment = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IProcessHandler> _processHandler = new();
    private readonly Mock<IProcess> _process = new();

    [TestInitialize]
    public void TestInitialize()
    {
        _testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns("TestAssembly");
        _testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns("/test/directory");

        _clock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 9, 22, 13, 49, 34, TimeSpan.Zero));

        _process.Setup(x => x.Id).Returns(12345);
        _process.Setup(x => x.Name).Returns("test-process");
        _process.Setup(x => x.Dispose());
        _processHandler.Setup(x => x.GetCurrentProcess()).Returns(_process.Object);
        _environment.Setup(x => x.Version).Returns(new Version(9, 0, 0));
    }

    [TestMethod]
    public void ResolveTemplate_WithBasicPlaceholders_ReplacesCorrectly()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<pname>_<pid>_<asm>.dmp";

        string result = service.ResolveTemplate(template);

        Assert.AreEqual("test-process_12345_TestAssembly.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithTimeAndIdPlaceholders_ReplacesCorrectly()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<time>_<id>_<os>";

        string result = service.ResolveTemplate(template);

        Assert.Contains("2025-09-22_13-49-34.0000000", result);

        string expectedOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
            : "unknown";
        Assert.EndsWith($"_{expectedOs}", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithCustomReplacements_OverridesDefaultValues()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<pname>_<pid>_custom.dmp";
        var customReplacements = new Dictionary<string, string>
        {
            ["pname"] = "custom-process",
            ["pid"] = "99999",
        };

        string result = service.ResolveTemplate(template, customReplacements);

        Assert.AreEqual("custom-process_99999_custom.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithUnknownPlaceholder_KeepsPlaceholderAsIs()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<unknown-field>_<pname>";

        string result = service.ResolveTemplate(template);

        Assert.StartsWith("<unknown-field>_", result);
        Assert.Contains("test-process", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithTfmPlaceholder_ReturnsValidTfm()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<tfm>";

        string result = service.ResolveTemplate(template);

        Assert.IsNotEmpty(result);
        Assert.MatchesRegex(@"^(net\d+\.\d+|netcoreapp\d+\.\d+|net\d+|\d+\.\d+\.\d+\.\d+)$", result);
    }

    [TestMethod]
    public void ResolveTemplate_ComplexTemplate_ReplacesAllKnownPlaceholders()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<asm>/<pname>_<pid>_<tfm>_<time>.dmp";

        string result = service.ResolveTemplate(template);

        Assert.DoesNotContain("<asm>", result);
        Assert.DoesNotContain("<pname>", result);
        Assert.DoesNotContain("<pid>", result);
        Assert.DoesNotContain("<tfm>", result);
        Assert.DoesNotContain("<time>", result);
        Assert.StartsWith("TestAssembly/", result);
        Assert.Contains("test-process_12345_", result);
        Assert.EndsWith("2025-09-22_13-49-34.0000000.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseSensitivePlaceholders_UpperCaseNotReplaced()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<PName>_<PID>";

        string result = service.ResolveTemplate(template);

        // Placeholders are case-sensitive, so <PName> and <PID> are not replaced
        Assert.AreEqual("<PName>_<PID>", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseSensitiveCustomReplacements_ExactMatchRequired()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<pname>_<pid>";
        var customReplacements = new Dictionary<string, string>
        {
            ["PNAME"] = "IGNORED",
            ["pname"] = "custom-process",
        };

        string result = service.ResolveTemplate(template, customReplacements);

        // Only exact-case match for "pname" is used; "PNAME" is ignored
        Assert.StartsWith("custom-process_", result);
    }

    [TestMethod]
    public void ResolveTemplate_NullTemplate_ThrowsArgumentException()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);

        Assert.ThrowsExactly<ArgumentException>(() => service.ResolveTemplate(null!));
    }

    [TestMethod]
    public void ResolveTemplate_EmptyTemplate_ThrowsArgumentException()
    {
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);

        Assert.ThrowsExactly<ArgumentException>(() => service.ResolveTemplate(string.Empty));
    }
}
