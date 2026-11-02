// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;
using System.Reflection;

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
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
    private const int TestSessionAbortedExitCode = 3;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task OnTestHostProcessExitedAsync_CrashAndAbortGenerateActualReportFromDurablePrefix(bool aborted, bool hasResults)
    {
        using RecoveryFixture fixture = new();
        await DeliverRequestAsync(fixture.Handler, "TestAdapterInformationRequest", ["fixture.adapter", "1.2.3"]);
        if (hasResults)
        {
            fixture.WriteSidecar();
            await DeliverRequestAsync(fixture.Handler, "TrxStreamLocationRequest", [fixture.SidecarPath]);
        }

        var process = new Mock<ITestHostProcessInformation>();
        process.SetupGet(value => value.PID).Returns(123);
        process.SetupGet(value => value.ExitCode).Returns(aborted ? TestSessionAbortedExitCode : 17);

        await fixture.Handler.OnTestHostProcessExitedAsync(process.Object, TestContext.CancellationToken);

        FileArtifact published = Assert.ContainsSingle(fixture.Published);
        Assert.AreEqual(TrxReportEngine.TrxArtifactKind, published.Kind);
        Assert.AreEqual(Path.Combine(fixture.Root, "recovered.trx"), published.FileInfo.FullName);
        var report = XDocument.Load(published.FileInfo.FullName);
        XNamespace ns = report.Root!.Name.Namespace;
        XElement summary = report.Root.Element(ns + "ResultSummary")!;
        Assert.AreEqual("Failed", summary.Attribute("outcome")!.Value);
        XElement counters = summary.Element(ns + "Counters")!;
        Assert.AreEqual(hasResults ? "1" : "0", counters.Attribute("total")!.Value);
        Assert.AreEqual(hasResults ? "1" : "0", counters.Attribute("passed")!.Value);
        Assert.AreEqual("0", counters.Attribute("failed")!.Value);
        if (hasResults)
        {
            XElement result = Assert.ContainsSingle(report.Descendants(ns + "UnitTestResult"));
            Assert.AreEqual("durably recorded", result.Attribute("testName")!.Value);
            Assert.AreEqual("Passed", result.Attribute("outcome")!.Value);
        }
        else
        {
            Assert.IsEmpty(report.Descendants(ns + "UnitTestResult"));
        }

        string crashInfo = Assert.ContainsSingle(report.Descendants(ns + "RunInfo")).Element(ns + "Text")!.Value;
        Assert.AreEqual(
            aborted ? "Test host process pid: 123 was terminated because the test session was aborted." : "Test host process pid: 123 crashed.",
            crashInfo);
        string warning = Assert.ContainsSingle(fixture.Warnings);
        Assert.Contains(aborted ? "Test session was aborted" : "Test host crashed", warning);
        Assert.Contains(hasResults ? "recovered 1 test result(s)" : "no test results could be recovered", warning);
        Assert.IsFalse(File.Exists(fixture.SidecarPath));
        Assert.AreEqual(TestContext.CancellationToken, Assert.ContainsSingle(fixture.OutputTokens));
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_OverwritesReportAndPublishesOverwriteBeforeEmptyRecoveryWarning()
    {
        using RecoveryFixture fixture = new();
        string path = Path.Combine(fixture.Root, "recovered.trx");
        File.WriteAllText(path, new string('x', 20_000));
        await DeliverRequestAsync(fixture.Handler, "TestAdapterInformationRequest", ["fixture.adapter", "1.2.3"]);

        await fixture.Handler.OnTestHostProcessExitedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken);

        Assert.AreEqual(path, Assert.ContainsSingle(fixture.Published).FileInfo.FullName);
        Assert.HasCount(2, fixture.Warnings);
        Assert.Contains(path, fixture.Warnings[0]);
        Assert.Contains("overwritten", fixture.Warnings[0]);
        Assert.Contains("no test results could be recovered", fixture.Warnings[1]);
        var document = XDocument.Load(path);
        Assert.AreEqual("Failed", document.Root!.Element(document.Root.Name.Namespace + "ResultSummary")!.Attribute("outcome")!.Value);
        Assert.IsLessThan(20_000L, new FileInfo(path).Length, "A full rewrite must remove the old trailing bytes.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OnTestHostProcessExitedAsync_GroupsProducerArtifactsCopiesCollidingNamesAndSurfacesMissingAttachment(bool existingCollectors)
    {
        using RecoveryFixture fixture = new();
        string reportPath = Path.Combine(fixture.Root, "existing.trx");
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var summary = new XElement(ns + "ResultSummary", new XAttribute("outcome", "Completed"));
        if (existingCollectors)
        {
            summary.Add(new XElement(ns + "CollectorDataEntries", new XElement(ns + "Collector", new XAttribute("uri", "original"))));
        }

        new XDocument(
            new XElement(
                ns + "TestRun",
                new XElement(ns + "TestSettings", new XElement(ns + "Deployment", new XAttribute("runDeploymentRoot", "run"))),
                summary)).Save(reportPath);
        string firstDirectory = Path.Combine(fixture.Root, "source1");
        string secondDirectory = Path.Combine(fixture.Root, "source2");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        string first = Path.Combine(firstDirectory, "artifact.txt");
        string second = Path.Combine(secondDirectory, "artifact.txt");
        string missing = Path.Combine(fixture.Root, "missing.txt");
        File.WriteAllText(first, "first producer");
        File.WriteAllText(second, "second producer");
        IDataProducer producer = CreateArtifactProducer("producer.one");
        IDataProducer other = CreateArtifactProducer("producer.two");
        await fixture.Handler.ConsumeAsync(producer, new FileArtifact(new FileInfo(first), "first", "first description"), TestContext.CancellationToken);
        await fixture.Handler.ConsumeAsync(producer, new FileArtifact(new FileInfo(missing), "missing", "missing description"), TestContext.CancellationToken);
        await fixture.Handler.ConsumeAsync(other, new FileArtifact(new FileInfo(second), "second", "second description"), TestContext.CancellationToken);
        await DeliverRequestAsync(fixture.Handler, "TestAdapterInformationRequest", ["fixture.adapter", "1.2.3"]);
        await DeliverRequestAsync(fixture.Handler, "ReportFileNameRequest", [reportPath]);
        fixture.WriteSidecar();
        await DeliverRequestAsync(fixture.Handler, "TrxStreamLocationRequest", [fixture.SidecarPath]);

        await fixture.Handler.OnTestHostProcessExitedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken);

        Assert.AreEqual(reportPath, Assert.ContainsSingle(fixture.Published).FileInfo.FullName);
        var document = XDocument.Load(reportPath);
        XElement actualSummary = document.Root!.Element(ns + "ResultSummary")!;
        Assert.AreEqual("Completed", actualSummary.Attribute("outcome")!.Value);
        XElement[] collectors = actualSummary.Descendants(ns + "Collector").ToArray();
        Assert.HasCount(existingCollectors ? 3 : 2, collectors);
        Assert.HasCount(1, collectors.Where(value => (string?)value.Attribute("uri") == "datacollector://producer.one/1.0.0"));
        Assert.HasCount(1, collectors.Where(value => (string?)value.Attribute("uri") == "datacollector://producer.two/1.0.0"));
        string[] hrefs = document.Descendants(ns + "A").Select(value => value.Attribute("href")!.Value).ToArray();
        Assert.HasCount(2, hrefs);
        Assert.Contains(Path.Combine("fixture-machine", "artifact.txt"), hrefs);
        Assert.Contains(Path.Combine("fixture-machine", "artifact_1.txt"), hrefs);
        string copiedDirectory = Path.Combine(fixture.Root, "run", "In", "fixture-machine");
        Assert.AreEqual("first producer", File.ReadAllText(Path.Combine(copiedDirectory, "artifact.txt")));
        Assert.AreEqual("second producer", File.ReadAllText(Path.Combine(copiedDirectory, "artifact_1.txt")));
        string warning = Assert.ContainsSingle(fixture.Warnings);
        Assert.Contains(missing, warning);
        Assert.Contains("FileNotFoundException", warning);
        XElement runInfo = Assert.ContainsSingle(document.Descendants(ns + "RunInfo"));
        Assert.AreEqual("Warning", runInfo.Attribute("outcome")!.Value);
        Assert.AreEqual(warning, runInfo.Element(ns + "Text")!.Value);
        Assert.IsFalse(File.Exists(fixture.SidecarPath));
    }

    private static IDataProducer CreateArtifactProducer(string uid)
    {
        var producer = new Mock<IDataProducer>();
        producer.SetupGet(value => value.Uid).Returns(uid);
        producer.SetupGet(value => value.Version).Returns("1.0.0");
        producer.SetupGet(value => value.DisplayName).Returns(uid + " display");
        producer.SetupGet(value => value.Description).Returns(uid + " description");
        return producer.Object;
    }

    [TestMethod]
    public async Task OnTestHostProcessStartedAsync_RequiresPreparationAndPreservesWaitFailure()
    {
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler();
        using (handler)
        {
            InvalidOperationException missing = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => handler.OnTestHostProcessStartedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken));
            Assert.Contains("BeforeTestHostProcessStartAsync", missing.Message);
            IOException expected = new("controlled connection failed");
            typeof(TrxProcessLifetimeHandler).GetField("_waitConnectionTask", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(handler, Task.FromException(expected));
            IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
                () => handler.OnTestHostProcessStartedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken));
            Assert.AreSame(expected, actual);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OnTestHostProcessExitedAsync_PublishesOriginalReportAndBestEffortDeletesSidecar(bool deletionFails)
    {
        string reportPath = Path.Combine(Path.GetTempPath(), "recorded-report.trx");
        string sidecarPath = Path.Combine(Path.GetTempPath(), "recorded-sidecar.bin");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.ExistFile(sidecarPath)).Returns(true);
        if (deletionFails)
        {
            fileSystem.Setup(value => value.DeleteFile(sidecarPath)).Throws(new IOException("delete denied"));
        }

        FileArtifact? published = null;
        var bus = new Mock<IMessageBus>();
        bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback((IDataProducer producer, IData data) => published = (FileArtifact)data).Returns(Task.CompletedTask);
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler(fileSystem.Object, bus.Object);
        using (handler)
        {
            await DeliverRequestAsync(handler, "ReportFileNameRequest", [reportPath]);
            await DeliverRequestAsync(handler, "TrxStreamLocationRequest", [sidecarPath]);

            await handler.OnTestHostProcessExitedAsync(Mock.Of<ITestHostProcessInformation>(), TestContext.CancellationToken);

            Assert.AreEqual(reportPath, published!.FileInfo.FullName);
            Assert.AreEqual(TrxReportEngine.TrxArtifactKind, published.Kind);
            fileSystem.Verify(value => value.DeleteFile(sidecarPath), Times.Once);
        }
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_CancellationPreventsPublishingAndFileAccess()
    {
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler(fileSystem.Object, bus.Object);
        using (handler)
        using (CancellationTokenSource cancellation = new())
        {
            cancellation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => handler.OnTestHostProcessExitedAsync(Mock.Of<ITestHostProcessInformation>(), cancellation.Token));
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
            Assert.IsEmpty(fileSystem.Invocations);
            Assert.IsEmpty(bus.Invocations);
        }
    }

    [TestMethod]
    public async Task TryRecoverStreamingResultsAsync_PreservesValidPrefixWhenFinalRecordIsTruncated()
    {
        byte[] bytes;
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            TrxTestResultSerializer.Write(writer, new TrxTestResult { Uid = "first", DisplayName = "recovered first", Outcome = TrxTestOutcome.Passed });
            TrxTestResultSerializer.Write(writer, new TrxTestResult { Uid = "torn", DisplayName = "not durable", Outcome = TrxTestOutcome.Failed });
            bytes = stream.ToArray().Take((int)stream.Length - 3).ToArray();
        }

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.ExistFile("sidecar.bin")).Returns(true);
        using MemoryStream recoveredStream = new(bytes);
        var fileStream = new Mock<IFileStream>();
        fileStream.SetupGet(value => value.Stream).Returns(recoveredStream);
        fileSystem.Setup(value => value.NewFileStream("sidecar.bin", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)).Returns(fileStream.Object);
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler(fileSystem.Object);
        using (handler)
        {
            await DeliverRequestAsync(handler, "TrxStreamLocationRequest", ["sidecar.bin"]);
            IReadOnlyList<TrxTestResult> results = await RecoverAsync(handler);
            TrxTestResult result = Assert.ContainsSingle(results);
            Assert.AreEqual("first", result.Uid);
            Assert.AreEqual("recovered first", result.DisplayName);
            Assert.AreEqual(TrxTestOutcome.Passed, result.Outcome);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TryRecoverStreamingResultsAsync_MissingOrUnreadableSidecarDoesNotBlockRecovery(bool unreadable)
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.ExistFile("sidecar.bin")).Returns(unreadable);
        fileSystem.Setup(value => value.NewFileStream("sidecar.bin", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Throws(new IOException("read denied"));
        (TrxProcessLifetimeHandler handler, _, _, _) = CreateHandler(fileSystem.Object);
        using (handler)
        {
            Assert.IsEmpty(await RecoverAsync(handler), "No reported streaming location must not access a file.");
            await DeliverRequestAsync(handler, "TrxStreamLocationRequest", ["sidecar.bin"]);
            Assert.IsEmpty(await RecoverAsync(handler));
            fileSystem.Verify(value => value.NewFileStream("sidecar.bin", FileMode.Open, FileAccess.Read, FileShare.ReadWrite), unreadable ? Times.Once : Times.Never);
        }
    }

    private static async Task DeliverRequestAsync(TrxProcessLifetimeHandler handler, string requestName, object[] arguments)
    {
        Type requestType = typeof(TrxProcessLifetimeHandler).Assembly.GetTypes().Single(type => type.Name == requestName);
        object request = Activator.CreateInstance(requestType, arguments)!;
        await (Task)typeof(TrxProcessLifetimeHandler).GetMethod("CallbackAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(handler, [request])!;
    }

    private static async Task<IReadOnlyList<TrxTestResult>> RecoverAsync(TrxProcessLifetimeHandler handler)
        => await (Task<IReadOnlyList<TrxTestResult>>)typeof(TrxProcessLifetimeHandler).GetMethod("TryRecoverStreamingResultsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, [CancellationToken.None])!;

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
        IFileSystem? fileSystem = null, IMessageBus? messageBus = null, IEnvironment? customEnvironment = null,
        ITestApplicationModuleInfo? module = null, IConfiguration? configuration = null, IClock? customClock = null,
        IOutputDevice? output = null, ICommandLineOptions? options = null)
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
            options ?? commandLineOptions,
            customEnvironment ?? environment.Object,
            loggerFactory.Object,
            messageBus ?? new Mock<IMessageBus>().Object,
            fileSystem ?? new Mock<IFileSystem>().Object,
            module ?? new Mock<ITestApplicationModuleInfo>().Object,
            configuration ?? new Mock<IConfiguration>().Object,
            customClock ?? clock.Object,
            task,
            output ?? new Mock<IOutputDevice>().Object,
            serviceProvider,
            endpoint,
        ]);

        return (handler, endpoint, serviceProvider, task);
    }

    private sealed class RecoveryFixture : IDisposable
    {
        public RecoveryFixture()
        {
            Directory.CreateDirectory(Root);
            var environment = new Mock<IEnvironment>();
            environment.SetupGet(value => value.MachineName).Returns("fixture-machine");
            environment.Setup(value => value.GetEnvironmentVariable("UserName")).Returns("fixture-user");
            var module = new Mock<ITestApplicationModuleInfo>();
            module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(Root, "fixture.dll"));
            var configuration = new Mock<IConfiguration>();
            configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(Root);
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
            var bus = new Mock<IMessageBus>();
            bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, value) => Published.Add((FileArtifact)value)).Returns(Task.CompletedTask);
            var output = new Mock<IOutputDevice>();
            output.Setup(value => value.DisplayAsync(It.IsAny<Microsoft.Testing.Platform.Extensions.OutputDevice.IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Callback<Microsoft.Testing.Platform.Extensions.OutputDevice.IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, value, token) =>
                {
                    Warnings.Add(((WarningMessageOutputDeviceData)value).Message);
                    OutputTokens.Add(token);
                }).Returns(Task.CompletedTask);
            var options = new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                [TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
                [TrxReportGeneratorCommandLine.TrxReportFileNameOptionName] = ["recovered.trx"],
            });
            (Handler, _, _, _) = CreateHandler(new SystemFileSystem(), bus.Object, environment.Object, module.Object, configuration.Object, clock.Object, output.Object, options);
        }

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "trx-controller-coverage-" + Guid.NewGuid().ToString("N"));

        public string SidecarPath => Path.Combine(Root, "sidecar.bin");

        public TrxProcessLifetimeHandler Handler { get; }

        public List<FileArtifact> Published { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<CancellationToken> OutputTokens { get; } = [];

        public void WriteSidecar()
        {
            using FileStream stream = File.Create(SidecarPath);
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            TrxTestResultSerializer.Write(writer, new TrxTestResult { Uid = "durable", DisplayName = "durably recorded", Outcome = TrxTestOutcome.Passed, TrxFullyQualifiedTypeName = "Fixture.Results" });
            TrxTestResultSerializer.Write(writer, new TrxTestResult { Uid = "torn", DisplayName = "not durable", Outcome = TrxTestOutcome.Failed, TrxFullyQualifiedTypeName = "Fixture.Results" });
            stream.SetLength(stream.Length - 3);
        }

        public void Dispose()
        {
            Handler.Dispose();
            Directory.Delete(Root, recursive: true);
        }
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
