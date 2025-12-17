// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        _processHandler.Setup(x => x.GetCurrentProcess()).Returns(_process.Object);
    }

    [TestMethod]
    public void ResolveTemplate_WithBasicPlaceholders_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<pname>_<pid>_<asm>.dmp";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        Assert.Contains("test-process", result);
        Assert.Contains("12345", result);
        Assert.Contains("TestAssembly", result);
        Assert.Contains(@"test-process_12345_TestAssembly.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithTimeAndIdPlaceholders_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<time>_<id>_<os>";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        Assert.Contains("2025-09-22T13:49:34", result);
        Assert.MatchesRegex(@"2025-09-22T13:49:34_[a-f0-9]{8}_\w+", result);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Contains("linux", result);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains("windows", result);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Contains("macos", result);
        }
    }

    [TestMethod]
    public void ResolveTemplate_WithCustomReplacements_OverridesDefaultValues()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<pname>_<pid>_custom.dmp";
        var customReplacements = new Dictionary<string, string>
        {
            ["pname"] = "custom-process",
            ["pid"] = "99999",
        };

        // Act
        string result = service.ResolveTemplate(template, customReplacements);

        // Assert
        Assert.AreEqual("custom-process_99999_custom.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithUnknownPlaceholder_KeepsPlaceholderAsIs()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<unknown-field>_<pname>";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        Assert.StartsWith("<unknown-field>_", result);
        Assert.Contains("test-process", result);
    }

    [TestMethod]
    public void ResolveTemplateWithLegacySupport_WithLegacyPattern_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "dump_%p_hang.dmp";
        var legacyReplacements = new Dictionary<string, string>
        {
            ["%p"] = "54321",
        };

        // Act
        string result = service.ResolveTemplate(template, legacyReplacements);

        // Assert
        Assert.AreEqual("dump_54321_hang.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplateWithLegacySupport_WithMixedPatterns_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "%p_<pname>_hang.dmp";
        var customReplacements = new Dictionary<string, string>
        {
            ["pname"] = "notepad",
            ["%p"] = "1111",
        };

        // Act
        string result = service.ResolveTemplate(template, customReplacements);

        // Assert
        Assert.AreEqual("1111_notepad_hang.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithTfmPlaceholder_ReturnsValidTfm()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<tfm>";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        // The TFM should be a valid framework identifier
        Assert.IsNotEmpty(result);
        Assert.MatchesRegex(@"^(net\d+\.\d+|netcoreapp\d+\.\d+|net\d+|\d+\.\d+\.\d+\.\d+)$", result);
    }

    [TestMethod]
    public void ResolveTemplate_WithRootPlaceholder_ReturnsDirectoryPath()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<root>/artifacts";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        Assert.EndsWith("/artifacts", result);
        Assert.DoesNotStartWith("<root>", result);
    }

    [TestMethod]
    public void ResolveTemplate_ComplexTemplate_ReplacesAllPlaceholders()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<root>/artifacts/<os>/<asm>/dumps/<pname>_<pid>_<tfm>_<time>.dmp";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.Contains("/artifacts/", result);
        Assert.Contains("/TestAssembly/dumps/", result);
        Assert.Contains("test-process_12345_", result);
        Assert.Contains("2025-09-22T13:49:34.dmp", result);
    }

    [TestMethod]
    public void ResolveTemplate_CaseInsensitiveReplacements_WorksCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<PName>_<PID>";
        var customReplacements = new Dictionary<string, string>
        {
            ["PNAME"] = "CUSTOM",
            ["pid"] = "777",
        };

        // Act
        string result = service.ResolveTemplate(template, customReplacements);

        // Assert
        Assert.AreEqual("CUSTOM_777", result);
    }
}
