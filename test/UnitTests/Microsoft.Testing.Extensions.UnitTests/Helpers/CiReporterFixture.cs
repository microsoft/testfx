// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

/// <summary>Per-case services and an injected, snapshot-based filesystem. No process or timer is started here.</summary>
internal sealed class CiReporterFixture : IDisposable
{
    public static readonly DateTimeOffset Epoch = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    public CiReporterFixture()
    {
        Configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(() => ResultsDirectory);
        Environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => Variables.TryGetValue(name, out string? value) ? value : null);
        Environment.Setup(value => value.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string?>((name, value) => Variables[name] = value);
        Environment.SetupGet(value => value.ProcessId).Returns(42);
        Environment.SetupGet(value => value.MachineName).Returns("owned-agent");
        Clock.SetupGet(value => value.UtcNow).Returns(() => Now);
        Module.Setup(value => value.TryGetAssemblyName()).Returns("Reporter.Tests");
        Module.Setup(value => value.GetDisplayName()).Returns("Fallback.Tests");
        Module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(Root, "Reporter.Tests.dll"));
        Coverage.SetupGet(value => value.Scopes).Returns([]);
        Coverage.SetupGet(value => value.Thresholds).Returns([]);
        Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        Logger.Setup(value => value.Log(
                It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, state, exception, formatter) =>
            {
                Logs.Add((level, formatter(state, exception)));
                if (LogError is not null)
                {
                    throw LogError;
                }
            });
        Logger.Setup(value => value.LogAsync(
                It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, state, exception, formatter) =>
            {
                Logs.Add((level, formatter(state, exception)));
                return LogError is null ? Task.CompletedTask : Task.FromException(LogError);
            });
        LoggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
        Output.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((owner, data, token) =>
            {
                DisplayAttempts.Add((owner, data, token));
                Exception? error = DisplayError?.Invoke(data);
                if (error is not null)
                {
                    return Task.FromException(error);
                }

                Displays.Add(data);
                return Task.CompletedTask;
            });
        Bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((_, data) => Publications.Add(data))
            .Returns(Task.CompletedTask);
        FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns<string>(path => Files.ContainsKey(path));
        FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string?>())).Returns<string?>(path => path is not null && Directories.Contains(path));
        FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                Directories.Add(path);
                return path;
            });
        FileSystem.Setup(value => value.ReadAllText(It.IsAny<string>())).Returns<string>(ReadText);
        FileSystem.Setup(value => value.ReadAllTextAsync(It.IsAny<string>())).Returns<string>(path => Task.FromResult(ReadText(path)));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns<string, FileMode, FileAccess, FileShare>(Open);
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()))
            .Returns<string, FileMode, FileAccess>((path, mode, access) => Open(path, mode, access, FileShare.None));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
            .Returns<string, FileMode>((path, mode) => Open(path, mode, FileAccess.ReadWrite, FileShare.None));
        FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>())).Callback<string>(path =>
        {
            BeforeDelete?.Invoke(path);
            Files.Remove(path);
            DeletedPaths.Add(path);
        });
        FileSystem.Setup(value => value.ReplaceFile(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((source, destination) =>
            {
                BeforeReplace?.Invoke(source, destination);
                Files[destination] = Files[source];
                Files.Remove(source);
                Replacements.Add(destination);
            });
        FileSystem.Setup(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((source, destination, _) =>
            {
                Files[destination] = Files[source];
                Files.Remove(source);
            });
        FileSystem.Setup(value => value.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns<string, string, SearchOption>((directory, pattern, _) =>
            {
                string prefix = pattern.Split('*')[0];
                string suffix = pattern.Split('*').Last();
                return [.. Files.Keys.Where(path => Path.GetDirectoryName(path) == directory
                    && Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal)
                    && Path.GetFileName(path).EndsWith(suffix, StringComparison.Ordinal))];
            });
        TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((action, name, token) =>
            {
                ScheduledAction = action;
                ScheduledName = name;
                ScheduledToken = token;
            })
            .Returns(() => ScheduledTask);
        TaskService.Setup(value => value.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<TimeSpan, CancellationToken>((delay, token) =>
            {
                Delays.Add(delay);
                return Delay?.Invoke(delay, token) ?? new TaskCompletionSource<object?>().Task;
            });
        Client.Setup(value => value.CreateTestRunAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<CancellationToken>())).ReturnsAsync(77);
        Client.Setup(value => value.PublishTestResultsWithSubResultsAsync(
                It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken>((_, _, results, _) =>
                Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([.. results.Select((_, index) => new AzureDevOpsPublishedTestResult(101 + index, new Dictionary<int, int>()))]));
        Client.Setup(value => value.UpdateTestResultsWithSubResultsAsync(
                It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken>((_, _, results, _) =>
                Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([.. results.Select(result => new AzureDevOpsPublishedTestResult(
                    result.Id!.Value, (result.SubResults ?? []).ToDictionary(attempt => attempt.SequenceId, attempt => 1000 + attempt.SequenceId)))]));
        Client.Setup(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Client.Setup(value => value.UploadTestResultAttachmentAsync(
                It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<AzureDevOpsTestResultAttachment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Client.Setup(value => value.UploadTestRunAttachmentAsync(
                It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<AzureDevOpsTestResultAttachment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Services.AllowTestAdapterFrameworkRegistration = true;
        Services.AddServices(
        [
            new TestCommandLineOptions(Options), Configuration.Object, Environment.Object, FileSystem.Object, Clock.Object,
            Module.Object, Coverage.Object, ExitCode.Object, LoggerFactory.Object, Output.Object, Bus.Object, TaskService.Object,
        ]);
    }

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ci-reporter-" + Guid.NewGuid().ToString("N"));

    public string? ResultsDirectory { get; set; }

    public DateTimeOffset Now { get; set; } = Epoch;

    public Dictionary<string, string[]> Options { get; } = [with(StringComparer.OrdinalIgnoreCase)];

    public Dictionary<string, string?> Variables { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, byte[]> Files { get; } = [with(StringComparer.Ordinal)];

    public HashSet<string> Directories { get; } = [with(StringComparer.Ordinal)];

    public List<(string Path, FileMode Mode, FileAccess Access, FileShare Share)> Opens { get; } = [];

    public List<string> DeletedPaths { get; } = [];

    public List<string> Replacements { get; } = [];

    public List<(LogLevel Level, string Message)> Logs { get; } = [];

    public string LogText => string.Join("\n", Logs.Select(entry => entry.Message));

    public List<(IOutputDeviceDataProducer Owner, IOutputDeviceData Data, CancellationToken Token)> DisplayAttempts { get; } = [];

    public List<IOutputDeviceData> Displays { get; } = [];

    public List<IData> Publications { get; } = [];

    public List<TimeSpan> Delays { get; } = [];

    public Exception? LogError { get; set; }

    public Func<IOutputDeviceData, Exception?>? DisplayError { get; set; }

    public Action<string, FileMode, FileAccess, FileShare>? BeforeOpen { get; set; }

    public Action<string>? BeforeDelete { get; set; }

    public Action<string, string>? BeforeReplace { get; set; }

    public Func<TimeSpan, CancellationToken, Task>? Delay { get; set; }

    public Func<Task>? ScheduledAction { get; private set; }

    public string? ScheduledName { get; private set; }

    public CancellationToken ScheduledToken { get; private set; }

    public Task ScheduledTask { get; set; } = Task.CompletedTask;

    public ServiceProvider Services { get; } = new();

    public Mock<IConfiguration> Configuration { get; } = new();

    public Mock<IEnvironment> Environment { get; } = new();

    public Mock<IFileSystem> FileSystem { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public Mock<ITestApplicationModuleInfo> Module { get; } = new();

    public Mock<ITestCoverageResult> Coverage { get; } = new();

    public Mock<ITestApplicationProcessExitCode> ExitCode { get; } = new();

    public Mock<ILogger> Logger { get; } = new();

    public Mock<ILoggerFactory> LoggerFactory { get; } = new();

    public Mock<IOutputDevice> Output { get; } = new();

    public Mock<IMessageBus> Bus { get; } = new();

    public Mock<ITask> TaskService { get; } = new();

    public Mock<IAzureDevOpsTestResultsClient> Client { get; } = new();

    public static TestNodeUpdateMessage Update(string uid, params IProperty[] properties)
        => UpdateNamed(uid, uid, properties);

    public static TestNodeUpdateMessage UpdateNamed(string uid, string displayName, params IProperty[] properties)
        => new(new SessionUid("ci-session"), new TestNode
        {
            Uid = uid,
            DisplayName = displayName,
            // PropertyBag prepends non-state entries. Keep the caller's declared order observable.
            Properties = new PropertyBag([new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", uid), .. properties.AsEnumerable().Reverse()]),
        });

    public void EnableAzureDevOps()
    {
        ResultsDirectory = Root;
        Variables["TF_BUILD"] = "true";
        Variables["SYSTEM_COLLECTIONURI"] = "https://ci.invalid/org/";
        Variables["SYSTEM_TEAMPROJECT"] = "project";
        Variables["SYSTEM_ACCESSTOKEN"] = "owned-token";
        Variables["BUILD_BUILDID"] = "123";
        Variables["BUILD_DEFINITIONID"] = "17";
        Options[AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [];
    }

    public AzureDevOpsPublishConfiguration PublishConfiguration()
        => new("https://ci.invalid/org/", "project", "owned-token", 123, "Owned run", "Reporter.Tests.dll", Root);

    public static AzureDevOpsTestResultsPublisherOptions PublisherOptions(int batchSize = 10)
        => new(batchSize, TimeSpan.FromHours(12), 1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromSeconds(2))
        {
            CoordinationFinalizeMaxWaitTime = TimeSpan.Zero,
        };

    public AzureDevOpsTestResultsPublisher Publisher(AzureDevOpsTestResultsPublisherOptions? options = null)
    {
        Options[AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName] = [];
        return new(
            new TestCommandLineOptions(Options), Configuration.Object, Environment.Object, FileSystem.Object, Output.Object,
            Module.Object, ExitCode.Object, Client.Object, TaskService.Object, Clock.Object, Logger.Object, options ?? PublisherOptions());
    }

    public AzureDevOpsTestRunOrchestratorLifetime Orchestrator()
    {
        Options[AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName] = [];
        return new(
            new TestCommandLineOptions(Options), Configuration.Object, Environment.Object, FileSystem.Object, Output.Object,
            Module.Object, Client.Object, TaskService.Object, Clock.Object, Logger.Object, PublisherOptions());
    }

    public void PutText(string path, string content) => Files[path] = Encoding.UTF8.GetBytes(content);

    public string ReadText(string path) => Encoding.UTF8.GetString(Files[path]);

    public string CreateRealDirectory()
    {
        Directory.CreateDirectory(Root);
        return Root;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private IFileStream Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        Opens.Add((path, mode, access, share));
        BeforeOpen?.Invoke(path, mode, access, share);
        bool exists = Files.TryGetValue(path, out byte[]? bytes);
        if (mode == FileMode.CreateNew && exists)
        {
            throw new IOException("Owned file already exists.");
        }

        if (mode is FileMode.Open or FileMode.Truncate && !exists)
        {
            throw new FileNotFoundException("Owned file does not exist.", path);
        }

        if (mode is FileMode.Create or FileMode.CreateNew or FileMode.Truncate || !exists)
        {
            bytes = [];
            Files[path] = bytes;
        }

        return new MemoryFile(path, bytes!, mode == FileMode.Append, access != FileAccess.Read ? value => Files[path] = value : null);
    }

    internal sealed class Session(CancellationToken cancellationToken = default) : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("ci-session");

        public CancellationToken CancellationToken { get; } = cancellationToken;
    }

    internal sealed class MemoryFile : IFileStream
    {
        private readonly MemoryStream _stream = new();
        private readonly Action<byte[]>? _commit;
        private bool _disposed;

        public MemoryFile(string path, byte[] bytes, bool append = false, Action<byte[]>? commit = null)
        {
            Name = path;
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Position = append ? _stream.Length : 0;
            _commit = commit;
        }

        public Stream Stream => _stream;

        public string Name { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                _commit?.Invoke(_stream.ToArray());
                _stream.Dispose();
                _disposed = true;
            }
        }

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }
}
