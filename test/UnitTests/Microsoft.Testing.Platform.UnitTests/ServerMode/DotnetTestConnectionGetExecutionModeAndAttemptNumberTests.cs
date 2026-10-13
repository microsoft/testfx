// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DotnetTestConnectionGetExecutionModeAndAttemptNumberTests
{
    private static readonly ICommandLineOptionsProvider[] SystemCommandLineOptionsProviders = [new PlatformCommandLineProvider()];
    private static readonly ICommandLineOptionsProvider[] ExtensionCommandLineOptionsProviders = [];
    private static readonly MethodInfo GetExecutionModeMethod = typeof(DotnetTestConnection).GetMethod("GetExecutionMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo GetAttemptNumberMethod = typeof(DotnetTestConnection).GetMethod("GetAttemptNumber", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [TestMethod]
    public void GetExecutionMode_NoOptions_ReturnsRun()
    {
        DotnetTestConnection connection = CreateConnection([]);

        string actual = (string)GetExecutionModeMethod.Invoke(connection, null)!;

        Assert.AreEqual(HandshakeMessageExecutionModes.Run, actual);
    }

    [TestMethod]
    public void GetExecutionMode_DiscoverOption_ReturnsDiscover()
    {
        DotnetTestConnection connection = CreateConnection(["--list-tests"]);

        string actual = (string)GetExecutionModeMethod.Invoke(connection, null)!;

        Assert.AreEqual(HandshakeMessageExecutionModes.Discover, actual);
    }

    [TestMethod]
    public void GetExecutionMode_HelpOption_ReturnsHelp()
    {
        DotnetTestConnection connection = CreateConnection(["--help"]);

        string actual = (string)GetExecutionModeMethod.Invoke(connection, null)!;

        Assert.AreEqual(HandshakeMessageExecutionModes.Help, actual);
    }

    [TestMethod]
    public void GetExecutionMode_HelpAndDiscoverBothSet_HelpTakesPrecedenceOverDiscover()
    {
        DotnetTestConnection connection = CreateConnection(["--help", "--list-tests"]);

        string actual = (string)GetExecutionModeMethod.Invoke(connection, null)!;

        Assert.AreEqual(HandshakeMessageExecutionModes.Help, actual);
    }

    [TestMethod]
    public void GetExecutionMode_ToolAndHelpBothSet_ToolTakesPrecedenceOverHelp()
    {
        DotnetTestConnection connection = CreateConnection(["tool", "--help"]);

        string actual = (string)GetExecutionModeMethod.Invoke(connection, null)!;

        Assert.AreEqual(HandshakeMessageExecutionModes.Tool, actual);
    }

    [TestMethod]
    public void GetAttemptNumber_EnvironmentVariableNotSet_ReturnsOne()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns((string?)null);
        DotnetTestConnection connection = CreateConnection([], environment.Object);

        string actual = (string)GetAttemptNumberMethod.Invoke(connection, null)!;

        Assert.AreEqual("1", actual);
    }

    [TestMethod]
    public void GetAttemptNumber_EnvironmentVariableEmpty_ReturnsOne()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns(string.Empty);
        DotnetTestConnection connection = CreateConnection([], environment.Object);

        string actual = (string)GetAttemptNumberMethod.Invoke(connection, null)!;

        Assert.AreEqual("1", actual);
    }

    [TestMethod]
    [DataRow("1", DisplayName = "FirstAttempt")]
    [DataRow("5", DisplayName = "LaterAttempt")]
    public void GetAttemptNumber_PositiveInteger_ReturnsStringifiedValue(string value)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns(value);
        DotnetTestConnection connection = CreateConnection([], environment.Object);

        string actual = (string)GetAttemptNumberMethod.Invoke(connection, null)!;

        Assert.AreEqual(value, actual);
    }

    [TestMethod]
    [DataRow("0", DisplayName = "Zero")]
    [DataRow("-1", DisplayName = "Negative")]
    [DataRow("notanumber", DisplayName = "NonNumeric")]
    [DataRow("1.5", DisplayName = "Fractional")]
    public void GetAttemptNumber_InvalidValue_ThrowsInvalidOperationException(string value)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns(value);
        DotnetTestConnection connection = CreateConnection([], environment.Object);

        TargetInvocationException invocationException = Assert.ThrowsExactly<TargetInvocationException>(() => GetAttemptNumberMethod.Invoke(connection, null));

        InvalidOperationException exception = Assert.IsInstanceOfType<InvalidOperationException>(invocationException.InnerException);
        Assert.AreEqual(
            $"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER}' must contain a positive integer.",
            exception.Message);
    }

    private static DotnetTestConnection CreateConnection(string[] args, IEnvironment? environment = null)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        ITestApplicationModuleInfo testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>().Object;
        CommandLineHandler commandLineHandler = new(
            parseResult,
            ExtensionCommandLineOptionsProviders,
            SystemCommandLineOptionsProviders,
            testApplicationModuleInfo,
            new Mock<IRuntimeFeature>().Object);

        return new(
            commandLineHandler,
            environment ?? new Mock<IEnvironment>().Object,
            testApplicationModuleInfo,
            new Mock<ITestApplicationCancellationTokenSource>().Object);
    }
}
