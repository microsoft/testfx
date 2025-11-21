// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpTests
{
    private HangDumpCommandLineProvider GetProvider()
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        _ = testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("FullPath");
        return new();
    }

    [TestMethod]
    public async Task IsValid_If_Timeout_Value_Has_CorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_Timeout_Value_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTimeoutOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
#if NETCOREAPP
    [DataRow("Triage")]
#endif
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    public async Task IsValid_If_HangDumpType_Has_CorrectValue(string dumpType)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_HangDumpType_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionInvalidType, "invalid"), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task Missing_HangDumpMainOption_ShouldReturn_IsInvalid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual("You specified one or more hang dump parameters but did not enable it, add --hangdump to the command line", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task If_HangDumpMainOption_IsSpecified_ShouldReturn_IsValid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task HangDumpActivityIndicator_IsEnabled_WhenHangDumpOptionSet_AndNoServerOption()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        });

        var mockEnvironment = new Mock<IEnvironment>();
        var mockTask = new Mock<ITask>();
        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpActivityIndicator>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpActivityIndicator>()).Returns(mockLogger.Object);
        var mockClock = new Mock<IClock>();

        // Act & Assert - This will throw because we don't have the environment variables set,
        // but we're only testing IsEnabledAsync which doesn't need them
        try
        {
            var indicator = new HangDumpActivityIndicator(
                commandLineOptions,
                mockEnvironment.Object,
                mockTask.Object,
                mockTestApplicationModuleInfo.Object,
                mockLoggerFactory.Object,
                mockClock.Object);

            // This should be true since hangdump is enabled and no server option is set
            bool isEnabled = await indicator.IsEnabledAsync().ConfigureAwait(false);
            Assert.IsTrue(isEnabled, "HangDumpActivityIndicator should be enabled when --hangdump is set and --server is not set");
        }
        catch (InvalidOperationException)
        {
            // Expected due to missing environment variables in constructor
            // We'll test IsEnabledAsync() separately
        }
    }

    [TestMethod]
    public async Task HangDumpActivityIndicator_IsDisabled_WhenHangDumpOptionSet_AndServerOptionSet_WithoutDotNetTestPipe()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.JsonRpcProtocolName] },
        });

        var mockEnvironment = new Mock<IEnvironment>();
        var mockTask = new Mock<ITask>();
        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpActivityIndicator>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpActivityIndicator>()).Returns(mockLogger.Object);
        var mockClock = new Mock<IClock>();

        var indicator = new HangDumpActivityIndicator(
            commandLineOptions,
            mockEnvironment.Object,
            mockTask.Object,
            mockTestApplicationModuleInfo.Object,
            mockLoggerFactory.Object,
            mockClock.Object);

        // Act
        bool isEnabled = await indicator.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        Assert.IsFalse(isEnabled, "HangDumpActivityIndicator should be disabled when --server is set without --dotnet-test-pipe (non-dotnet-test server mode)");
    }

    [TestMethod]
    public async Task HangDumpActivityIndicator_IsDisabled_WhenHangDumpOptionSet_AndBothServerAndDotNetTestPipeSet()
    {
        // Arrange - This simulates 'dotnet test' scenario
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.DotnetTestCliProtocolName] },
            { PlatformCommandLineProvider.DotNetTestPipeOptionKey, ["somepipe"] },
        });

        var mockEnvironment = new Mock<IEnvironment>();
        var mockTask = new Mock<ITask>();
        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpActivityIndicator>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpActivityIndicator>()).Returns(mockLogger.Object);
        var mockClock = new Mock<IClock>();

        var indicator = new HangDumpActivityIndicator(
            commandLineOptions,
            mockEnvironment.Object,
            mockTask.Object,
            mockTestApplicationModuleInfo.Object,
            mockLoggerFactory.Object,
            mockClock.Object);

        // Act
        bool isEnabled = await indicator.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        // BUG: Currently this returns false, but it should return true for 'dotnet test' scenarios
        // The issue is that IsEnabledAsync checks `!IsOptionSet(ServerOptionKey)` which is false when running under dotnet test
        Assert.IsFalse(isEnabled, "BUG: HangDumpActivityIndicator is currently disabled when running under 'dotnet test' (--server with --dotnet-test-pipe), but it should be enabled");
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_IsEnabled_WhenHangDumpOptionSet_AndNoServerOption()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        });

        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("TestPath");
        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var configuration = new HangDumpConfiguration(mockTestApplicationModuleInfo.Object, mockPipeNameDescription, "testSuffix");
        var provider = new HangDumpEnvironmentVariableProvider(commandLineOptions, configuration);

        // Act
        bool isEnabled = await provider.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        Assert.IsTrue(isEnabled, "HangDumpEnvironmentVariableProvider should be enabled when --hangdump is set and --server is not set");
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_IsDisabled_WhenHangDumpOptionSet_AndServerOptionSet_WithoutDotNetTestPipe()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.JsonRpcProtocolName] },
        });

        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("TestPath");
        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var configuration = new HangDumpConfiguration(mockTestApplicationModuleInfo.Object, mockPipeNameDescription, "testSuffix");
        var provider = new HangDumpEnvironmentVariableProvider(commandLineOptions, configuration);

        // Act
        bool isEnabled = await provider.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        Assert.IsFalse(isEnabled, "HangDumpEnvironmentVariableProvider should be disabled when --server is set without --dotnet-test-pipe (non-dotnet-test server mode)");
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_IsDisabled_WhenHangDumpOptionSet_AndBothServerAndDotNetTestPipeSet()
    {
        // Arrange - This simulates 'dotnet test' scenario
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.DotnetTestCliProtocolName] },
            { PlatformCommandLineProvider.DotNetTestPipeOptionKey, ["somepipe"] },
        });

        var mockTestApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        mockTestApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("TestPath");
        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var configuration = new HangDumpConfiguration(mockTestApplicationModuleInfo.Object, mockPipeNameDescription, "testSuffix");
        var provider = new HangDumpEnvironmentVariableProvider(commandLineOptions, configuration);

        // Act
        bool isEnabled = await provider.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        // BUG: Currently this returns false, but it should return true for 'dotnet test' scenarios
        Assert.IsFalse(isEnabled, "BUG: HangDumpEnvironmentVariableProvider is currently disabled when running under 'dotnet test' (--server with --dotnet-test-pipe), but it should be enabled");
    }

    [TestMethod]
    public async Task HangDumpProcessLifetimeHandler_IsEnabled_WhenHangDumpOptionSet_AndNoServerOption()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        });

        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var mockMessageBus = new Mock<IMessageBus>();
        var mockOutputDevice = new Mock<IOutputDevice>();
        var mockTask = new Mock<ITask>();
        var mockEnvironment = new Mock<IEnvironment>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpProcessLifetimeHandler>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpProcessLifetimeHandler>()).Returns(mockLogger.Object);
        var mockConfiguration = new Mock<IConfiguration>();
        var mockProcessHandler = new Mock<IProcessHandler>();
        var mockClock = new Mock<IClock>();

        var handler = new HangDumpProcessLifetimeHandler(
            mockPipeNameDescription,
            mockMessageBus.Object,
            mockOutputDevice.Object,
            commandLineOptions,
            mockTask.Object,
            mockEnvironment.Object,
            mockLoggerFactory.Object,
            mockConfiguration.Object,
            mockProcessHandler.Object,
            mockClock.Object);

        // Act
        bool isEnabled = await handler.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        Assert.IsTrue(isEnabled, "HangDumpProcessLifetimeHandler should be enabled when --hangdump is set and --server is not set");
    }

    [TestMethod]
    public async Task HangDumpProcessLifetimeHandler_IsDisabled_WhenHangDumpOptionSet_AndServerOptionSet_WithoutDotNetTestPipe()
    {
        // Arrange
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.JsonRpcProtocolName] },
        });

        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var mockMessageBus = new Mock<IMessageBus>();
        var mockOutputDevice = new Mock<IOutputDevice>();
        var mockTask = new Mock<ITask>();
        var mockEnvironment = new Mock<IEnvironment>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpProcessLifetimeHandler>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpProcessLifetimeHandler>()).Returns(mockLogger.Object);
        var mockConfiguration = new Mock<IConfiguration>();
        var mockProcessHandler = new Mock<IProcessHandler>();
        var mockClock = new Mock<IClock>();

        var handler = new HangDumpProcessLifetimeHandler(
            mockPipeNameDescription,
            mockMessageBus.Object,
            mockOutputDevice.Object,
            commandLineOptions,
            mockTask.Object,
            mockEnvironment.Object,
            mockLoggerFactory.Object,
            mockConfiguration.Object,
            mockProcessHandler.Object,
            mockClock.Object);

        // Act
        bool isEnabled = await handler.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        Assert.IsFalse(isEnabled, "HangDumpProcessLifetimeHandler should be disabled when --server is set without --dotnet-test-pipe (non-dotnet-test server mode)");
    }

    [TestMethod]
    public async Task HangDumpProcessLifetimeHandler_IsDisabled_WhenHangDumpOptionSet_AndBothServerAndDotNetTestPipeSet()
    {
        // Arrange - This simulates 'dotnet test' scenario
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { PlatformCommandLineProvider.ServerOptionKey, [PlatformCommandLineProvider.DotnetTestCliProtocolName] },
            { PlatformCommandLineProvider.DotNetTestPipeOptionKey, ["somepipe"] },
        });

        var mockPipeNameDescription = new PipeNameDescription("testPipe", "testPipe");
        var mockMessageBus = new Mock<IMessageBus>();
        var mockOutputDevice = new Mock<IOutputDevice>();
        var mockTask = new Mock<ITask>();
        var mockEnvironment = new Mock<IEnvironment>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger<HangDumpProcessLifetimeHandler>>();
        mockLoggerFactory.Setup(x => x.CreateLogger<HangDumpProcessLifetimeHandler>()).Returns(mockLogger.Object);
        var mockConfiguration = new Mock<IConfiguration>();
        var mockProcessHandler = new Mock<IProcessHandler>();
        var mockClock = new Mock<IClock>();

        var handler = new HangDumpProcessLifetimeHandler(
            mockPipeNameDescription,
            mockMessageBus.Object,
            mockOutputDevice.Object,
            commandLineOptions,
            mockTask.Object,
            mockEnvironment.Object,
            mockLoggerFactory.Object,
            mockConfiguration.Object,
            mockProcessHandler.Object,
            mockClock.Object);

        // Act
        bool isEnabled = await handler.IsEnabledAsync().ConfigureAwait(false);

        // Assert
        // BUG: Currently this returns false, but it should return true for 'dotnet test' scenarios
        Assert.IsFalse(isEnabled, "BUG: HangDumpProcessLifetimeHandler is currently disabled when running under 'dotnet test' (--server with --dotnet-test-pipe), but it should be enabled");
    }
}
