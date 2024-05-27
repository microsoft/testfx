// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
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
    private readonly ServiceProvider _serviceProvider = new();

    public ServerLoggerForwarderTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockServerTestHost.Setup(x => x.PushDataAsync(It.IsAny<IData>())).Returns(Task.CompletedTask);
        _serviceProvider.AddService(_mockServerTestHost.Object);
    }

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public void ServerLoggerForwarder_Log(LogLevel defaultLevel, LogLevel currentLevel)
    {
        using (ServerLoggerForwarder serverLoggerForwarder = new(defaultLevel, _mockServerTestHost.Object))
        {
            serverLoggerForwarder.Log(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<IData>()), LogTestHelpers.GetExpectedLogCallTimes(defaultLevel, currentLevel));
    }

    [ArgumentsProvider(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    public async Task ServerLoggerForwarder_LogAsync(LogLevel defaultLevel, LogLevel currentLevel)
    {
        using (ServerLoggerForwarder serverLoggerForwarder = new(defaultLevel, _mockServerTestHost.Object))
        {
            await serverLoggerForwarder.LogAsync(currentLevel, Message, null, Formatter);
        }

        _mockServerTestHost.Verify(x => x.PushDataAsync(It.IsAny<ServerLogMessage>()), LogTestHelpers.GetExpectedLogCallTimes(defaultLevel, currentLevel));
    }
}
