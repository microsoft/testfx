﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ServerLoggerForwarderTests : TestBase
{
    private const string Message = "Dummy";

    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1}", state, exception is not null ? $" -- {exception}" : string.Empty);

    private readonly Mock<IServerTestHost> _mockServerTestHost = new();

    public ServerLoggerForwarderTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockServerTestHost.Setup(x => x.IsInitialized).Returns(true);
        _mockServerTestHost.Setup(x => x.PushDataAsync(It.IsAny<IData>())).Returns(Task.CompletedTask);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Trace, LogLevel.Debug, true)]
    [Arguments(LogLevel.Trace, LogLevel.Information, true)]
    [Arguments(LogLevel.Trace, LogLevel.Warning, true)]
    [Arguments(LogLevel.Trace, LogLevel.Error, true)]
    [Arguments(LogLevel.Trace, LogLevel.Critical, true)]
    [Arguments(LogLevel.Debug, LogLevel.Trace, false)]
    [Arguments(LogLevel.Debug, LogLevel.Debug, true)]
    [Arguments(LogLevel.Debug, LogLevel.Information, true)]
    [Arguments(LogLevel.Debug, LogLevel.Warning, true)]
    [Arguments(LogLevel.Debug, LogLevel.Error, true)]
    [Arguments(LogLevel.Debug, LogLevel.Critical, true)]
    [Arguments(LogLevel.Information, LogLevel.Trace, false)]
    [Arguments(LogLevel.Information, LogLevel.Debug, false)]
    [Arguments(LogLevel.Information, LogLevel.Information, true)]
    [Arguments(LogLevel.Information, LogLevel.Warning, true)]
    [Arguments(LogLevel.Information, LogLevel.Error, true)]
    [Arguments(LogLevel.Information, LogLevel.Critical, true)]
    [Arguments(LogLevel.Warning, LogLevel.Trace, false)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Information, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Error, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.Error, LogLevel.Trace, false)]
    [Arguments(LogLevel.Error, LogLevel.Debug, false)]
    [Arguments(LogLevel.Error, LogLevel.Information, false)]
    [Arguments(LogLevel.Error, LogLevel.Warning, false)]
    [Arguments(LogLevel.Error, LogLevel.Error, true)]
    [Arguments(LogLevel.Error, LogLevel.Critical, true)]
    [Arguments(LogLevel.Critical, LogLevel.Trace, false)]
    [Arguments(LogLevel.Critical, LogLevel.Debug, false)]
    [Arguments(LogLevel.Critical, LogLevel.Information, false)]
    [Arguments(LogLevel.Critical, LogLevel.Warning, false)]
    [Arguments(LogLevel.Critical, LogLevel.Error, false)]
    [Arguments(LogLevel.Critical, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Trace, false)]
    [Arguments(LogLevel.None, LogLevel.Debug, false)]
    [Arguments(LogLevel.None, LogLevel.Information, false)]
    [Arguments(LogLevel.None, LogLevel.Warning, false)]
    [Arguments(LogLevel.None, LogLevel.Error, false)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    public void ServerLoggerForwarder_Log(LogLevel defaultLevel, LogLevel currentLevel, bool shouldLog)
    {
        using (ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                defaultLevel,
                new SystemTask(),
                _mockServerTestHost.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!)
        {
            serverLoggerForwarder.Log(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<IData>()), shouldLog ? Times.Once : Times.Never);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace)]
    [Arguments(LogLevel.Trace, LogLevel.Debug)]
    [Arguments(LogLevel.Trace, LogLevel.Information)]
    [Arguments(LogLevel.Trace, LogLevel.Warning)]
    [Arguments(LogLevel.Trace, LogLevel.Error)]
    [Arguments(LogLevel.Trace, LogLevel.Critical)]
    [Arguments(LogLevel.Debug, LogLevel.Trace)]
    [Arguments(LogLevel.Debug, LogLevel.Debug)]
    [Arguments(LogLevel.Debug, LogLevel.Information)]
    [Arguments(LogLevel.Debug, LogLevel.Warning)]
    [Arguments(LogLevel.Debug, LogLevel.Error)]
    [Arguments(LogLevel.Debug, LogLevel.Critical)]
    [Arguments(LogLevel.Information, LogLevel.Trace)]
    [Arguments(LogLevel.Information, LogLevel.Debug)]
    [Arguments(LogLevel.Information, LogLevel.Information)]
    [Arguments(LogLevel.Information, LogLevel.Warning)]
    [Arguments(LogLevel.Information, LogLevel.Error)]
    [Arguments(LogLevel.Information, LogLevel.Critical)]
    [Arguments(LogLevel.Warning, LogLevel.Trace)]
    [Arguments(LogLevel.Warning, LogLevel.Debug)]
    [Arguments(LogLevel.Warning, LogLevel.Information)]
    [Arguments(LogLevel.Warning, LogLevel.Warning)]
    [Arguments(LogLevel.Warning, LogLevel.Error)]
    [Arguments(LogLevel.Warning, LogLevel.Critical)]
    [Arguments(LogLevel.Error, LogLevel.Trace)]
    [Arguments(LogLevel.Error, LogLevel.Debug)]
    [Arguments(LogLevel.Error, LogLevel.Information)]
    [Arguments(LogLevel.Error, LogLevel.Warning)]
    [Arguments(LogLevel.Error, LogLevel.Error)]
    [Arguments(LogLevel.Error, LogLevel.Critical)]
    [Arguments(LogLevel.Critical, LogLevel.Trace)]
    [Arguments(LogLevel.Critical, LogLevel.Debug)]
    [Arguments(LogLevel.Critical, LogLevel.Information)]
    [Arguments(LogLevel.Critical, LogLevel.Warning)]
    [Arguments(LogLevel.Critical, LogLevel.Error)]
    [Arguments(LogLevel.Critical, LogLevel.Critical)]
    [Arguments(LogLevel.None, LogLevel.Trace)]
    [Arguments(LogLevel.None, LogLevel.Debug)]
    [Arguments(LogLevel.None, LogLevel.Information)]
    [Arguments(LogLevel.None, LogLevel.Warning)]
    [Arguments(LogLevel.None, LogLevel.Error)]
    [Arguments(LogLevel.None, LogLevel.Critical)]
    public void ServerLoggerForwarder_ServerLogNotInitialized_NoLogForwarded(LogLevel defaultLevel, LogLevel currentLevel)
    {
        _mockServerTestHost.Setup(x => x.IsInitialized).Returns(false);

        using (ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                defaultLevel,
                new SystemTask(),
                _mockServerTestHost.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!)
        {
            serverLoggerForwarder.Log(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<IData>()), Times.Never);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Trace, LogLevel.Debug, true)]
    [Arguments(LogLevel.Trace, LogLevel.Information, true)]
    [Arguments(LogLevel.Trace, LogLevel.Warning, true)]
    [Arguments(LogLevel.Trace, LogLevel.Error, true)]
    [Arguments(LogLevel.Trace, LogLevel.Critical, true)]
    [Arguments(LogLevel.Debug, LogLevel.Trace, false)]
    [Arguments(LogLevel.Debug, LogLevel.Debug, true)]
    [Arguments(LogLevel.Debug, LogLevel.Information, true)]
    [Arguments(LogLevel.Debug, LogLevel.Warning, true)]
    [Arguments(LogLevel.Debug, LogLevel.Error, true)]
    [Arguments(LogLevel.Debug, LogLevel.Critical, true)]
    [Arguments(LogLevel.Information, LogLevel.Trace, false)]
    [Arguments(LogLevel.Information, LogLevel.Debug, false)]
    [Arguments(LogLevel.Information, LogLevel.Information, true)]
    [Arguments(LogLevel.Information, LogLevel.Warning, true)]
    [Arguments(LogLevel.Information, LogLevel.Error, true)]
    [Arguments(LogLevel.Information, LogLevel.Critical, true)]
    [Arguments(LogLevel.Warning, LogLevel.Trace, false)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Information, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Error, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.Error, LogLevel.Trace, false)]
    [Arguments(LogLevel.Error, LogLevel.Debug, false)]
    [Arguments(LogLevel.Error, LogLevel.Information, false)]
    [Arguments(LogLevel.Error, LogLevel.Warning, false)]
    [Arguments(LogLevel.Error, LogLevel.Error, true)]
    [Arguments(LogLevel.Error, LogLevel.Critical, true)]
    [Arguments(LogLevel.Critical, LogLevel.Trace, false)]
    [Arguments(LogLevel.Critical, LogLevel.Debug, false)]
    [Arguments(LogLevel.Critical, LogLevel.Information, false)]
    [Arguments(LogLevel.Critical, LogLevel.Warning, false)]
    [Arguments(LogLevel.Critical, LogLevel.Error, false)]
    [Arguments(LogLevel.Critical, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Trace, false)]
    [Arguments(LogLevel.None, LogLevel.Debug, false)]
    [Arguments(LogLevel.None, LogLevel.Information, false)]
    [Arguments(LogLevel.None, LogLevel.Warning, false)]
    [Arguments(LogLevel.None, LogLevel.Error, false)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    public async Task ServerLoggerForwarder_LogAsync(LogLevel defaultLevel, LogLevel currentLevel, bool shouldLog)
    {
        using (ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                defaultLevel,
                new SystemTask(),
                _mockServerTestHost.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!)
        {
            await serverLoggerForwarder.LogAsync(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<ServerLogMessage>()), shouldLog ? Times.Once : Times.Never);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace)]
    [Arguments(LogLevel.Trace, LogLevel.Debug)]
    [Arguments(LogLevel.Trace, LogLevel.Information)]
    [Arguments(LogLevel.Trace, LogLevel.Warning)]
    [Arguments(LogLevel.Trace, LogLevel.Error)]
    [Arguments(LogLevel.Trace, LogLevel.Critical)]
    [Arguments(LogLevel.Debug, LogLevel.Trace)]
    [Arguments(LogLevel.Debug, LogLevel.Debug)]
    [Arguments(LogLevel.Debug, LogLevel.Information)]
    [Arguments(LogLevel.Debug, LogLevel.Warning)]
    [Arguments(LogLevel.Debug, LogLevel.Error)]
    [Arguments(LogLevel.Debug, LogLevel.Critical)]
    [Arguments(LogLevel.Information, LogLevel.Trace)]
    [Arguments(LogLevel.Information, LogLevel.Debug)]
    [Arguments(LogLevel.Information, LogLevel.Information)]
    [Arguments(LogLevel.Information, LogLevel.Warning)]
    [Arguments(LogLevel.Information, LogLevel.Error)]
    [Arguments(LogLevel.Information, LogLevel.Critical)]
    [Arguments(LogLevel.Warning, LogLevel.Trace)]
    [Arguments(LogLevel.Warning, LogLevel.Debug)]
    [Arguments(LogLevel.Warning, LogLevel.Information)]
    [Arguments(LogLevel.Warning, LogLevel.Warning)]
    [Arguments(LogLevel.Warning, LogLevel.Error)]
    [Arguments(LogLevel.Warning, LogLevel.Critical)]
    [Arguments(LogLevel.Error, LogLevel.Trace)]
    [Arguments(LogLevel.Error, LogLevel.Debug)]
    [Arguments(LogLevel.Error, LogLevel.Information)]
    [Arguments(LogLevel.Error, LogLevel.Warning)]
    [Arguments(LogLevel.Error, LogLevel.Error)]
    [Arguments(LogLevel.Error, LogLevel.Critical)]
    [Arguments(LogLevel.Critical, LogLevel.Trace)]
    [Arguments(LogLevel.Critical, LogLevel.Debug)]
    [Arguments(LogLevel.Critical, LogLevel.Information)]
    [Arguments(LogLevel.Critical, LogLevel.Warning)]
    [Arguments(LogLevel.Critical, LogLevel.Error)]
    [Arguments(LogLevel.Critical, LogLevel.Critical)]
    [Arguments(LogLevel.None, LogLevel.Trace)]
    [Arguments(LogLevel.None, LogLevel.Debug)]
    [Arguments(LogLevel.None, LogLevel.Information)]
    [Arguments(LogLevel.None, LogLevel.Warning)]
    [Arguments(LogLevel.None, LogLevel.Error)]
    [Arguments(LogLevel.None, LogLevel.Critical)]
    public async Task ServerLoggerForwarder_ServerLogNotInitialized_NoLogAsyncForwarded(LogLevel defaultLevel, LogLevel currentLevel)
    {
        _mockServerTestHost.Setup(x => x.IsInitialized).Returns(false);

        using (ServerLoggerForwarder serverLoggerForwarder = (new ServerLoggerForwarderProvider(
                defaultLevel,
                new SystemTask(),
                _mockServerTestHost.Object)
            .CreateLogger("Test") as ServerLoggerForwarder)!)
        {
            await serverLoggerForwarder.LogAsync(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<ServerLogMessage>()), Times.Never);
    }
}
