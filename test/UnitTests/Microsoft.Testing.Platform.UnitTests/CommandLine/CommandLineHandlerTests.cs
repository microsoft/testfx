// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

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
        StringAssert.Contains(result.ErrorMessage, "Invalid command line arguments:");
        StringAssert.Contains(result.ErrorMessage, "Unexpected argument 'a'");
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
        StringAssert.Contains(result.ErrorMessage, "Option '--userOption' is declared by multiple extensions: 'Microsoft Testing Platform command line provider', 'Microsoft Testing Platform command line provider'");
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
        Assert.AreEqual("Option '--diagnostic-verbosity' has invalid arguments: '--diagnostic-verbosity' expects a single level argument ('Trace', 'Debug', 'Information', 'Warning', 'Error', or 'Critical')", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ParseAndValidateAsync_InvalidArgumentArity_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--help arg"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        // Act
        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, _systemCommandLineOptionsProviders,
            _extensionCommandLineOptionsProviders, new Mock<ICommandLineOptions>().Object);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Option '--help' from provider 'Platform command line provider' (UID: PlatformCommandLineProvider) expects no arguments", result.ErrorMessage);
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
        Assert.AreEqual("Option '--help' is reserved and cannot be used by providers: 'help'", result.ErrorMessage);
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
        Assert.AreEqual("Unknown option '--x'", result.ErrorMessage);
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
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
            {
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Invalid command line arguments:"));
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Unexpected argument"));
            });

        CommandLineHandler commandLineHandler = new(parseResult, _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders,
            _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object);

        // Act
        bool result = commandLineHandler.TryGetOptionArgumentList("name", out string[]? optionValue);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNull(optionValue);
    }

    private sealed class ExtensionCommandLineProviderMockReservedOptions : ICommandLineOptionsProvider
    {
        public const string HelpOption = "help";

        public string Uid => nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version => AppVersion.DefaultSemVer;

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
        public string Version => AppVersion.DefaultSemVer;

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

    private sealed class ExtensionCommandLineProviderMockInvalidConfiguration : ICommandLineOptionsProvider
    {
        private readonly string _option;

        public ExtensionCommandLineProviderMockInvalidConfiguration(string optionName = "option") => _option = optionName;

        public string Uid => nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version => AppVersion.DefaultSemVer;

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
}
