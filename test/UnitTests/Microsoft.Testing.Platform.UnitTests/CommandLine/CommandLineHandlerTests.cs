// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class CommandLineHandlerTests : TestBase
{
    private readonly Mock<IPlatformOutputDevice> _outputDisplayMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IRuntimeFeature> _runtimeFeatureMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IProcessHandler> _processHandlerMock = new();
    private readonly ICommandLineOptionsProvider[] _systemCommandLineOptionsProviders = new[]
    {
        new PlatformCommandLineProvider(),
    };

    private readonly ICommandLineOptionsProvider[] _extensionCommandLineOptionsProviders = [];

    public CommandLineHandlerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public async Task ParseAndValidateAsync_InvalidCommandLineArguments_ReturnsFalse()
    {
        // Arrange
        string[] args = ["option1", "'a'"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
            {
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Invalid command line arguments:"));
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Unexpected argument 'a'"));
            });

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_EmptyCommandLineArguments_ReturnsTrue()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var commandLineHandler = new CommandLineHandler(args, parseResult,
             _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(), Times.Never);
    }

    public void IsHelpInvoked_HelpOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--help"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = commandLineHandler.IsHelpInvoked();

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(), Times.Never);
    }

    public void IsInfoInvoked_InfoOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--info"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = commandLineHandler.IsInfoInvoked();

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(), Times.Never);
    }

    public void IsVersionInvoked_VersionOptionSet_ReturnsTrue()
    {
        // Arrange
        string[] args = ["--version"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = commandLineHandler.IsOptionSet("version");

        // Assert
        Assert.IsTrue(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Never);
        _outputDisplayMock.Verify(o => o.DisplayBannerAsync(), Times.Never);
    }

    public void GetOptionValue_OptionExists_ReturnsOptionValue()
    {
        // Arrange
        var optionRecord = new OptionRecord("name", ["value1", "value2"]);
        var commandLineHandler = new CommandLineHandler([], new CommandLineParseResult(string.Empty, [optionRecord], [], []),
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = commandLineHandler.TryGetOptionArgumentList("name", out string[]? optionValue);

        // Assert
        Assert.IsTrue(result);
        Assert.IsFalse(optionValue is null);
        Assert.AreEqual(optionValue?.Length, 2);
        Assert.AreEqual("value1", optionValue?[0]);
        Assert.AreEqual("value2", optionValue?[1]);
    }

    public void GetOptionValue_OptionDoesNotExist_ReturnsNull()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());

        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
            {
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Invalid command line arguments:"));
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Unexpected argument"));
            });

        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = commandLineHandler.TryGetOptionArgumentList("name", out string[]? optionValue);

        // Assert
        Assert.IsFalse(result);
        Assert.IsTrue(optionValue is null);
    }

    public async Task ParseAndValidateAsync_DuplicateOption_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Contains("Option '--userOption' is declared by multiple extensions: 'userOption'")));

        ICommandLineOptionsProvider[] extensionCommandLineOptionsProviders = new[]
        {
            new ExtensionCommandLineProviderMockInvalidConfiguration("userOption"),
            new ExtensionCommandLineProviderMockInvalidConfiguration("userOption"),
        };
        var commandLineHandler = new CommandLineHandler(args, parseResult,
           extensionCommandLineOptionsProviders, [], _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_InvalidOption_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--diagnostic-verbosity", "r"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Equals($"Option '--diagnostic-verbosity' has invalid arguments: '--diagnostic-verbosity' expects a single level argument ('Trace', 'Debug', 'Information', 'Warning', 'Error', or 'Critical'){Environment.NewLine}", StringComparison.Ordinal)));

        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_InvalidArgumentArity_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--help arg"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Equals($"Option '--help' from provider 'Platform command line provider' (UID: PlatformCommandLineProvider) expects no arguments{Environment.NewLine}", StringComparison.Ordinal)));

        var commandLineHandler = new CommandLineHandler(args, parseResult,
            _extensionCommandLineOptionsProviders, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_ReservedOptions_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
            .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
                Assert.IsTrue(((TextOutputDeviceData)data).Text.Equals($"Option '--help' is reserved and cannot be used by providers: 'help'{Environment.NewLine}", StringComparison.Ordinal)));

        ICommandLineOptionsProvider[] extensionCommandLineProvider = new[]
        {
            new ExtensionCommandLineProviderMockReservedOptions(),
        };
        var commandLineHandler = new CommandLineHandler(args, parseResult, extensionCommandLineProvider,
            _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_ReservedOptionsPrefix_ReturnsFalse()
    {
        // Arrange
        string[] args = [];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
        .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
            Assert.IsTrue(((TextOutputDeviceData)data).Text.Equals($"Option `--internal-customextension` from provider 'Microsoft Testing Platform command line provider' (UID: PlatformCommandLineProvider) is using the reserved prefix '--internal'{Environment.NewLine}", StringComparison.Ordinal)));

        ICommandLineOptionsProvider[] extensionCommandLineProvider = new[]
        {
            new ExtensionCommandLineProviderMockInvalidConfiguration("--internal-customextension"),
        };
        var commandLineHandler = new CommandLineHandler(args, parseResult, extensionCommandLineProvider,
            _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object, _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    public async Task ParseAndValidateAsync_UnknownOption_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--x"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        var messages = new List<string>();
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
        .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) => messages.Add(((TextOutputDeviceData)data).Text));

        ICommandLineOptionsProvider[] extensionCommandLineProvider = new[]
        {
            new ExtensionCommandLineProviderMockUnknownOption(),
        };
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            extensionCommandLineProvider, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object,
            _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        Assert.IsTrue(messages.Count is 23);
        Assert.IsTrue(messages[0].Equals($"Unknown option '--x'{Environment.NewLine}", StringComparison.Ordinal));
    }

    public async Task ParseAndValidateAsync_InvalidValidConfiguration_ReturnsFalse()
    {
        // Arrange
        string[] args = ["--option"];
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        _outputDisplayMock.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()))
        .Callback((IOutputDeviceDataProducer message, IOutputDeviceData data) =>
            Assert.IsTrue(((TextOutputDeviceData)data).Text.Equals($"Invalid configuration for provider 'Microsoft Testing Platform command line provider' (UID: PlatformCommandLineProvider). Error: Invalid configuration errorMessage{Environment.NewLine}{Environment.NewLine}", StringComparison.Ordinal)));

        ICommandLineOptionsProvider[] extensionCommandLineProvider = new[]
        {
            new ExtensionCommandLineProviderMockInvalidConfiguration(),
        };
        var commandLineHandler = new CommandLineHandler(args, parseResult,
            extensionCommandLineProvider, _systemCommandLineOptionsProviders, _testApplicationModuleInfoMock.Object,
            _runtimeFeatureMock.Object, _outputDisplayMock.Object, _environmentMock.Object, _processHandlerMock.Object);

        // Act
        bool result = await commandLineHandler.ParseAndValidateAsync();

        // Assert
        Assert.IsFalse(result);
        _outputDisplayMock.Verify(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>()), Times.Once);
    }

    private sealed class ExtensionCommandLineProviderMockReservedOptions : ICommandLineOptionsProvider
    {
        public const string HelpOption = "help";

        public string Uid { get; } = nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version { get; } = AppVersion.DefaultSemVer;

        /// <inheritdoc />
        public string DisplayName { get; } = "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description { get; } = "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public CommandLineOption[] GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(HelpOption, "Show command line help.", ArgumentArity.ZeroOrOne, false),
        };

        public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage) => throw new NotImplementedException();

        public bool OptionArgumentsAreValid(CommandLineOption commandOption, string[] arguments, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }

    private sealed class ExtensionCommandLineProviderMockUnknownOption : ICommandLineOptionsProvider
    {
        public const string Option = "option";

        public string Uid { get; } = nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version { get; } = AppVersion.DefaultSemVer;

        /// <inheritdoc />
        public string DisplayName { get; } = "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description { get; } = "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public CommandLineOption[] GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(Option, "Show command line option.", ArgumentArity.ZeroOrOne, false),
        };

        public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage) => throw new NotImplementedException();

        public bool OptionArgumentsAreValid(CommandLineOption commandOption, string[] arguments, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }

    private sealed class ExtensionCommandLineProviderMockInvalidConfiguration : ICommandLineOptionsProvider
    {
        private readonly string _option = "option";

        public ExtensionCommandLineProviderMockInvalidConfiguration(string optionName = "option")
        {
            _option = optionName;
        }

        public string Uid { get; } = nameof(PlatformCommandLineProvider);

        /// <inheritdoc />
        public string Version { get; } = AppVersion.DefaultSemVer;

        /// <inheritdoc />
        public string DisplayName { get; } = "Microsoft Testing Platform command line provider";

        /// <inheritdoc />
        public string Description { get; } = "Built-in command line provider";

        /// <inheritdoc />
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public CommandLineOption[] GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(_option, "Show command line option.", ArgumentArity.ZeroOrOne, false),
        };

        public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage)
        {
            errorMessage = "Invalid configuration errorMessage";
            return false;
        }

        public bool OptionArgumentsAreValid(CommandLineOption commandOption, string[] arguments, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }
}
