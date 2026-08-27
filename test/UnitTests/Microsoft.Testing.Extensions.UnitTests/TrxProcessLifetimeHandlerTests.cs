// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;
using System.Reflection;

using Microsoft.Testing.Extensions.CrashDump;
using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxProcessLifetimeHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BeforeTestHostProcessStartAsync_CreatesPipeBeforeSchedulingConnectionWait()
    {
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [CrashDumpCommandLineOptions.CrashDumpOptionName] = [],
            [TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
        });
        Mock<IEnvironment> environment = new();
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        var task = new DeferredTask();
        Assembly trxAssembly = typeof(TrxProcessLifetimeHandler).Assembly;
        Type namedPipeServerType = trxAssembly.GetType("Microsoft.Testing.Platform.IPC.NamedPipeServer", throwOnError: true)!;
        MethodInfo getPipeName = namedPipeServerType.GetMethod("GetPipeName", [typeof(string)])!;
        object pipeName = getPipeName.Invoke(null, [Guid.NewGuid().ToString("N")])!;
        ConstructorInfo constructor = typeof(TrxProcessLifetimeHandler).GetConstructors().Single();

        using var handler = (TrxProcessLifetimeHandler)constructor.Invoke([
            commandLineOptions,
            environment.Object,
            loggerFactory.Object,
            new Mock<IMessageBus>().Object,
            new Mock<IFileSystem>().Object,
            new Mock<ITestApplicationModuleInfo>().Object,
            new Mock<IConfiguration>().Object,
            clock.Object,
            task,
            new Mock<IOutputDevice>().Object,
            pipeName,
        ]);

        await handler.BeforeTestHostProcessStartAsync(TestContext.CancellationToken);

        Assert.IsNotNull(task.DeferredFunction);
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;
        using var client = new NamedPipeClientStream(".", pipeNameValue, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(timeout: 5_000, TestContext.CancellationToken);
        Assert.IsTrue(client.IsConnected);
    }

    private sealed class DeferredTask : ITask
    {
        public Func<Task>? DeferredFunction { get; private set; }

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
        {
            DeferredFunction = function;
            return Task.CompletedTask;
        }

        public Task Run(Action action) => throw new NotSupportedException();

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => Task.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => Task.Delay(timeSpan, cancellationToken);
    }
}
