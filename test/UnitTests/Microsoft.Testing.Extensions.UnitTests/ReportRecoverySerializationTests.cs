// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportRecoverySerializationTests
{
    [TestMethod]
    public void ReportJournalConfiguration_RelativeResultsDirectory_UsesCreatedFullPath()
    {
        const string relativeDirectory = "relative-results";
        string fullDirectory = Path.GetFullPath(relativeDirectory);
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns(relativeDirectory);
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.CreateDirectory(relativeDirectory)).Returns(fullDirectory);
        Type journalConfigurationType = typeof(CtrfReportEngine).Assembly
            .GetType("Microsoft.Testing.Extensions.ReportJournalConfiguration", throwOnError: true)!;
        object journalConfiguration = Activator.CreateInstance(journalConfigurationType, "TEST_REPORT_JOURNAL")!;

        string path = (string)journalConfigurationType
            .GetMethod("GetOrCreatePath", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(journalConfiguration, [configuration.Object, fileSystem.Object])!;

        Assert.AreEqual(fullDirectory, Path.GetDirectoryName(path));
        Assert.EndsWith(".jsonl", path, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(typeof(HtmlReportEngine), "Microsoft.Testing.Extensions.HtmlReport.HtmlReportGenerator", "Outcome", "passed")]
    [DataRow(typeof(JUnitReportEngine), "Microsoft.Testing.Extensions.JUnitReport.JUnitReportGenerator", "Outcome", "passed")]
    [DataRow(typeof(CtrfReportEngine), "Microsoft.Testing.Extensions.CtrfReport.CtrfReportGenerator", "Status", "passed")]
    public void DeserializeJournalRecord_UsesGeneratedMetadata(
        Type assemblyMarker,
        string generatorTypeName,
        string formatPropertyName,
        string formatPropertyValue)
    {
        Type generatorType = assemblyMarker.Assembly.GetType(generatorTypeName, throwOnError: true)!;
        MethodInfo deserialize = generatorType.GetMethod(
            "DeserializeJournalRecord",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        string formatSpecificProperties = generatorTypeName.Contains("JUnit", StringComparison.Ordinal)
            ? $"""
              "RawUid":"test-1","{formatPropertyName}":"{formatPropertyValue}"
              """
            : $"""
              "{formatPropertyName}":"{formatPropertyValue}"
              """;
        string json = $$"""
            {
              "Type": 1,
              "Result": {
                "Uid": "test-1",
                "DisplayName": "Recovered test",
                {{formatSpecificProperties}}
              }
            }
            """;

        object record = deserialize.Invoke(null, [json])!;
        object result = record.GetType().GetProperty("Result")!.GetValue(record)!;

        Assert.AreEqual("Recovered test", result.GetType().GetProperty("DisplayName")!.GetValue(result));
        Assert.AreEqual(formatPropertyValue, result.GetType().GetProperty(formatPropertyName)!.GetValue(result));
    }

    [TestMethod]
    public void ReadJournal_TruncatedTail_ReturnsValidPrefix()
    {
        const string path = "journal.jsonl";
        const string journal = """
            {"Type":0,"StartTime":"2026-08-31T12:00:00+00:00","ProcessId":42,"FrameworkUid":"framework","FrameworkVersion":"1.0","FrameworkDisplayName":"Framework"}
            {"Type":1,"Result":{"Uid":"test-1","DisplayName":"Recovered test","Status":"passed"}}
            {"Type":1,"Result":{"Uid":"truncated"
            """;
        var fileSystem = new Mock<IFileSystem>();
        _ = fileSystem
            .Setup(fs => fs.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(() => new MemoryFileStream(journal));
        object handler = CreateHandler(typeof(CtrfReportEngine), fileSystem.Object, out _, out _);

        MethodInfo readJournal = handler.GetType().GetMethod("ReadJournal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object recovered = readJournal.Invoke(handler, [path, new TestHostProcessInformation(314, 1, hasExitedGracefully: false)])!;
        var results = (System.Collections.IEnumerable)recovered.GetType().GetProperty("Results")!.GetValue(recovered)!;
        object[] recoveredResults = [.. results.Cast<object>()];

        Assert.HasCount(1, recoveredResults);
        Assert.AreEqual("Recovered test", recoveredResults[0].GetType().GetProperty("DisplayName")!.GetValue(recoveredResults[0]));
        object metadata = recovered.GetType().GetProperty("Metadata")!.GetValue(recovered)!;
        Assert.AreEqual(314, metadata.GetType().GetProperty("ProcessId")!.GetValue(metadata));
        Assert.IsTrue((bool)metadata.GetType().GetProperty("IsIncomplete")!.GetValue(metadata)!);
        Assert.IsTrue((bool)recovered.GetType().GetProperty("IsPartial")!.GetValue(recovered)!);
        Assert.IsFalse((bool)recovered.GetType().GetProperty("Completed")!.GetValue(recovered)!);
    }

    [TestMethod]
    public void ReadJournal_OversizedRecord_StopsAtBoundedPrefix()
    {
        const string path = "journal.jsonl";
        Type handlerOpenType = typeof(CtrfReportEngine).Assembly
            .GetType("Microsoft.Testing.Extensions.ReportProcessLifetimeHandler`2", throwOnError: true)!;
        Type handlerType = handlerOpenType.MakeGenericType(
            typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.CtrfReport.CtrfReportGenerator", true)!,
            typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.CtrfReport.CapturedTestResult", true)!);
        int maxRecordBytes = (int)handlerType.GetField("MaxJournalRecordBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        string journal = """
            {"Type":1,"Result":{"Uid":"test-1","DisplayName":"Recovered test","Status":"passed"}}
            """
            + "\n"
            + new string('x', maxRecordBytes + 1);
        var fileSystem = new Mock<IFileSystem>();
        _ = fileSystem
            .Setup(fs => fs.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(() => new MemoryFileStream(journal));
        object handler = CreateHandler(typeof(CtrfReportEngine), fileSystem.Object, out _, out _);

        MethodInfo readJournal = handler.GetType().GetMethod("ReadJournal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object recovered = readJournal.Invoke(handler, [path, new TestHostProcessInformation(314, 1, hasExitedGracefully: false)])!;
        var results = (System.Collections.IEnumerable)recovered.GetType().GetProperty("Results")!.GetValue(recovered)!;

        Assert.HasCount(1, results.Cast<object>());
        Assert.IsTrue((bool)recovered.GetType().GetProperty("IsPartial")!.GetValue(recovered)!);
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_CompletionRecord_RegeneratesAndPublishesReportAsync()
    {
        const string journal = """
            {"Type":0,"StartTime":"2026-08-31T12:00:00+00:00","ProcessId":42,"FrameworkUid":"framework","FrameworkVersion":"1.0","FrameworkDisplayName":"Framework"}
            {"Type":2}

            """;
        var fileSystem = new Mock<IFileSystem>();
        _ = fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>()))
            .Returns<string>(path => path.EndsWith(".jsonl", StringComparison.Ordinal));
        _ = fileSystem
            .Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(() => new MemoryFileStream(journal));
        _ = fileSystem
            .Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns(() => new WritableMemoryFileStream());
        object handler = CreateHandler(
            typeof(CtrfReportEngine),
            fileSystem.Object,
            out Mock<IMessageBus> messageBus,
            out Mock<IOutputDevice> outputDevice);

        await ((ITestHostProcessLifetimeHandler)handler).OnTestHostProcessExitedAsync(
            new TestHostProcessInformation(314, 1, hasExitedGracefully: false),
            CancellationToken.None);

        messageBus.Verify(
            bus => bus.PublishAsync(
                It.IsAny<IDataProducer>(),
                It.Is<FileArtifact>(artifact => artifact.Kind == "microsoft.testing.ctrf")),
            Times.Once);
        outputDevice.Verify(
            device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.Is<WarningMessageOutputDeviceData>(message =>
                    message.Message.Contains("before report artifact delivery was confirmed", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_GracefulExit_OnlyDeletesJournalAsync()
    {
        var fileSystem = new Mock<IFileSystem>();
        _ = fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        object handler = CreateHandler(typeof(CtrfReportEngine), fileSystem.Object, out Mock<IMessageBus> messageBus, out _);

        await ((ITestHostProcessLifetimeHandler)handler).OnTestHostProcessExitedAsync(
            new TestHostProcessInformation(314, 0, hasExitedGracefully: true),
            CancellationToken.None);

        fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
        fileSystem.Verify(
            fs => fs.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()),
            Times.Never);
        messageBus.Verify(bus => bus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()), Times.Never);
    }

    private static object CreateHandler(
        Type assemblyMarker,
        IFileSystem fileSystem,
        out Mock<IMessageBus> messageBus,
        out Mock<IOutputDevice> outputDevice)
    {
        Assembly assembly = assemblyMarker.Assembly;
        Type generatorType = assembly.GetType("Microsoft.Testing.Extensions.CtrfReport.CtrfReportGenerator", throwOnError: true)!;
        Type capturedResultType = assembly.GetType("Microsoft.Testing.Extensions.CtrfReport.CapturedTestResult", throwOnError: true)!;
        Type handlerType = assembly.GetType("Microsoft.Testing.Extensions.ReportProcessLifetimeHandler`2", throwOnError: true)!
            .MakeGenericType(generatorType, capturedResultType);
        Type journalConfigurationType = assembly.GetType("Microsoft.Testing.Extensions.ReportJournalConfiguration", throwOnError: true)!;
        object journalConfiguration = Activator.CreateInstance(journalConfigurationType, "TEST_REPORT_JOURNAL")!;

        var configuration = new Mock<IConfiguration>();
        _ = configuration.SetupGet(c => c[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("results");
        messageBus = new Mock<IMessageBus>();
        _ = messageBus
            .Setup(bus => bus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Returns(Task.CompletedTask);
        outputDevice = new Mock<IOutputDevice>();
        _ = outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var loggerFactory = new Mock<ILoggerFactory>();
        _ = loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(new TestCommandLineOptions(new() { ["report"] = [] }));
        serviceProvider.AddService(configuration.Object);
        serviceProvider.AddService(fileSystem);
        serviceProvider.AddService(messageBus.Object);
        serviceProvider.AddService(outputDevice.Object);
        serviceProvider.AddService(Mock.Of<IClock>(clock => clock.UtcNow == DateTimeOffset.UtcNow));
        serviceProvider.AddService(Mock.Of<IEnvironment>(environment => environment.MachineName == "machine"));
        serviceProvider.AddService(Mock.Of<ITestApplicationModuleInfo>(
            module => module.GetCurrentTestApplicationFullPath() == "testhost.dll"));
        serviceProvider.AddService(Mock.Of<ITestApplicationProcessExitCode>(
            exitCode => exitCode.GetProcessExitCode() == 1));
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());

        MethodInfo deserializeMethod = generatorType.GetMethod(
            "DeserializeJournalRecord",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Type deserializerType = handlerType.GetConstructors().Single().GetParameters()[4].ParameterType;
        var deserializer = Delegate.CreateDelegate(deserializerType, deserializeMethod);
        Type factoryType = handlerType.GetConstructors().Single().GetParameters()[3].ParameterType;
        Type[] factoryArguments = factoryType.GetGenericArguments();
        ParameterExpression serviceProviderParameter = Expression.Parameter(factoryArguments[0], "serviceProvider");
        ParameterExpression metadataParameter = Expression.Parameter(factoryArguments[1], "metadata");
        ConstructorInfo recoveredConstructor = generatorType
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 2);
        Delegate generatorFactory = Expression.Lambda(
            factoryType,
            Expression.New(recoveredConstructor, serviceProviderParameter, metadataParameter),
            serviceProviderParameter,
            metadataParameter).Compile();

        return Activator.CreateInstance(
            handlerType,
            serviceProvider,
            "report",
            journalConfiguration,
            generatorFactory,
            deserializer)!;
    }

    private sealed class TestHostProcessInformation(int pid, int exitCode, bool hasExitedGracefully) : ITestHostProcessInformation
    {
        public int PID { get; } = pid;

        public int ExitCode { get; } = exitCode;

        public bool HasExitedGracefully { get; } = hasExitedGracefully;
    }

    private sealed class MemoryFileStream(string content) : IFileStream
    {
        private readonly MemoryStream _stream = new(Encoding.UTF8.GetBytes(content));

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif
    }

    private sealed class WritableMemoryFileStream : IFileStream
    {
        private readonly MemoryStream _stream = new();

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif
    }
}
