// Copyright (c) Microsoft Corporation. All rights reserved.
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

    internal static IEnumerable<(LogLevel DefaultLevel, LogLevel CurrentLevel)> GetLogLevelCombinations()
    {
        yield return (LogLevel.Trace, LogLevel.Trace);
        yield return (LogLevel.Trace, LogLevel.Debug);
        yield return (LogLevel.Trace, LogLevel.Information);
        yield return (LogLevel.Trace, LogLevel.Warning);
        yield return (LogLevel.Trace, LogLevel.Error);
        yield return (LogLevel.Trace, LogLevel.Critical);
        yield return (LogLevel.Debug, LogLevel.Trace);
        yield return (LogLevel.Debug, LogLevel.Debug);
        yield return (LogLevel.Debug, LogLevel.Information);
        yield return (LogLevel.Debug, LogLevel.Warning);
        yield return (LogLevel.Debug, LogLevel.Error);
        yield return (LogLevel.Debug, LogLevel.Critical);
        yield return (LogLevel.Information, LogLevel.Trace);
        yield return (LogLevel.Information, LogLevel.Debug);
        yield return (LogLevel.Information, LogLevel.Information);
        yield return (LogLevel.Information, LogLevel.Warning);
        yield return (LogLevel.Information, LogLevel.Error);
        yield return (LogLevel.Information, LogLevel.Critical);
        yield return (LogLevel.Warning, LogLevel.Trace);
        yield return (LogLevel.Warning, LogLevel.Debug);
        yield return (LogLevel.Warning, LogLevel.Information);
        yield return (LogLevel.Warning, LogLevel.Warning);
        yield return (LogLevel.Warning, LogLevel.Error);
        yield return (LogLevel.Warning, LogLevel.Critical);
        yield return (LogLevel.Error, LogLevel.Trace);
        yield return (LogLevel.Error, LogLevel.Debug);
        yield return (LogLevel.Error, LogLevel.Information);
        yield return (LogLevel.Error, LogLevel.Warning);
        yield return (LogLevel.Error, LogLevel.Error);
        yield return (LogLevel.Error, LogLevel.Critical);
        yield return (LogLevel.Critical, LogLevel.Trace);
        yield return (LogLevel.Critical, LogLevel.Debug);
        yield return (LogLevel.Critical, LogLevel.Information);
        yield return (LogLevel.Critical, LogLevel.Warning);
        yield return (LogLevel.Critical, LogLevel.Error);
        yield return (LogLevel.Critical, LogLevel.Critical);
        yield return (LogLevel.None, LogLevel.Trace);
        yield return (LogLevel.None, LogLevel.Debug);
        yield return (LogLevel.None, LogLevel.Information);
        yield return (LogLevel.None, LogLevel.Warning);
        yield return (LogLevel.None, LogLevel.Error);
        yield return (LogLevel.None, LogLevel.Critical);
    }

    internal static IEnumerable<(LogLevel DefaultLevel, LogLevel CurrentLevel, bool ShouldLog)> GetLogLevelCombinationsWithShouldLog()
    {
        yield return (LogLevel.Trace, LogLevel.Trace, true);
        yield return (LogLevel.Trace, LogLevel.Debug, true);
        yield return (LogLevel.Trace, LogLevel.Information, true);
        yield return (LogLevel.Trace, LogLevel.Warning, true);
        yield return (LogLevel.Trace, LogLevel.Error, true);
        yield return (LogLevel.Trace, LogLevel.Critical, true);
        yield return (LogLevel.Debug, LogLevel.Trace, false);
        yield return (LogLevel.Debug, LogLevel.Debug, true);
        yield return (LogLevel.Debug, LogLevel.Information, true);
        yield return (LogLevel.Debug, LogLevel.Warning, true);
        yield return (LogLevel.Debug, LogLevel.Error, true);
        yield return (LogLevel.Debug, LogLevel.Critical, true);
        yield return (LogLevel.Information, LogLevel.Trace, false);
        yield return (LogLevel.Information, LogLevel.Debug, false);
        yield return (LogLevel.Information, LogLevel.Information, true);
        yield return (LogLevel.Information, LogLevel.Warning, true);
        yield return (LogLevel.Information, LogLevel.Error, true);
        yield return (LogLevel.Information, LogLevel.Critical, true);
        yield return (LogLevel.Warning, LogLevel.Trace, false);
        yield return (LogLevel.Warning, LogLevel.Debug, false);
        yield return (LogLevel.Warning, LogLevel.Information, false);
        yield return (LogLevel.Warning, LogLevel.Warning, true);
        yield return (LogLevel.Warning, LogLevel.Error, true);
        yield return (LogLevel.Warning, LogLevel.Critical, true);
        yield return (LogLevel.Error, LogLevel.Trace, false);
        yield return (LogLevel.Error, LogLevel.Debug, false);
        yield return (LogLevel.Error, LogLevel.Information, false);
        yield return (LogLevel.Error, LogLevel.Warning, false);
        yield return (LogLevel.Error, LogLevel.Error, true);
        yield return (LogLevel.Error, LogLevel.Critical, true);
        yield return (LogLevel.Critical, LogLevel.Trace, false);
        yield return (LogLevel.Critical, LogLevel.Debug, false);
        yield return (LogLevel.Critical, LogLevel.Information, false);
        yield return (LogLevel.Critical, LogLevel.Warning, false);
        yield return (LogLevel.Critical, LogLevel.Error, false);
        yield return (LogLevel.Critical, LogLevel.Critical, true);
        yield return (LogLevel.None, LogLevel.Trace, false);
        yield return (LogLevel.None, LogLevel.Debug, false);
        yield return (LogLevel.None, LogLevel.Information, false);
        yield return (LogLevel.None, LogLevel.Warning, false);
        yield return (LogLevel.None, LogLevel.Error, false);
        yield return (LogLevel.None, LogLevel.Critical, false);
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinationsWithShouldLog))]
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

    [ArgumentsProvider(nameof(GetLogLevelCombinations))]
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

    [ArgumentsProvider(nameof(GetLogLevelCombinationsWithShouldLog))]
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

    [ArgumentsProvider(nameof(GetLogLevelCombinations))]
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
