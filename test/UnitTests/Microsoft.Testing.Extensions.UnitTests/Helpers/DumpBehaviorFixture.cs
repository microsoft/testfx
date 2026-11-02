// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class DumpBehaviorFixture : IDisposable
{
    internal const string JournalHeader = "# MTP CrashDump test sequence v1 (format: <event>\\t<isoTimestamp>\\t<uid>\\t<displayName-or-state>)";

    public DumpBehaviorFixture()
    {
        EnvironmentMock
            .Setup(x => x.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns((string name) => EnvironmentValues.TryGetValue(name, out string? value) ? value : null);
        Clock.SetupGet(x => x.UtcNow).Returns(() => Now);
        Logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        Logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, exception, _) => Logs.Add((level, message, exception)))
            .Returns(Task.CompletedTask);
        LoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
        Output
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (producer, data, token) => Displays.Add((producer, data, token)))
            .Returns(Task.CompletedTask);
        MessageBus
            .Setup(x => x.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((producer, data) => Publications.Add((producer, data)))
            .Returns(Task.CompletedTask);
        Configuration.SetupGet(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(RootDirectory);
        Scheduler
            .Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> callback, CancellationToken _) => callback());
        Scheduler.Setup(x => x.WhenAll(It.IsAny<Task[]>())).Returns((Task[] tasks) => Task.WhenAll(tasks));

        Services.AddServices(
        [
            new TestCommandLineOptions(Options),
            EnvironmentMock.Object,
            Clock.Object,
            LoggerFactory.Object,
            Output.Object,
            MessageBus.Object,
            Configuration.Object,
            Scheduler.Object,
            ProcessHandler.Object,
        ]);
    }

    public string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), $"mtp-dump-behavior-{Guid.NewGuid():N}");

    public Dictionary<string, string[]> Options { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, string?> EnvironmentValues { get; } = [with(StringComparer.Ordinal)];

    public DateTimeOffset Now { get; set; } = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    public Mock<IEnvironment> EnvironmentMock { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public Mock<ILogger> Logger { get; } = new();

    public Mock<ILoggerFactory> LoggerFactory { get; } = new();

    public Mock<IOutputDevice> Output { get; } = new();

    public Mock<IMessageBus> MessageBus { get; } = new();

    public Mock<IConfiguration> Configuration { get; } = new();

    public Mock<ITask> Scheduler { get; } = new();

    public Mock<IProcessHandler> ProcessHandler { get; } = new();

    public ServiceProvider Services { get; } = new();

    public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

    public List<(IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token)> Displays { get; } = [];

    public List<(IDataProducer Producer, IData Data)> Publications { get; } = [];

    public HangDumpProcessLifetimeHandler CreateHangHandler()
        => new(
            new NamedPipeServerEndpoint($"unused-{Guid.NewGuid():N}"),
            MessageBus.Object,
            Output.Object,
            new TestCommandLineOptions(Options),
            Scheduler.Object,
            EnvironmentMock.Object,
            LoggerFactory.Object,
            Configuration.Object,
            ProcessHandler.Object,
            Clock.Object,
            Services);

    public HangDumpActivityIndicator CreateActivityIndicator()
        => new(
            new TestCommandLineOptions(Options),
            EnvironmentMock.Object,
            Scheduler.Object,
            LoggerFactory.Object,
            Clock.Object);

    public CrashDumpSequenceLogger CreateSequenceLogger()
        => new(EnvironmentMock.Object, Clock.Object, LoggerFactory.Object, Output.Object);

    public CrashDumpProcessLifetimeHandler CreateCrashHandler(string? sequencePath)
    {
        Options[CrashDumpCommandLineOptions.CrashDumpOptionName] = [];
        return new(
            new TestCommandLineOptions(Options),
            MessageBus.Object,
            Output.Object,
            new CrashDumpConfiguration
            {
                // This directory deliberately does not exist: no real or synthetic dump is needed.
                DumpFileNamePattern = Path.Combine(RootDirectory, "missing-dumps", "host_%p.dmp"),
                SequenceFileName = sequencePath,
            });
    }

    public string WriteJournal(params string[] lines)
    {
        Directory.CreateDirectory(RootDirectory);
        string path = Path.Combine(RootDirectory, "tests.sequence.log");
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }

    public static TestNodeUpdateMessage Update(string uid, string displayName, params IProperty[] properties)
        => new(
            new SessionUid("dump-behavior-session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = displayName,
                Properties = new PropertyBag(properties),
            });

    public void Dispose()
    {
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
