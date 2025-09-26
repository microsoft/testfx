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
        _process.Setup(x => x.ProcessName).Returns("test-process");
        _processHandler.Setup(x => x.GetCurrentProcess()).Returns(_process.Object);
    }

    [TestMethod]
    public void ResolveTemplate_WithBasicPlaceholders_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<process-name>_<pid>_<assembly>.dmp";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        result.Should().Contain("test-process");
        result.Should().Contain("12345");
        result.Should().Contain("TestAssembly");
        result.Should().MatchRegex(@"test-process_12345_TestAssembly\.dmp");
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
        result.Should().Contain("2025-09-22T13:49:34");
        result.Should().MatchRegex(@"2025-09-22T13:49:34_[a-f0-9]{8}_\w+");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            result.Should().Contain("linux");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.Should().Contain("windows");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            result.Should().Contain("macos");
        }
    }

    [TestMethod]
    public void ResolveTemplate_WithCustomReplacements_OverridesDefaultValues()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<process-name>_<pid>_custom.dmp";
        var customReplacements = new Dictionary<string, string>
        {
            ["process-name"] = "custom-process",
            ["pid"] = "99999"
        };

        // Act
        string result = service.ResolveTemplate(template, customReplacements);

        // Assert
        result.Should().Be("custom-process_99999_custom.dmp");
    }

    [TestMethod]
    public void ResolveTemplate_WithUnknownPlaceholder_KeepsPlaceholderAsIs()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<unknown-field>_<process-name>";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        result.Should().StartWith("<unknown-field>_");
        result.Should().Contain("test-process");
    }

    [TestMethod]
    public void ResolveTemplateWithLegacySupport_WithLegacyPattern_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "dump_%p_hang.dmp";
        var legacyReplacements = new Dictionary<string, string>
        {
            ["%p"] = "54321"
        };

        // Act
        string result = service.ResolveTemplateWithLegacySupport(template, legacyReplacements: legacyReplacements);

        // Assert
        result.Should().Be("dump_54321_hang.dmp");
    }

    [TestMethod]
    public void ResolveTemplateWithLegacySupport_WithMixedPatterns_ReplacesCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "%p_<process-name>_hang.dmp";
        var customReplacements = new Dictionary<string, string>
        {
            ["process-name"] = "notepad"
        };
        var legacyReplacements = new Dictionary<string, string>
        {
            ["%p"] = "1111"
        };

        // Act
        string result = service.ResolveTemplateWithLegacySupport(template, customReplacements, legacyReplacements);

        // Assert
        result.Should().Be("1111_notepad_hang.dmp");
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
        result.Should().NotBeEmpty();
        result.Should().MatchRegex(@"^(net\d+\.\d+|netcoreapp\d+\.\d+|net\d+|\d+\.\d+\.\d+\.\d+)$");
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
        result.Should().EndWith("/artifacts");
        result.Should().NotStartWith("<root>");
    }

    [TestMethod]
    public void ResolveTemplate_ComplexTemplate_ReplacesAllPlaceholders()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<root>/artifacts/<os>/<assembly>/dumps/<process-name>_<pid>_<tfm>_<time>.dmp";

        // Act
        string result = service.ResolveTemplate(template);

        // Assert
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().Contain("/artifacts/");
        result.Should().Contain("/TestAssembly/dumps/");
        result.Should().Contain("test-process_12345_");
        result.Should().Contain("2025-09-22T13:49:34.dmp");
    }

    [TestMethod]
    public void ResolveTemplate_CaseInsensitiveReplacements_WorksCorrectly()
    {
        // Arrange
        var service = new ArtifactNamingService(_testApplicationModuleInfo.Object, _environment.Object, _clock.Object, _processHandler.Object);
        string template = "<Process-Name>_<PID>";
        var customReplacements = new Dictionary<string, string>
        {
            ["PROCESS-NAME"] = "CUSTOM",
            ["pid"] = "777"
        };

        // Act
        string result = service.ResolveTemplate(template, customReplacements);

        // Assert
        result.Should().Be("CUSTOM_777");
    }
}