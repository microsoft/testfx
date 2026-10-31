// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using OrchestratorManager = Microsoft.Testing.Platform.TestHostOrchestrator.ITestHostOrchestratorManager;
using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

#pragma warning disable TPEXP // Observe the public retry post-processing contract without modifying its inputs.

internal sealed class RetryRecoveryProtocolFixture
{
    public const string ManifestVariable = "TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST";
    public const string AttemptVariable = "TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER";
    public const string RootsConfiguredVariable = "TESTINGPLATFORM_PACKAGEDAPP_APPCONTAINER_ARTIFACT_ROOTS_CONFIGURED";
    public const string ArtifactSourceVariable = "TESTINGPLATFORM_ARTIFACT_PATH_SOURCE_ROOT";
    public const string ArtifactDestinationVariable = "TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT";
    public const string DiagnosticSourceVariable = "TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_SOURCE_ROOT";
    public const string DiagnosticDestinationVariable = "TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT";

    private readonly CancellationTokenSource _lifetime;
    private readonly ProtocolTasks _tasks = new();
    private readonly ServiceProvider _services = new();
    private readonly Func<IServiceProvider, ITestHostApplicationLifetime> _lifecycleFactory;
    private readonly ICompositeExtensionFactory _consumerFactory;
    private readonly ITestHostExecutionOrchestrator _orchestrator;
    private readonly ConcurrentDictionary<string, byte[]> _inputArtifacts = new(StringComparer.Ordinal);
    private readonly byte[] _originalResponseBytes = Encoding.UTF8.GetBytes("caller-owned response input: Ω 東京");
    private Task<int>? _orchestration;
    private CancellationToken _invocationToken;

    public RetryRecoveryProtocolFixture(CancellationToken cancellationToken, int maxRetries = 1)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime.CancelAfter(TimeSpan.FromMinutes(1));
        Root = Path.Combine(Path.GetTempPath(), "retry-protocol-" + Guid.NewGuid().ToString("N"));
        ResultsDirectory = Path.Combine(Root, "results 東京");
        OriginalResponsePath = Path.Combine(Root, "caller input.rsp");
        Assert.IsTrue(Files.TryAdd(OriginalResponsePath, _originalResponseBytes));

        Environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => EnvironmentValues.TryGetValue(name, out string? value) ? value : null);
        Environment.Setup(value => value.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string?>((name, value) => EnvironmentValues[name] = value);
        Environment.SetupGet(value => value.NewLine).Returns("\n");
        Environment.Setup(value => value.Exit(It.IsAny<int>())).Throws(new AssertFailedException("Unexpected environment exit."));
        Environment.Setup(value => value.FailFast(It.IsAny<string>())).Throws(new AssertFailedException("Unexpected server failure."));
        Environment.Setup(value => value.FailFast(It.IsAny<string>(), It.IsAny<Exception>()))
            .Throws(new AssertFailedException("Unexpected server failure."));
        Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        Logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, error, _) => Logs.Enqueue((level, message, error)));
        Logger.Setup(value => value.LogAsync(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, error, _) => Logs.Enqueue((level, message, error)))
            .Returns(Task.CompletedTask);
        var loggerFactory = new Mock<ILoggerFactory>(MockBehavior.Strict);
        loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
        FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(false);
        FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                Events.Enqueue("directory:" + path);
                return path;
            });
        FileSystem.Setup(value => value.ExistFile(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                Events.Enqueue("exists:" + path);
                return Files.ContainsKey(path);
            });
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write))
            .Returns<string, FileMode, FileAccess>((path, _, _) =>
            {
                Assert.IsFalse(Files.ContainsKey(path), "Generated response files must not replace an existing input.");
                return new ProtocolFile(path, new MemoryStream(), () => Events.Enqueue("dispose-write:" + path),
                    bytes => Assert.IsTrue(Files.TryAdd(path, bytes)));
            });
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) =>
            {
                Attempt attempt = Attempts.Single(value => value.ManifestPath == path);
                Events.Enqueue("open:" + path);
                if (ManifestOpenFailure is { } error)
                {
                    throw error;
                }

                var stream = new ManifestStream(Files[path], attempt, this);
                attempt.ManifestStream = stream;
                return new ProtocolFile(path, stream, () => Events.Enqueue("dispose-read:" + path));
            });
        FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                Events.Enqueue("delete-attempt:" + path);
                if (Attempts.Any(attempt => attempt.ManifestPath == path) && ManifestDeleteFailure is { } error)
                {
                    throw error;
                }

                Assert.IsTrue(Files.TryRemove(path, out _), "Only an existing task-owned generated file may be deleted.");
                Events.Enqueue("delete:" + path);
            });
        FileSystem.Setup(value => value.GetFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
            .Returns<string, string, SearchOption>((path, _, _) =>
            {
                string prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return [.. Files.Keys.Where(file => file.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(file => file, StringComparer.Ordinal)];
            });
        FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((source, destination, overwrite) =>
            {
                byte[] bytes = Files[source];
                Copies.Enqueue((source, destination, overwrite));
                Events.Enqueue("copy:" + source + "->" + destination);
                if (!Files.TryAdd(destination, bytes.ToArray()))
                {
                    Assert.IsTrue(overwrite);
                    Assert.AreSequenceEqual(Files[destination], bytes, "Null-output processors must never rewrite an existing input's bytes.");
                }
            });
        Output.Setup(value => value.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, token) =>
            {
                token.ThrowIfCancellationRequested();
                Displays.Enqueue(data);
                Events.Enqueue("display");
            })
            .Returns(Task.CompletedTask);
        var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
        configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(ResultsDirectory);
        var module = new Mock<ITestApplicationModuleInfo>(MockBehavior.Strict);
        string retries = maxRetries.ToString(CultureInfo.InvariantCulture);
        module.Setup(value => value.GetCurrentExecutableInfo()).Returns(new ExecutableInfo(
            "retry-fixture-never-started.exe",
            ["Suite.dll", "@" + OriginalResponsePath],
            [
                "Suite.dll", "--retry-failed-tests", retries, "--results-directory", ResultsDirectory,
                "--test-label", "protocole 東京",
            ],
            Root));
        var applicationCancellation = new Mock<ITestApplicationCancellationTokenSource>(MockBehavior.Strict);
        applicationCancellation.SetupGet(value => value.CancellationToken).Returns(Token);
        ProcessHandler.Setup(value => value.Start(It.IsAny<ProcessStartInfo>())).Returns<ProcessStartInfo>(Start);
        _services.AddServices(
        [
            new TestCommandLineOptions(new() { [RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = [retries] }),
            FileSystem.Object, Environment.Object, loggerFactory.Object, configuration.Object, module.Object,
            applicationCancellation.Object, ProcessHandler.Object, Output.Object, _tasks,
        ]);

        var host = new Mock<ITestHostManager>(MockBehavior.Strict);
        var commands = new Mock<ICommandLineManager>(MockBehavior.Strict);
        var orchestrators = new Mock<OrchestratorManager>(MockBehavior.Strict);
        var builder = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.TestHost).Returns(host.Object);
        builder.SetupGet(value => value.CommandLine).Returns(commands.Object);
        builder.SetupGet(value => value.TestHostOrchestrator).Returns(orchestrators.Object);
        Func<IServiceProvider, ITestHostApplicationLifetime>? lifecycleFactory = null;
        CompositeExtensionFactory<RetryDataConsumer>? consumerFactory = null;
        CompositeExtensionFactory<RetryDataConsumer>? sessionFactory = null;
        Func<IServiceProvider, ITestHostExecutionOrchestrator>? orchestratorFactory = null;
        host.Setup(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
            .Callback((Func<IServiceProvider, ITestHostApplicationLifetime> factory) => lifecycleFactory = factory);
        host.Setup(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()))
            .Callback((CompositeExtensionFactory<RetryDataConsumer> factory) => consumerFactory = factory);
        host.Setup(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()))
            .Callback((CompositeExtensionFactory<RetryDataConsumer> factory) => sessionFactory = factory);
        commands.Setup(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()));
        orchestrators.Setup(value => value.AddTestHostOrchestrator(It.IsAny<Func<IServiceProvider, ITestHostExecutionOrchestrator>>()))
            .Callback((Func<IServiceProvider, ITestHostExecutionOrchestrator> factory) => orchestratorFactory = factory);
        builder.Object.AddRetryProvider();
        Assert.AreSame(consumerFactory, sessionFactory);
        _lifecycleFactory = lifecycleFactory!;
        _consumerFactory = (ICompositeExtensionFactory)consumerFactory!;
        _orchestrator = orchestratorFactory!(_services);
    }

    public string Root { get; }

    public string ResultsDirectory { get; }

    public string OriginalResponsePath { get; }

    public CancellationToken Token => _lifetime.Token;

    public Mock<IFileSystem> FileSystem { get; } = new(MockBehavior.Strict);

    public Mock<IEnvironment> Environment { get; } = new(MockBehavior.Strict);

    public Mock<IProcessHandler> ProcessHandler { get; } = new(MockBehavior.Strict);

    public Mock<ILogger> Logger { get; } = new(MockBehavior.Strict);

    public Mock<IOutputDevice> Output { get; } = new(MockBehavior.Strict);

    public Dictionary<string, string?> EnvironmentValues { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> Events { get; } = new();

    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Error)> Logs { get; } = new();

    public ConcurrentQueue<(string Source, string Destination, bool Overwrite)> Copies { get; } = new();

    public ConcurrentQueue<IOutputDeviceData> Displays { get; } = new();

    public List<Attempt> Attempts { get; } = [];

    public Action<Attempt>? OnStart { get; set; }

    public Func<Attempt, Task<int>>? ChildBody { get; set; }

    public Exception? LaunchFailure { get; set; }

    public Exception? ManifestOpenFailure { get; set; }

    public Exception? ManifestDeleteFailure { get; set; }

    public Exception? ManifestReadFailure { get; set; }

    public int? FailReadAfterBytes { get; set; }

    public Action? BeforeReadFailure { get; set; }

    public bool ExpectChildCancellation { get; set; }

    public string[] Warnings => [.. Logs.Where(value => value.Level == LogLevel.Warning).Select(value => value.Message)];

    public ArtifactProbe WatchKind(string kind) => Watch([kind], []);

    public ArtifactProbe WatchExtension(string extension) => Watch([], [extension]);

    public Task<int> RunAsync(CancellationToken? cancellationToken = null)
    {
        Assert.IsNull(_orchestration, "Each fixture owns one public orchestration.");
        _invocationToken = cancellationToken ?? Token;
        return _orchestration = _orchestrator.OrchestrateTestHostExecutionAsync(_invocationToken);
    }

    public void AddArtifact(string path, string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        Assert.IsTrue(Files.TryAdd(path, bytes), "Inputs must be unique, not overwritten.");
        Assert.IsTrue(_inputArtifacts.TryAdd(path, bytes.ToArray()));
    }

    public void AssertInputsPreserved()
    {
        Assert.AreSequenceEqual(_originalResponseBytes, Files[OriginalResponsePath]);
        foreach (KeyValuePair<string, byte[]> artifact in _inputArtifacts)
        {
            Assert.AreSequenceEqual(artifact.Value, Files[artifact.Key], "Retry processing must preserve original artifact bytes.");
        }
    }

    public async Task CloseAsync()
    {
        try
        {
            _lifetime.Cancel();
            foreach (Attempt attempt in Attempts)
            {
                attempt.Cancel();
            }

            if (_orchestration is { IsCompleted: false })
            {
                try
                {
                    await _orchestration.WithCancellationAsync(Token);
                }
                catch (OperationCanceledException)
                {
                    // The invocation was canceled by this fixture's failure guard.
                }
            }
        }
        finally
        {
            try
            {
                // A failed assertion must also release a client whose session did not finish.
                foreach (Attempt attempt in Attempts)
                {
                    attempt.CloseClient();
                }

                await _tasks.DrainAsync();
                AssertInputsPreserved();
                Environment.Verify(value => value.Exit(It.IsAny<int>()), Times.Never);
                Environment.Verify(value => value.FailFast(It.IsAny<string>()), Times.Never);
                Environment.Verify(value => value.FailFast(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
                Assert.IsEmpty(Logs.Where(value => value.Level == LogLevel.Error), "A transport failure is not a successful recovery test.");
            }
            finally
            {
                foreach (Attempt attempt in Attempts)
                {
                    attempt.DisposeCancellation();
                }

                _lifetime.Dispose();
            }
        }
    }

    private ArtifactProbe Watch(string[] kinds, string[] extensions)
    {
        var probe = new ArtifactProbe(kinds, extensions, Events);
        _services.AddService(probe.Processor.Object);
        return probe;
    }

    private IProcess Start(ProcessStartInfo startInfo)
    {
        Assert.AreEqual("retry-fixture-never-started.exe", startInfo.FileName);
        Assert.IsFalse(startInfo.UseShellExecute);
#if NET8_0_OR_GREATER
        string[] arguments = [.. startInfo.ArgumentList];
#else
        string[] arguments = [.. ResponseFileHelper.SplitCommandLine(startInfo.Arguments)];
#endif
        List<string> expanded = [];
        foreach (string argument in arguments)
        {
            if (argument.StartsWith("@", StringComparison.Ordinal))
            {
                string path = argument.Substring(1);
                Assert.IsTrue(Files.ContainsKey(path), "The fake child reads the actual generated response file.");
                expanded.AddRange(ResponseFileHelper.SplitCommandLine(Encoding.UTF8.GetString(Files[path])));
            }
            else
            {
                expanded.Add(argument);
            }
        }

        Dictionary<string, string[]> options = [];
        for (int i = 0; i < expanded.Count; i++)
        {
            if (!expanded[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = expanded[i].Substring(2);
            List<string> values = [];
            while (i + 1 < expanded.Count && !expanded[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values.Add(expanded[++i]);
            }

            options.Add(key, [.. values]);
        }

        Assert.IsFalse(options.ContainsKey(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName));
        Assert.AreSequenceEqual(new[] { "protocole 東京" }, options["test-label"]);
        int number = int.Parse(startInfo.EnvironmentVariables[AttemptVariable]!, CultureInfo.InvariantCulture);
        Assert.AreEqual(Attempts.Count + 1, number);
        string manifest = startInfo.EnvironmentVariables[ManifestVariable]!;
        Assert.MatchesRegex(new Regex("^testfx-retry-recovered-artifacts-[0-9a-f]{32}\\.txt$", RegexOptions.CultureInvariant), Path.GetFileName(manifest));
        var attempt = new Attempt(this, number, options, arguments, manifest, _invocationToken);
        Attempts.Add(attempt);
        Events.Enqueue("start-process:" + number);
        OnStart?.Invoke(attempt);
        if (LaunchFailure is { } failure)
        {
            throw failure;
        }

        attempt.StartChild();
        return attempt.Process.Object;
    }

    internal sealed class Attempt
    {
        private readonly RetryRecoveryProtocolFixture _owner;
        private readonly CancellationTokenSource _cancellation;
        private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private RetryLifecycleCallbacks? _lifecycle;

        public Attempt(
            RetryRecoveryProtocolFixture owner, int number, Dictionary<string, string[]> options,
            string[] arguments, string manifestPath, CancellationToken invocationToken)
        {
            _owner = owner;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(owner.Token, invocationToken);
            Number = number;
            Options = options;
            LaunchArguments = arguments;
            ManifestPath = manifestPath;
            Directory = Assert.ContainsSingle(options[PlatformCommandLineProvider.ResultDirectoryOptionKey]);
            PipeName = Assert.ContainsSingle(options[RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName]);
            Assert.AreEqual(number.ToString(CultureInfo.InvariantCulture), Path.GetFileName(Directory));
            Assert.IsTrue(Directory.StartsWith(owner.ResultsDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            Assert.IsNotEmpty(PipeName);
            Process.SetupGet(value => value.HasExited).Returns(() => _exit.Task.IsCompleted);
            Process.SetupGet(value => value.ExitCode).Returns(() => _exit.Task.GetAwaiter().GetResult());
            Process.Setup(value => value.WaitForExitAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async token =>
                {
                    owner.Events.Enqueue("wait-process:" + Number);
                    await _exit.Task.WithCancellationAsync(token);
                    owner.Events.Enqueue("waited-process:" + Number);
                });
            Process.Setup(value => value.Kill()).Callback(() =>
            {
                owner.Events.Enqueue("kill-process:" + Number);
                _cancellation.Cancel();
            });
            Process.Setup(value => value.Dispose()).Callback(() => owner.Events.Enqueue("dispose-process:" + Number));
            Process.SetupAdd(value => value.Exited += It.IsAny<EventHandler>());
            Process.SetupRemove(value => value.Exited -= It.IsAny<EventHandler>());
        }

        public int Number { get; }

        public string Directory { get; }

        public string RetryRoot => Path.GetDirectoryName(Directory)!;

        public string ManifestPath { get; }

        public string PipeName { get; }

        public Dictionary<string, string[]> Options { get; }

        public string[] LaunchArguments { get; }

        public Dictionary<string, string?> EnvironmentValues { get; } = new(StringComparer.Ordinal);

        public Mock<IProcess> Process { get; } = new(MockBehavior.Strict);

        public RetryDataConsumer Consumer { get; private set; } = null!;

        public TestSessionContext Session { get; private set; } = null!;

        public string[] RetryIds { get; private set; } = [];

        public OperationCanceledException? ChildCancellation { get; private set; }

        public ManifestStream? ManifestStream { get; set; }

        public Task? ChildTask { get; private set; }

        public CancellationToken Token => _cancellation.Token;

        public string PathFor(string file) => Path.Combine(Directory, file);

        public void SetManifest(string text)
            => Assert.IsTrue(_owner.Files.TryAdd(ManifestPath, Encoding.UTF8.GetBytes(text)), "Never overwrite an existing manifest.");

        public void AddArtifact(string path, string content) => _owner.AddArtifact(path, content);

        public Task PublishAsync(string path, string? kind)
            => Consumer.ConsumeAsync(null!, new SessionFileArtifact(
                Session.SessionUid, new FileInfo(path), "retry protocol artifact", "independent fixture input", kind), Token);

        public Task ResultAsync(string uid, string displayName, TestNodeStateProperty state)
            => Consumer.ConsumeAsync(null!, new TestNodeUpdateMessage(
                Session.SessionUid, new TestNode { Uid = uid, DisplayName = displayName, Properties = new PropertyBag(state) }), Token);

        public void StartChild() => ChildTask = _owner._tasks.Run(RunChildAsync, CancellationToken.None);

        public void Cancel() => _cancellation.Cancel();

        public void CloseClient() => _lifecycle?.Dispose();

        public void DisposeCancellation() => _cancellation.Dispose();

        private async Task RunChildAsync()
        {
            int exitCode;
            try
            {
                var childEnvironment = new Mock<IEnvironment>(MockBehavior.Strict);
                EnvironmentValues[ManifestVariable] = ManifestPath;
                EnvironmentValues[AttemptVariable] = Number.ToString(CultureInfo.InvariantCulture);
                childEnvironment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
                    .Returns<string>(name => EnvironmentValues.TryGetValue(name, out string? value) ? value : null);
                var child = (ServiceProvider)_owner._services.Clone();
                child.ReplaceService<IEnvironment>(childEnvironment.Object);
                child.ReplaceService<ICommandLineOptions>(new TestCommandLineOptions(Options));
                _lifecycle = Assert.IsInstanceOfType<RetryLifecycleCallbacks>(_owner._lifecycleFactory(child));
                child.AddService(_lifecycle);
                var factory = (ICompositeExtensionFactory)_owner._consumerFactory.Clone();
                Consumer = Assert.IsInstanceOfType<RetryDataConsumer>(factory.GetInstance(child));
                ITestSessionLifetimeHandler sessionLifetime = Assert.IsInstanceOfType<ITestSessionLifetimeHandler>(factory.GetInstance(child));
                Assert.AreSame<IExtension>(Consumer, sessionLifetime);
                Assert.IsTrue(await _lifecycle.IsEnabledAsync());
                await Consumer.InitializeAsync();
                await _lifecycle.BeforeRunAsync(Token);
                RetryIds = _lifecycle.FailedTestsIDToRetry!;
                _owner.Events.Enqueue("connected-child:" + Number);
                Session = new TestSessionContext(Token);
                await sessionLifetime.OnTestSessionStartingAsync(Session);
                exitCode = _owner.ChildBody is { } body ? await body(this) : 0;
                if (_owner.ChildBody is null)
                {
                    await ResultAsync("stable-1", "Stable one", PassedTestNodeStateProperty.CachedInstance);
                    await ResultAsync("stable-2", "Stable two", PassedTestNodeStateProperty.CachedInstance);
                }

                await sessionLifetime.OnTestSessionFinishingAsync(Session);
                await _lifecycle.AfterRunAsync(exitCode, Token);
            }
            catch (OperationCanceledException error) when (_owner.ExpectChildCancellation && Token.IsCancellationRequested)
            {
                ChildCancellation = error;
                exitCode = 143;
            }
            catch (Exception error)
            {
                _exit.TrySetException(error);
                throw;
            }
            finally
            {
                CloseClient();
                _owner.Events.Enqueue("dispose-child:" + Number);
            }

            _owner.Events.Enqueue("complete-child:" + Number);
            _exit.TrySetResult(exitCode);
            Process.Raise(value => value.Exited += null, EventArgs.Empty);
        }
    }

    internal sealed class ArtifactProbe
    {
        public ArtifactProbe(string[] kinds, string[] extensions, ConcurrentQueue<string> events)
        {
            Processor.SetupGet(value => value.SupportedModes).Returns([ArtifactPostProcessingMode.RetryAttempts]);
            Processor.SetupGet(value => value.SupportedKinds).Returns(kinds);
            Processor.SetupGet(value => value.SupportedFileExtensionsFallback).Returns(extensions);
            Processor.Setup(value => value.ProcessAsync(
                It.IsAny<IReadOnlyList<InputArtifact>>(), It.IsAny<string>(), It.IsAny<ArtifactPostProcessingContext>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<InputArtifact>, string, ArtifactPostProcessingContext, CancellationToken>((inputs, directory, context, token) =>
                {
                    Calls.Add(([.. inputs], directory, context, token));
                    events.Enqueue("process-artifacts");
                    return Task.FromResult<ProcessedArtifact?>(null);
                });
        }

        public Mock<IArtifactPostProcessorRequiresPostProcessing> Processor { get; } = new(MockBehavior.Strict);

        public List<(InputArtifact[] Inputs, string Directory, ArtifactPostProcessingContext Context, CancellationToken Token)> Calls { get; } = [];
    }

    internal sealed class ManifestStream(byte[] bytes, Attempt attempt, RetryRecoveryProtocolFixture fixture) : MemoryStream(bytes, writable: false)
    {
        public bool Disposed { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (fixture.ManifestReadFailure is { } failure && Position >= (fixture.FailReadAfterBytes ?? 0))
            {
                fixture.BeforeReadFailure?.Invoke();
                throw failure;
            }

            if (fixture.FailReadAfterBytes is { } limit && Position < limit)
            {
                count = Math.Min(count, limit - (int)Position);
            }

            int read = base.Read(buffer, offset, count);
            fixture.Events.Enqueue("read:" + attempt.ManifestPath);
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ProtocolFile(
        string name, MemoryStream stream, Action disposed, Action<byte[]>? commit = null) : IFileStream
    {
        private bool _disposed;

        public Stream Stream => stream;

        public string Name => name;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            commit?.Invoke(stream.ToArray());
            stream.Dispose();
            disposed();
        }

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }

    private sealed class ProtocolTasks : ITask
    {
        private readonly ConcurrentQueue<Task> _work = new();

        public Task Run(Action action) => Track(Task.Run(action));

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => Track(Task.Run(function, cancellationToken));

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => Track(Task.Run(function, cancellationToken));

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => Run(action, cancellationToken);

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => throw new AssertFailedException("Retry recovery must not schedule an unowned delay.");

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
            => throw new AssertFailedException("Retry recovery must not schedule an unowned delay.");

        public async Task DrainAsync()
        {
            using var timeout = new CancellationTokenSource();
            Task guard = Task.Delay(TimeSpan.FromMinutes(1), timeout.Token);
            try
            {
                Task[] tasks = _work.ToArray();
                Task joined = Task.WhenAll(tasks);
                if (await Task.WhenAny(joined, guard) != joined)
                {
                    throw new TimeoutException("Task-owned retry child/pipe delegates did not drain.");
                }

                await joined;
            }
            finally
            {
                timeout.Cancel();
                try
                {
                    await guard;
                }
                catch (OperationCanceledException)
                {
                    // Cancel and observe the bounded guard even on assertion or transport failure.
                }
            }
        }

        private TTask Track<TTask>(TTask task)
            where TTask : Task
        {
            _work.Enqueue(task);
            return task;
        }
    }
}
