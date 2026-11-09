// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;
using System.Reflection;

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxProcessLifetimeHandlerTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OnTestHostProcessExitedAsync_RegistersReportKindWithRetryOnlyWhenConfigured(bool retryEnabled)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string reportPath = Path.Combine(root, "report.trx");
        string manifestPath = Path.Combine(root, "artifacts.manifest");
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"))
            .Returns(retryEnabled ? manifestPath : null);
        using MemoryStream manifest = new();
        var fileStream = new Mock<IFileStream>();
        fileStream.SetupGet(value => value.Stream).Returns(manifest);
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.NewFileStream(manifestPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns(fileStream.Object);
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler(fileSystem.Object, environment.Object);
        using (handler)
        {
            Type requestType = typeof(TrxProcessLifetimeHandler).Assembly.GetTypes().Single(type => type.Name == "ReportFileNameRequest");
            object request = Activator.CreateInstance(requestType, [reportPath])!;
            await (Task)typeof(TrxProcessLifetimeHandler).GetMethod("CallbackAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(handler, [request])!;
            await handler.OnTestHostProcessExitedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken);
        }

        if (retryEnabled)
        {
            string[] entry = Encoding.UTF8.GetString(manifest.ToArray()).TrimEnd('\r', '\n').Split('\t');
            Assert.HasCount(2, entry);
            Assert.AreEqual(reportPath, Encoding.UTF8.GetString(Convert.FromBase64String(entry[0])));
            Assert.AreEqual(TrxReportEngine.TrxArtifactKind, Encoding.UTF8.GetString(Convert.FromBase64String(entry[1])));
        }
        else
        {
            Assert.AreEqual(0, manifest.Length);
            fileSystem.Verify(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()), Times.Never);
        }
    }

    [TestMethod]
    public async Task BeforeTestHostProcessStartAsync_CreatesPipeBeforeSchedulingConnectionWait()
    {
        (TrxProcessLifetimeHandler handler, NamedPipeServerEndpoint endpoint, _, DeferredTask task) = CreateHandler();
        using (handler)
        {
            await handler.BeforeTestHostProcessStartAsync(TestContext.CancellationToken);

            Assert.IsNotNull(task.DeferredFunction);
            using var client = new NamedPipeClientStream(".", endpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeout: 5_000, TestContext.CancellationToken);
            Assert.IsTrue(client.IsConnected);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public async Task BeforeTestHostProcessStartAsync_UsesRuntimeAuthorizationAndPublishesEffectivePipeName()
    {
        (TrxProcessLifetimeHandler handler, NamedPipeServerEndpoint endpoint, ServiceProvider serviceProvider, DeferredTask task) = CreateHandler();
        using (handler)
        {
            serviceProvider.TestHostControllerAuthorizedSecurityIdentities = [ContosoPackageSid];

            await handler.BeforeTestHostProcessStartAsync(TestContext.CancellationToken);

            Assert.IsTrue(endpoint.PipeName.StartsWith(@"LOCAL\", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(task.DeferredFunction);
        }
    }

    private static (TrxProcessLifetimeHandler Handler, NamedPipeServerEndpoint Endpoint, ServiceProvider ServiceProvider, DeferredTask Task) CreateHandler(
        IFileSystem? customFileSystem = null,
        IEnvironment? customEnvironment = null)
    {
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
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
        string pipeNameValue = (string)pipeName.GetType().GetProperty("Name")!.GetValue(pipeName)!;
        var endpoint = new NamedPipeServerEndpoint(pipeNameValue);
        ServiceProvider serviceProvider = new();
        ConstructorInfo constructor = typeof(TrxProcessLifetimeHandler).GetConstructors().Single();

        var handler = (TrxProcessLifetimeHandler)constructor.Invoke([
            commandLineOptions,
            customEnvironment ?? environment.Object,
            loggerFactory.Object,
            new Mock<IMessageBus>().Object,
            customFileSystem ?? new Mock<IFileSystem>().Object,
            new Mock<ITestApplicationModuleInfo>().Object,
            new Mock<IConfiguration>().Object,
            clock.Object,
            task,
            new Mock<IOutputDevice>().Object,
            serviceProvider,
            endpoint,
        ]);

        return (handler, endpoint, serviceProvider, task);
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
