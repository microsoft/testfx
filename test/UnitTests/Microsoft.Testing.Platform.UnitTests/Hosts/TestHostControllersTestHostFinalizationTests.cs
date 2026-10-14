// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostControllersTestHostFinalizationTests : IDisposable
{
    private readonly Mock<IProcess> _process = new();
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<ITestHostProcessLifetimeHandler> _lifetimeHandler = new();
    private readonly TestHostControllersTestHost _host;

    public TestHostControllersTestHostFinalizationTests()
    {
        _process.SetupGet(x => x.HasExited).Returns(true);
        _process.SetupGet(x => x.ExitCode).Returns(0);
        _logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        _lifetimeHandler
            .Setup(x => x.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger.Object);
        Mock<BaseMessageBus> messageBus = new();
        messageBus.Setup(x => x.DrainDataAsync()).Returns(Task.CompletedTask);
        messageBus.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);
        MessageBusProxy messageBusProxy = new();
        messageBusProxy.SetBuiltMessageBus(messageBus.Object);
        IEnvironment environment = Mock.Of<IEnvironment>();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddServices([messageBusProxy, new TestCoverageResult(), Mock.Of<ICommandLineOptions>(), environment]);
        _host = new(
            new([], [_lifetimeHandler.Object], [], testHostLauncher: null, requireProcessRestart: false),
            serviceProvider,
            passiveNode: null,
            environment,
            loggerFactory.Object,
            Mock.Of<IClock>());
    }

    [TestMethod]
    public async Task FinalizeTestHostProcessAsync_PidUpdatedDuringProcessSnapshot_UsesLatestPid()
    {
        await ReportPidAsync(101);
        Task duplicatePidRequest = Task.CompletedTask;
        _process.SetupGet(x => x.HasExited).Returns(() =>
        {
            duplicatePidRequest = ReportPidAsync(202);
            return true;
        });

        (_, TestHostProcessInformation processInformation, _) = await FinalizeTestHostProcessAsync();
        await duplicatePidRequest;

        Assert.AreEqual(202, processInformation.PID);
    }

    [TestMethod]
    public async Task FinalizeTestHostProcessAsync_PidUpdatedByExitHandler_DiagnosticUsesLatestPid()
    {
        await ReportPidAsync(101);
        _lifetimeHandler
            .Setup(x => x.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Returns(() => ReportPidAsync(303));
        string? warning = null;
        _logger
            .Setup(x => x.LogAsync(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => warning = message)
            .Returns(Task.CompletedTask);

        (_, TestHostProcessInformation processInformation, _) = await FinalizeTestHostProcessAsync();

        Assert.AreEqual(101, processInformation.PID);
        const string ExpectedWarning = """
            Test host did not exit gracefully.
              OS exit code: '0'
              OS exit code authoritative: 'True'
              IPC-reported exit code: '<not received>'
              TestHostCompletedRequest received: 'False'
              PID: '303'
              CancellationRequested: 'False'
            """;
        Assert.AreEqual(ExpectedWarning.Replace("\r\n", "\n"), warning?.Replace("\r\n", "\n"));
    }

    public void Dispose() => _host.Dispose();

    private Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)> FinalizeTestHostProcessAsync()
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "FinalizeTestHostProcessAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.FinalizeTestHostProcessAsync.");
        return (Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)>?)method.Invoke(
            _host,
            [
                _process.Object,
                new ProxyOutputDevice(new NopPlatformOutputDevice(), serverModeOutputDevice: null),
                new TelemetryInformation(isEnabled: false, version: "test"),
                Stopwatch.StartNew(),
                CancellationToken.None,
            ])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.FinalizeTestHostProcessAsync returned null.");
    }

    private Task ReportPidAsync(int pid)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "HandleRequestAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.HandleRequestAsync.");
        // IPC models are source-linked into tests, so use the request type from the host assembly.
        Type requestType = typeof(TestHostControllersTestHost).Assembly.GetType(typeof(TestHostProcessPIDRequest).FullName!)
            ?? throw new InvalidOperationException("Could not find the host's TestHostProcessPIDRequest type.");
        object request = Activator.CreateInstance(requestType, [pid])
            ?? throw new InvalidOperationException("Could not create TestHostProcessPIDRequest.");
        return (Task?)method.Invoke(_host, [request])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.HandleRequestAsync returned null.");
    }
}
