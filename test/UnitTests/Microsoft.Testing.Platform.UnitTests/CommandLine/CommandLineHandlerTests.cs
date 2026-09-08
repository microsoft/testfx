// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineHandlerTests
{
    private readonly Mock<IPlatformOutputDevice> _outputDisplayMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IRuntimeFeature> _runtimeFeatureMock = new();
    private readonly ICommandLineOptionsProvider[] _systemCommandLineOptionsProviders =
    [
        new PlatformCommandLineProvider()
    ];

    private readonly ICommandLineOptionsProvider[] _extensionCommandLineOptionsProviders = [];

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidCommandLineArguments_ReturnsFalse()
    {
        // Arrange
        string[] args = ["option1", "'a'"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains("Invalid command line arguments:", result.ErrorMessage);
        Assert.Contains("Unexpected argument 'a'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidCommandLineRedactsHttpTransportSecrets()
    {
        string[] args =
        [
            "--server", "dotnettestcli",
            "--dotnet-test-transport", "http",
            "--dotnet-test-http-endpoint", "https://gateway.example/private/run-id",
            "--dotnet-test-http-token", "secret-token",
            "--unknown-option",
        ];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("***REDACTED***", result.ErrorMessage);
        Assert.Contains("https://gateway.example", result.ErrorMessage);
        Assert.DoesNotContain("secret-token", result.ErrorMessage);
        Assert.DoesNotContain("/private/run-id", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("--dotnet-test-http-token", "'secret'token'", "secret")]
    [DataRow("--dotnet-test-http-endpoint", "'https://gateway.example/private'run'", "/private")]
    public async Task ParseAndValidateAsync_ParserErrorsRedactHttpTransportSecrets(
        string option,
        string value,
        string sensitiveFragment)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([option, value], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("***REDACTED***", result.ErrorMessage);
        Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("---dotnet-test-http-token=secret-token", "secret-token", "***REDACTED***")]
    [DataRow("---dotnet-test-http-endpoint=https://gateway.example/private/run", "/private/run", "https://gateway.example")]
    public async Task ParseAndValidateAsync_MalformedSensitiveOptionPrefixIsRedacted(
        string argument,
        string sensitiveFragment,
        string expectedSafeFragment)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([argument], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedSafeFragment, result.ErrorMessage);
        Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("---dotnet-test-http-token", "secret-token", "secret-token", "***REDACTED***")]
    [DataRow("---dotnet-test-http-endpoint", "https://gateway.example/private/run", "/private/run", "https://gateway.example")]
    public async Task ParseAndValidateAsync_SeparatedMalformedSensitiveOptionValueIsRedacted(
        string option,
        string value,
        string sensitiveFragment,
        string expectedSafeFragment)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([option, value], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedSafeFragment, result.ErrorMessage);
        Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(" --dotnet-test-http-token", "secret-token", "secret-token", "***REDACTED***")]
    [DataRow(" --dotnet-test-http-endpoint", "https://gateway.example/private/run", "/private/run", "https://gateway.example")]
    public async Task ParseAndValidateAsync_LeadingWhitespaceSensitiveOptionIsRedacted(
        string option,
        string value,
        string sensitiveFragment,
        string expectedSafeFragment)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([option, value], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedSafeFragment, result.ErrorMessage);
        Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("---dotnet-test-http-token secret-token", "secret-token", "***REDACTED***")]
    [DataRow("---dotnet-test-http-endpoint https://gateway.example/private/run", "/private/run", "https://gateway.example")]
    public async Task ParseAndValidateAsync_ResponseFileParserErrorsRedactExpandedSensitiveValues(
        string responseFileContent,
        string sensitiveFragment,
        string expectedSafeFragment)
    {
        string responseFilePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(responseFilePath, responseFileContent);
            CommandLineParseResult parseResult = CommandLineParser.Parse([$"@{responseFilePath}"], new SystemEnvironment());

            ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
                parseResult,
                _systemCommandLineOptionsProviders,
                _extensionCommandLineOptionsProviders,
                new Mock<ICommandLineOptions>().Object);

            Assert.IsFalse(result.IsValid);
            Assert.Contains(expectedSafeFragment, result.ErrorMessage);
            Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
        }
        finally
        {
            File.Delete(responseFilePath);
        }
    }

    [TestMethod]
    [DataRow("--dotnet-test-http-token", "@secret-token", "secret-token")]
    [DataRow("--dotnet-test-http-endpoint", "@private/run-id", "private/run-id")]
    [DataRow("---dotnet-test-http-token", "@secret-token", "secret-token")]
    [DataRow("---dotnet-test-http-endpoint", "@private/run-id", "private/run-id")]
    [DataRow(" --dotnet-test-http-token", "@secret-token", "secret-token")]
    [DataRow(" --dotnet-test-http-endpoint", "@private/run-id", "private/run-id")]
    public async Task ParseAndValidateAsync_ResponseFileErrorsRedactSensitivePath(
        string option,
        string responseFileArgument,
        string sensitiveFragment)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(
            [option, responseFileArgument],
            new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("***REDACTED***", result.ErrorMessage);
        Assert.DoesNotContain(sensitiveFragment, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("--dotnet-test-http-token")]
    [DataRow("--dotnet-test-http-endpoint")]
    public async Task ParseAndValidateAsync_ResponseFileAccessErrorsRedactSensitivePath(string option)
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        try
        {
            CommandLineParseResult parseResult = CommandLineParser.Parse(
                [option, $"@{directoryPath}"],
                new SystemEnvironment());

            ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
                parseResult,
                _systemCommandLineOptionsProviders,
                _extensionCommandLineOptionsProviders,
                new Mock<ICommandLineOptions>().Object);

            Assert.IsFalse(result.IsValid);
            Assert.Contains("***REDACTED***", result.ErrorMessage);
            Assert.DoesNotContain(directoryPath, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(directoryPath);
        }
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ValidArgumentWithColonFollowedByValidArgumentWithoutColon_ReturnsTrue()
    {
        string[] args = ["--results-directory", "TestResults", "--timeout:60m", "--ignore-exit-code", "8"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        Assert.IsEmpty(parseResult.Errors);
        Assert.IsFalse(parseResult.HasError);

        Assert.HasCount(3, parseResult.Options);

        Assert.AreEqual("results-directory", parseResult.Options[0].Name);
        string resultsDirectory = Assert.ContainsSingle(parseResult.Options[0].Arguments);
        Assert.AreEqual("TestResults", resultsDirectory);

        Assert.AreEqual("timeout", parseResult.Options[1].Name);
        string timeout = Assert.ContainsSingle(parseResult.Options[1].Arguments);
        Assert.AreEqual("60m", timeout);

        Assert.AreEqual("ignore-exit-code", parseResult.Options[2].Name);
        string ignoreExitCode = Assert.ContainsSingle(parseResult.Options[2].Arguments);
        Assert.AreEqual("8", ignoreExitCode);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ValidArgumentWithColonValueIsShort_ReturnsTrue()
    {
        string[] args = ["--results-directory", "TestResults", "--ignore-exit-code:8", "--timeout", "60m"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        Assert.IsEmpty(parseResult.Errors);
        Assert.IsFalse(parseResult.HasError);

        Assert.HasCount(3, parseResult.Options);

        Assert.AreEqual("results-directory", parseResult.Options[0].Name);
        string resultsDirectory = Assert.ContainsSingle(parseResult.Options[0].Arguments);
        Assert.AreEqual("TestResults", resultsDirectory);

        Assert.AreEqual("ignore-exit-code", parseResult.Options[1].Name);
        string ignoreExitCode = Assert.ContainsSingle(parseResult.Options[1].Arguments);
        Assert.AreEqual("8", ignoreExitCode);

        Assert.AreEqual("timeout", parseResult.Options[2].Name);
        string timeout = Assert.ContainsSingle(parseResult.Options[2].Arguments);
        Assert.AreEqual("60m", timeout);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_EmptyCommandLineArguments_ReturnsTrue()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_DuplicateOption_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockInvalidConfiguration("userOption"),
            new ExtensionCommandLineProviderMockInvalidConfiguration("userOption")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains("Option '--userOption' is declared by multiple providers: 'Microsoft Testing Platform command line provider', 'Microsoft Testing Platform command line provider'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ToolOptionDoesNotConflictWithNormalExtension()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([], new SystemEnvironment());
        ICommandLineOptionsProvider[] providers =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("input"),
            new ToolCommandLineProviderMock("input"),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            providers,
            Mock.Of<ICommandLineOptions>());

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("--report-trx")]
    [DataRow("--report-txr")]
    public async Task ParseAndValidateAsync_KnownExtensionOptionInToolMode_DoesNotSuggestPackageOrOption(string option)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["tool", option], new SystemEnvironment());
        ICommandLineOptionsProvider[] providers =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("report-trx"),
            new ToolCommandLineProviderMock("input"),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            providers,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains($"Unknown option '{option}'", result.ErrorMessage);
        Assert.DoesNotContain("Did you mean", result.ErrorMessage);
        Assert.DoesNotContain("Microsoft.Testing.Extensions.TrxReport", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_UnknownJsonOptionForSelectedTool_ReturnsInvalid()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["tool"], new SystemEnvironment());
        ICommandLineOptionsProvider[] providers =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("normal-option"),
            new ToolCommandLineProviderMock("input"),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            providers,
            Mock.Of<ICommandLineOptions>(),
            [new JsonCommandLineOptionEntry("typo", ["value"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("Unknown option '--typo'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_RepeatedToolOption_ValidatesAggregatedArguments()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(
            ["tool", "--input", "first.trx", "--input", "second.trx"],
            new SystemEnvironment());
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.SetupGet(candidate => candidate.ToolName).Returns("tool");
        provider.SetupGet(candidate => candidate.DisplayName).Returns("tool");
        provider.SetupGet(candidate => candidate.Uid).Returns("tool");
        provider.Setup(candidate => candidate.GetCommandLineOptions())
            .Returns([new("input", "input", new ArgumentArity(2, int.MaxValue), isHidden: false)]);
        provider.Setup(candidate => candidate.ValidateOptionArgumentsAsync(
                It.IsAny<CommandLineOption>(),
                It.IsAny<string[]>()))
            .Returns<CommandLineOption, string[]>((_, arguments) =>
                arguments.Length == 2
                    ? ValidationResult.ValidTask
                    : ValidationResult.InvalidTask("expected aggregated arguments"));
        provider.Setup(candidate => candidate.ValidateCommandLineOptionsAsync(It.IsAny<ICommandLineOptions>()))
            .Returns(ValidationResult.ValidTask);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            [provider.Object],
            Mock.Of<ICommandLineOptions>());

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidOption_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--diagnostic-verbosity", "r"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Option '--diagnostic-verbosity' has invalid arguments: '--diagnostic-verbosity' expects a single level argument ('Trace', 'Debug', 'Information', 'Warning', 'Error', or 'Critical')
            Command line: --diagnostic-verbosity r
            """, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidArgumentArity_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--help", "arg"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Option '--help' from provider 'Platform command line provider' (UID: PlatformCommandLineProvider) expects no arguments
            Command line: --help arg
            """, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidArgumentArityWithToolName_IncludesFullCommandLine()
    {
        // Arrange
        string[] args = ["TestProject.dll", "--dotnet-test-pipe", "pipe", "extra"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Option '--dotnet-test-pipe' from provider 'Platform command line provider' (UID: PlatformCommandLineProvider) expects at most 1 arguments
            Command line: TestProject.dll --dotnet-test-pipe pipe extra
            """, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ReservedOptions_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockReservedOptions()
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Option '--help' is reserved and cannot be used by providers: 'Microsoft Testing Platform command line provider'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ReservedOptionsPrefix_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockInvalidConfiguration("--internal-customextension")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Option `--internal-customextension` from provider 'Microsoft Testing Platform command line provider' (UID: PlatformCommandLineProvider) is using the reserved prefix '--internal'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_UnknownOption_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--x"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockUnknownOption()
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Unknown option '--x'
            Run '--help' to see the options registered by this test application. If the option belongs to an extension, ensure its package is referenced and the extension is registered.
            Command line: --x
            """, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_UnknownOptionWithCloseRegisteredOption_SuggestsOption()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["--halp"], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Unknown option '--halp'
            Did you mean '--help'?
            Run '--help' to see the options registered by this test application. If the option belongs to an extension, ensure its package is referenced and the extension is registered.
            Command line: --halp
            """,
            result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("abcdefgh", "abczefgx", true)]
    [DataRow("abcdefgh", "abczefxy", false)]
    [DataRow("abcdefghijklmn", "abczefghijklxy", true)]
    [DataRow("abcdefghijklmn", "abczefghijkwxy", false)]
    public async Task ParseAndValidateAsync_UnknownOption_RespectsSuggestionDistanceThresholds(
        string registeredOptionName,
        string unknownOptionName,
        bool shouldSuggest)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([$"--{unknownOptionName}"], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration(registeredOptionName),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains($"Unknown option '--{unknownOptionName}'", result.ErrorMessage);
        if (shouldSuggest)
        {
            Assert.Contains($"Did you mean '--{registeredOptionName}'?", result.ErrorMessage);
        }
        else
        {
            Assert.DoesNotContain("Did you mean", result.ErrorMessage);
        }
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_UnknownOptionWithAmbiguousMatches_DoesNotSuggestOption()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["--ad"], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("ab"),
            new ExtensionCommandLineProviderMockValidConfiguration("ac"),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains("Unknown option '--ad'", result.ErrorMessage);
        Assert.DoesNotContain("Did you mean", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_UnknownOptionWithCloseHiddenOption_DoesNotSuggestOption()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["--hiddden"], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("hidden", isHidden: true),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains("Unknown option '--hiddden'", result.ErrorMessage);
        Assert.DoesNotContain("Did you mean", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("logger", "trx", "Use '--report-trx' instead.")]
    [DataRow("logger", "trx;LogFileName=results.trx", "Use '--report-trx', '--report-trx-filename <FILE>' instead.")]
    [DataRow("logger", "console;verbosity=minimal", "For comparable console verbosity, use '--output minimal'.")]
    [DataRow("logger", "console;verbosity=normal", "For comparable console verbosity, use '--output normal'.")]
    [DataRow("logger", "console;verbosity=detailed", "For comparable console verbosity, use '--output detailed'.")]
    [DataRow("logger", "custom", "MTP uses reporter-specific options")]
    [DataRow("collect", "Code Coverage", "Use '--coverage' instead.")]
    [DataRow("collect", "Code Coverage;Format=cobertura", "Use '--coverage', '--coverage-output-format cobertura' instead.")]
    [DataRow("collect", "XPlat Code Coverage", "This is not a transparent replacement for Coverlet's 'XPlat Code Coverage' collector.")]
    [DataRow("collect", "XPlat Code Coverage;Format=cobertura", "This is not a transparent replacement for Coverlet's 'XPlat Code Coverage' collector.")]
    [DataRow("collect", "blame", "there is no one-to-one replacement for the VSTest blame collector.")]
    [DataRow("collect", "custom", "MTP uses collector-specific options.")]
    public async Task ParseAndValidateAsync_VSTestOption_SuggestsMTPReplacement(
        string option,
        string argument,
        string expectedGuidance)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([$"--{option}", argument], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains($"Option '--{option}' uses VSTest syntax, which is not supported by Microsoft.Testing.Platform.", result.ErrorMessage);
        Assert.Contains(expectedGuidance, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("logger", "MTP uses reporter-specific options")]
    [DataRow("collect", "MTP uses collector-specific options")]
    public async Task ParseAndValidateAsync_VSTestOptionWithoutArgument_ProvidesGeneralGuidance(
        string option,
        string expectedGuidance)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([$"--{option}"], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedGuidance, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("logger", "trx", "--report-trx", "Microsoft.Testing.Extensions.TrxReport")]
    [DataRow("collect", "Code Coverage", "--coverage", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("collect", "Code Coverage;Format=cobertura", "--coverage", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("collect", "XPlat Code Coverage", "--coverage", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("collect", "XPlat Code Coverage;Format=cobertura", "--coverage", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("collect", "blame", "--crashdump", "Microsoft.Testing.Extensions.CrashDump")]
    [DataRow("collect", "blame", "--hangdump", "Microsoft.Testing.Extensions.HangDump")]
    public async Task ParseAndValidateAsync_VSTestOption_SuggestsRequiredExtension(
        string option,
        string argument,
        string replacement,
        string packageName)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([$"--{option}", argument], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            $"Option '{replacement}' is provided by the '{packageName}' extension. Add a package reference to use it.",
            result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("logger", "trx", "report-trx", "Microsoft.Testing.Extensions.TrxReport", "Use '--report-trx' instead.")]
    [DataRow("collect", "Code Coverage", "coverage", "Microsoft.Testing.Extensions.CodeCoverage", "Use '--coverage' instead.")]
    [DataRow("collect", "XPlat Code Coverage", "coverage", "Microsoft.Testing.Extensions.CodeCoverage", "For MTP code coverage, use '--coverage'.")]
    [DataRow("collect", "blame", "crashdump", "Microsoft.Testing.Extensions.CrashDump", "Use '--crashdump' and/or '--hangdump' instead.")]
    [DataRow("collect", "blame", "hangdump", "Microsoft.Testing.Extensions.HangDump", "Use '--crashdump' and/or '--hangdump' instead.")]
    public async Task ParseAndValidateAsync_VSTestOptionWithRegisteredReplacement_DoesNotSuggestPackage(
        string option,
        string argument,
        string registeredReplacement,
        string packageName,
        string expectedGuidance)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([$"--{option}", argument], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration(registeredReplacement),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.DoesNotContain(
            $"Option '--{registeredReplacement}' is provided by the '{packageName}' extension. Add a package reference to use it.",
            result.ErrorMessage);
        Assert.Contains(expectedGuidance, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("crashdump", "Microsoft.Testing.Extensions.CrashDump", "--hangdump", "Microsoft.Testing.Extensions.HangDump")]
    [DataRow("hangdump", "Microsoft.Testing.Extensions.HangDump", "--crashdump", "Microsoft.Testing.Extensions.CrashDump")]
    public async Task ParseAndValidateAsync_VSTestBlameWithOneRegisteredReplacement_SuggestsOtherExtension(
        string registeredReplacement,
        string registeredPackageName,
        string missingReplacement,
        string missingPackageName)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["--collect", "blame"], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration(registeredReplacement),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.DoesNotContain(
            $"Option '--{registeredReplacement}' is provided by the '{registeredPackageName}' extension.",
            result.ErrorMessage);
        Assert.Contains(
            $"Option '{missingReplacement}' is provided by the '{missingPackageName}' extension. Add a package reference to use it.",
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_VSTestOptionInTestConfig_SuggestsMTPReplacement()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>(),
            [new JsonCommandLineOptionEntry("logger", ["trx"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            "In testconfig.json under 'commandLineOptions': Unknown option '--logger'",
            result.ErrorMessage);
        Assert.Contains("Use '--report-trx' instead.", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("--report-azdo", "Microsoft.Testing.Extensions.AzureDevOpsReport")]
    [DataRow("--coverage-output-format", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("--crashdump-type", "Microsoft.Testing.Extensions.CrashDump")]
    [DataRow("--report-ctrf", "Microsoft.Testing.Extensions.CtrfReport")]
    [DataRow("--report-gh", "Microsoft.Testing.Extensions.GitHubActionsReport")]
    [DataRow("--hangdump-timeout", "Microsoft.Testing.Extensions.HangDump")]
    [DataRow("--report-html", "Microsoft.Testing.Extensions.HtmlReport")]
    [DataRow("--report-junit", "Microsoft.Testing.Extensions.JUnitReport")]
    [DataRow("--retry-failed-tests", "Microsoft.Testing.Extensions.Retry")]
    [DataRow("--report-trx", "Microsoft.Testing.Extensions.TrxReport")]
    [DataRow("--capture-video", "Microsoft.Testing.Extensions.VideoRecorder")]
    public async Task ParseAndValidateAsync_KnownExtensionOptionWithoutExtension_SuggestsPackage(string option, string packageName)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([option], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains($"Unknown option '{option}'", result.ErrorMessage);
        Assert.Contains($"Option '{option}' is provided by the '{packageName}' extension. Add a package reference to use it.", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("--report-adzo", "--report-azdo", "Microsoft.Testing.Extensions.AzureDevOpsReport")]
    [DataRow("--coverge", "--coverage", "Microsoft.Testing.Extensions.CodeCoverage")]
    [DataRow("--report-htlm", "--report-html", "Microsoft.Testing.Extensions.HtmlReport")]
    public async Task ParseAndValidateAsync_MisspelledKnownExtensionOption_SuggestsOptionAndPackage(
        string option,
        string suggestedOption,
        string packageName)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([option], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.Contains($"Unknown option '{option}'", result.ErrorMessage);
        Assert.Contains($"Did you mean '{suggestedOption}'?", result.ErrorMessage);
        Assert.Contains($"Option '{suggestedOption}' is provided by the '{packageName}' extension. Add a package reference to use it.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_MisspelledRegisteredExtensionOption_DoesNotSuggestPackage()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(["--report-adzo"], new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockValidConfiguration("report-azdo"),
        ];

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            """
            Unknown option '--report-adzo'
            Did you mean '--report-azdo'?
            Run '--help' to see the options registered by this test application. If the option belongs to an extension, ensure its package is referenced and the extension is registered.
            Command line: --report-adzo
            """,
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_MisspelledJsonOption_SuggestsRegisteredOption()
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse([], new SystemEnvironment());

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            parseResult,
            _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders,
            Mock.Of<ICommandLineOptions>(),
            [new JsonCommandLineOptionEntry("halp", [], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            """
            In testconfig.json under 'commandLineOptions': Unknown option '--halp'
            Did you mean '--help'?
            """,
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_MultipleUnknownOptions_ReportsAll()
    {
        // Arrange
        string[] args = ["--x", "--y"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockUnknownOption()
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains("Unknown option '--x'", result.ErrorMessage);
        Assert.Contains("Unknown option '--y'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_MultipleReservedOptionsFromDifferentProviders_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockReservedOptions(),
            new ExtensionCommandLineProviderMockWithNamedOption("help", "Provider2")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Option '--help' is reserved and cannot be used by providers: 'Microsoft Testing Platform command line provider', 'Provider2'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_DuplicateOptionWithDistinctProviderNames_ReportsAllProviders()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockWithNamedOption("userOption", "ProviderOne"),
            new ExtensionCommandLineProviderMockWithNamedOption("userOption", "ProviderTwo"),
            new ExtensionCommandLineProviderMockWithNamedOption("userOption", "ProviderThree")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains("Option '--userOption' is declared by multiple providers: 'ProviderOne', 'ProviderTwo', 'ProviderThree'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_DuplicateOptionFromSystemProviders_ReportsProviders()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] systemCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockWithNamedOption("userOption", "ProviderOne"),
            new ExtensionCommandLineProviderMockWithNamedOption("userOption", "ProviderTwo")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains("Option '--userOption' is declared by multiple providers: 'ProviderOne', 'ProviderTwo'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_ValidOptionsWithManyProviders_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--option1", "--option2", "--option3"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders =
        [
            new ExtensionCommandLineProviderMockWithNamedOption("option1", "Provider1"),
            new ExtensionCommandLineProviderMockWithNamedOption("option2", "Provider2"),
            new ExtensionCommandLineProviderMockWithNamedOption("option3", "Provider3")
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidValidConfiguration_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--option"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ICommandLineOptionsProvider[] extensionCommandLineProvider =
        [
            new ExtensionCommandLineProviderMockInvalidConfiguration()
        ];

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            extensionCommandLineProvider, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Invalid configuration for provider 'Microsoft Testing Platform command line provider' (UID: PlatformCommandLineProvider). Error: Invalid configuration errorMessage", result.ErrorMessage);
    }

    [TestMethod]
    public void IsHelpInvoked_HelpOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--help"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        CommandLineHandler commandLineHandler = new(parseResult, _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.IsHelpInvoked();

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void IsInfoInvoked_InfoOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--info"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        CommandLineHandler commandLineHandler = new(parseResult, _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.IsInfoInvoked();

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void IsVersionInvoked_VersionOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--version"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        CommandLineHandler commandLineHandler = new(parseResult, _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.IsOptionSet("version");

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void GetOptionValue_OptionExists_ReturnsOptionValue()
    {
        // Arrange
        CommandLineParseOption option = new("name", ["value1", "value2"]);
        CommandLineHandler commandLineHandler = new(
            new CommandLineParseResult(string.Empty, [option], []), _extensionCommandLineOptionsProviders,
            _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.TryGetOptionArgumentList("name", out string[]? optionValue);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(optionValue);
        Assert.AreEqual(2, optionValue?.Length);
        Assert.AreEqual("value1", optionValue?[0]);
        Assert.AreEqual("value2", optionValue?[1]);
    }

    [TestMethod]
    public void GetOptionValue_OptionDoesNotExist_ReturnsNull()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data, CancellationToken _) =>
            {
                Assert.Contains("Invalid command line arguments:", ((TextOutputDeviceData)data).Text);
                Assert.Contains("Unexpected argument", ((TextOutputDeviceData)data).Text);
            });

        CommandLineHandler commandLineHandler = new(parseResult, _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.TryGetOptionArgumentList("name", out string[]? optionValue);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNull(optionValue);
    }

    [TestMethod]
    public void GetOptionValueOrDefault_DefaultDoesNotActivateOption()
    {
        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(x => x["commandLineOptionDefaults:name:0"])
            .Returns("default-value");
        CommandLineHandler commandLineHandler = new(
            CommandLineParseResult.Empty,
            _extensionCommandLineOptionsProviders,
            _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object,
            _runtimeFeatureMock.Object,
            configuration.Object);

        Assert.IsFalse(commandLineHandler.IsOptionSet("name"));
        Assert.IsFalse(commandLineHandler.TryGetOptionArgumentList("name", out _));
        Assert.IsTrue(commandLineHandler.TryGetOptionArgumentListOrDefault("name", out string[]? arguments));
        Assert.AreSequenceEqual(["default-value"], arguments);
    }

    [TestMethod]
    public void GetOptionValueOrDefault_ExplicitValueWins()
    {
        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(x => x["commandLineOptions:name:0"])
            .Returns("explicit-value");
        configuration
            .Setup(x => x["commandLineOptionDefaults:name:0"])
            .Returns("default-value");
        CommandLineHandler commandLineHandler = new(
            CommandLineParseResult.Empty,
            _extensionCommandLineOptionsProviders,
            _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object,
            _runtimeFeatureMock.Object,
            configuration.Object);

        Assert.IsTrue(commandLineHandler.IsOptionSet("name"));
        Assert.IsTrue(commandLineHandler.TryGetOptionArgumentListOrDefault("name", out string[]? arguments));
        Assert.AreSequenceEqual(["explicit-value"], arguments);
    }

    [TestMethod]
    public void GetOptionValueOrDefault_NullOptionNameThrows()
    {
        ICommandLineOptions commandLineOptions = Mock.Of<ICommandLineOptions>();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => commandLineOptions.TryGetOptionArgumentListOrDefault(null!, out _));
    }

    private sealed class ExtensionCommandLineProviderMockReservedOptions : ICommandLineOptionsProvider
    {
        public const string HelpOption = "help";

        public string Uid => nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version => PlatformVersion.Version;

        /// <inheritdoc />
        public string DisplayName => "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description => "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
        [
            new(HelpOption, "Show command line help.", ArgumentArity.ZeroOrOne, false)
        ];

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => throw new NotImplementedException();

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) => ValidationResult.ValidTask;
    }

    private sealed class ExtensionCommandLineProviderMockUnknownOption : ICommandLineOptionsProvider
    {
        public const string Option = "option";

        public string Uid => nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version => PlatformVersion.Version;

        /// <inheritdoc />
        public string DisplayName => "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description => "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
        [
            new(Option, "Show command line option.", ArgumentArity.ZeroOrOne, false)
        ];

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => throw new NotImplementedException();

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) => ValidationResult.ValidTask;
    }

#pragma warning disable TPEXP // Tool command-line providers are experimental.
    private sealed class ToolCommandLineProviderMock(string optionName) : IToolCommandLineOptionsProvider
    {
        public string ToolName => "tool";

        public string Uid => nameof(ToolCommandLineProviderMock);

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
            => [new(optionName, optionName, ArgumentArity.ExactlyOne, isHidden: false)];

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
            => ValidationResult.ValidTask;

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
            => ValidationResult.ValidTask;
    }
#pragma warning restore TPEXP

    private sealed class ExtensionCommandLineProviderMockValidConfiguration(string optionName, bool isHidden = false) : ICommandLineOptionsProvider
    {
        public string Uid => nameof(ExtensionCommandLineProviderMockValidConfiguration);

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
            => [new(optionName, optionName, ArgumentArity.ExactlyOne, isHidden)];

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
            => ValidationResult.ValidTask;

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
            => ValidationResult.ValidTask;
    }

    private sealed class ExtensionCommandLineProviderMockInvalidConfiguration : ICommandLineOptionsProvider
    {
        private readonly string _option;

        public ExtensionCommandLineProviderMockInvalidConfiguration(string optionName = "option") => _option = optionName;

        public string Uid => nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version => PlatformVersion.Version;

        /// <inheritdoc />
        public string DisplayName => "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description => "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
        [
            new(_option, "Show command line option.", ArgumentArity.ZeroOrOne, false)
        ];

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => ValidationResult.InvalidTask("Invalid configuration errorMessage");

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) => ValidationResult.ValidTask;
    }

    private sealed class ExtensionCommandLineProviderMockWithNamedOption : ICommandLineOptionsProvider
    {
        private readonly string _option;

        public ExtensionCommandLineProviderMockWithNamedOption(string optionName, string displayName)
        {
            _option = optionName;
            DisplayName = displayName;
            Uid = $"TestMock_{displayName}";
        }

        public string Uid { get; }

        /// <inheritdoc />
        public string Version { get; } = PlatformVersion.Version;

        /// <inheritdoc />
        public string DisplayName { get; }

        /// <inheritdoc />
        public string Description { get; } = "Test extension command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
        [
            new(_option, "Show command line option.", ArgumentArity.ZeroOrOne, false)
        ];

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => ValidationResult.ValidTask;

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) => ValidationResult.ValidTask;
    }
}
