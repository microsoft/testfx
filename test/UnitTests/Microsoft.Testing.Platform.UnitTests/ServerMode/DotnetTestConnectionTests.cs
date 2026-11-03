// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DotnetTestConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AuxiliaryCancellationOnlyOwnsOneCallbackWhileTheOriginalIsStillPending()
    {
        using CancellationTokenSource application = new();
        using ConnectionFixture fixture = CreateConnection(application.Token);
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        CancellationToken actualToken = default;
        Func<CancellationToken, Task> callback = async token =>
        {
            Interlocked.Increment(ref calls);
            actualToken = token;
            entered.TrySetResult(true);
            await release.Task;
        };
        var owned = new List<Task>();
        try
        {
            owned.Add(InvokeAuxiliaryAsync(fixture.Connection, "RequestCancelOnceAsync", callback));
            await entered.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            owned.AddRange(Enumerable.Range(0, 8).Select(_ => InvokeAuxiliaryAsync(fixture.Connection, "RequestCancelOnceAsync", callback)));
            Assert.IsFalse(owned[0].IsCompleted);
            Assert.IsTrue(owned.Skip(1).All(task => task.IsCompleted && !task.IsFaulted && !task.IsCanceled));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(application.Token, actualToken);
            Assert.IsFalse(application.IsCancellationRequested);
            Assert.IsEmpty(fixture.Client.Mock.Invocations);
        }
        finally
        {
            release.TrySetResult(true);
            await Task.WhenAll(owned).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        await InvokeAuxiliaryAsync(fixture.Connection, "RequestCancelOnceAsync", callback);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AuxiliaryCancellationPropagatesOriginalFailureWithoutReplayingTheCallback(bool cancellation)
    {
        using CancellationTokenSource application = new();
        using ConnectionFixture fixture = CreateConnection(application.Token);
        Exception cause = cancellation ? new OperationCanceledException(application.Token) : new IOException("callback failed");
        int calls = 0;
        Func<CancellationToken, Task> callback = token =>
        {
            Assert.AreEqual(application.Token, token);
            calls++;
            return Task.FromException(cause);
        };

        Exception actual = await Assert.ThrowsAsync<Exception>(() => InvokeAuxiliaryAsync(fixture.Connection, "RequestCancelOnceAsync", callback));
        Assert.AreSame(cause, actual);
        await InvokeAuxiliaryAsync(fixture.Connection, "RequestCancelOnceAsync", callback);
        Assert.AreEqual(1, calls);
        Assert.IsFalse(application.IsCancellationRequested);
        Assert.IsEmpty(fixture.Client.Mock.Invocations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AuxiliaryDiagnosticsPreserveLevelAndStateButCannotSurfaceProviderFailure(bool synchronousFailure)
    {
        var logger = new Mock<ILogger>();
        TaskCompletionSource<bool> providerInvoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        object? recorded = null;
        LogLevel recordedLevel = LogLevel.None;
        logger.Setup(value => value.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, state, exception, formatter) =>
            {
                recordedLevel = level;
                recorded = formatter(state, exception);
                providerInvoked.TrySetResult(true);
                return synchronousFailure
                    ? throw new IOException("provider threw")
                    : Task.FromException(new IOException("provider task failed"));
            });
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken, logger: logger.Object);

        await InvokeAuxiliaryAsync(fixture.Connection, "TryLogAsync", LogLevel.Debug, "control detail");
        // The bounded logging wait can finish before the background provider runs.
        await providerInvoked.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        Assert.AreEqual(LogLevel.Debug, recordedLevel);
        Assert.AreEqual("control detail", recorded);
        logger.Verify(value => value.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        Assert.IsEmpty(fixture.Client.Mock.Invocations);
    }

    private static Task InvokeAuxiliaryAsync(DotnetTestConnection connection, string name, params object[] arguments)
        => (Task)typeof(DotnetTestConnection).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(connection, arguments)!;

    [TestMethod]
    [DataRow("1.0.0", false, false)]
    [DataRow("1.1.0", false, false)]
    [DataRow("1.2.0", true, false)]
    [DataRow("1.3.0", true, true)]
    [DataRow("2.0.0", false, false)]
    [DataRow("malformed", false, false)]
    public async Task IsCompatibleProtocolAsync_NegotiatesCapabilitiesAndSendsHostIdentity(string version, bool logs, bool display)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        HandshakeMessage? sent = null;
        fixture.Client.Respond(
            new HandshakeMessage(new()
            {
                [HandshakeMessagePropertyNames.SupportedProtocolVersions] = version,
                [HandshakeMessagePropertyNames.IsIDE] = "true",
                [HandshakeMessagePropertyNames.RequiredPostProcessingSupported] = "true",
                [HandshakeMessagePropertyNames.ServerControlPipeName] = "advertised-control",
            }), TestContext.CancellationToken, message => sent = message);

        Assert.AreEqual(version is "1.0.0" or "1.1.0" or "1.2.0" or "1.3.0", await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));
        Assert.AreEqual(logs, fixture.Connection.IsLogForwardingSupported);
        Assert.AreEqual(display, fixture.Connection.IsDisplayMessageForwardingSupported);
        Assert.IsTrue(fixture.Connection.IsIDE);
        Assert.IsTrue(fixture.Connection.IsRequiredArtifactPostProcessingSupported);
        Assert.IsTrue(fixture.Connection.IsServerControlChannelSupported);
        Assert.IsNotNull(sent);
        Assert.IsNotNull(sent.Properties);
        Assert.AreEqual("123", sent.Properties[HandshakeMessagePropertyNames.PID]);
        Assert.AreEqual("test-module.dll", sent.Properties[HandshakeMessagePropertyNames.ModulePath]);
        Assert.AreEqual("execution", sent.Properties[HandshakeMessagePropertyNames.ExecutionId]);
        Assert.AreEqual("run", sent.Properties[HandshakeMessagePropertyNames.ExecutionMode]);
        Assert.AreEqual("1", sent.Properties[HandshakeMessagePropertyNames.AttemptNumber]);
        Assert.AreEqual(DotnetTestConnection.InstanceId, sent.Properties[HandshakeMessagePropertyNames.InstanceId]);
    }

    [TestMethod]
    [DataRow("1.0.0", "1", true, true, DisplayName = "CoverageOnBase10")]
    [DataRow("1.3.0", "1", true, true, DisplayName = "CoverageOnBase13")]
    [DataRow("1.4.0", "1", true, true, DisplayName = "CoverageWithoutControlPipe")]
    [DataRow("1.3.0", null, true, false, DisplayName = "OldPeerOmitsCoverage")]
    [DataRow("1.3.0", "false", true, false, DisplayName = "PeerOptsOut")]
    [DataRow("1.3.0", "2", true, false, DisplayName = "UnknownCoverageVersion")]
    [DataRow("1.3.0", "true", true, false, DisplayName = "BooleanIsNotCoverageVersion")]
    [DataRow("2.0.0", "1", false, false, DisplayName = "IncompatibleBase")]
    [DataRow("malformed", "1", false, false, DisplayName = "MalformedBase")]
    [DataRow(null, "1", false, false, DisplayName = "MissingBase")]
    public async Task IsCompatibleProtocolAsync_CoverageSummaryRequiresExactVersionAndCompatibleBase(
        string? baseVersion, string? coverageVersion, bool compatible, bool coverageEnabled)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        Dictionary<byte, string> reply = [];
        if (baseVersion is not null)
        {
            reply[HandshakeMessagePropertyNames.SupportedProtocolVersions] = baseVersion;
        }

        if (coverageVersion is not null)
        {
            reply[20] = coverageVersion;
        }

        HandshakeMessage? sent = null;
        fixture.Client.Respond(new HandshakeMessage(reply), TestContext.CancellationToken, value => sent = value);

        Assert.AreEqual(compatible, await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));

        Assert.IsNotNull(sent);
        Assert.IsNotNull(sent.Properties);
        Assert.AreEqual("1", sent.Properties[20]);
        Assert.AreEqual("1.0.0;1.1.0;1.2.0;1.3.0;1.4.0", sent.Properties[4]);
        Assert.AreEqual(coverageEnabled, fixture.Connection.IsCoverageSummarySupported);
        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
    }

    [TestMethod]
    [DataRow("1.3.0", null, DisplayName = "RenegotiateOldPeer")]
    [DataRow("1.3.0", "false", DisplayName = "RenegotiateOptOut")]
    [DataRow("1.3.0", "2", DisplayName = "RenegotiateUnknownCoverage")]
    [DataRow("2.0.0", "1", DisplayName = "RenegotiateIncompatibleBase")]
    [DataRow(null, "1", DisplayName = "RenegotiateMissingBase")]
    public async Task IsCompatibleProtocolAsync_CoverageSummaryRenegotiationClearsPreviousSupport(string? baseVersion, string? coverageVersion)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken));
        Assert.IsTrue(fixture.Connection.IsCoverageSummarySupported);

        Assert.AreEqual(
            baseVersion == "1.3.0",
            await NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken, baseVersion, coverageVersion));

        Assert.IsFalse(fixture.Connection.IsCoverageSummarySupported);
        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
    }

    [TestMethod]
    [DataRow("false", DisplayName = "LocalCoverageOptOut")]
    [DataRow("2", DisplayName = "LocalUnknownCoverageVersion")]
    public async Task IsCompatibleProtocolAsync_LocalCoverageOptOutCannotBeEnabledByPeer(string advertisedVersion)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        HandshakeMessage? sent = null;
        fixture.Client.Respond(
            new HandshakeMessage(new() { [4] = "1.3.0", [20] = "1" }),
            TestContext.CancellationToken,
            value => sent = value);

        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(
            HandshakeMessageHostTypes.TestHost,
            new Dictionary<byte, string> { [20] = advertisedVersion }));

        Assert.IsNotNull(sent);
        Assert.IsNotNull(sent.Properties);
        Assert.AreEqual(advertisedVersion, sent.Properties[20]);
        Assert.IsFalse(fixture.Connection.IsCoverageSummarySupported);
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "RawCoverageOldPeer")]
    [DataRow("false", false, DisplayName = "RawCoverageOptOut")]
    [DataRow("2", false, DisplayName = "RawCoverageUnknownVersion")]
    [DataRow("1", true, DisplayName = "RawCoverageNegotiated")]
    public async Task SendMessageAsync_CoverageSummaryIsSentOnlyWhenNegotiated(string? coverageVersion, bool enabled)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken, coverageVersion: coverageVersion));
        List<CoverageSummaryMessages> sent = [];
        fixture.Client.RespondVoid<CoverageSummaryMessages>(TestContext.CancellationToken, sent.Add);
        fixture.Client.Mock.Invocations.Clear();
        var message = new CoverageSummaryMessages("execution", "instance", [new("session", "producer", 0, null, 0, null, 0, 0)]);

        await fixture.Connection.SendMessageAsync(message);

        if (enabled)
        {
            Assert.AreSame(message, Assert.ContainsSingle(sent));
            Assert.AreEqual(TestContext.CancellationToken, (CancellationToken)Assert.ContainsSingle(fixture.Client.Mock.Invocations).Arguments[1]);
        }
        else
        {
            Assert.IsEmpty(sent);
            Assert.IsEmpty(fixture.Client.Mock.Invocations);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "ArtifactCoverageOldPeer")]
    [DataRow(true, DisplayName = "ArtifactCoverageNegotiated")]
    public async Task SendMessageAsync_ArtifactCoverageIsGatedWithoutMutatingOriginal(bool enabled)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        Assert.IsTrue(await NegotiateCoverageSummaryAsync(fixture, TestContext.CancellationToken, coverageVersion: enabled ? "1" : null));
        List<FileArtifactMessages> sent = [];
        fixture.Client.RespondVoid<FileArtifactMessages>(TestContext.CancellationToken, sent.Add);
        CoverageSummaryMessage[] summary = [new(null, "merged-producer", 0, null, 255, "custom", 3_000_000_000L, long.MaxValue)];
        var artifact = new FileArtifactMessage(
            "merged.fixture", "merged", "description", "test", "display", "session", "fixture.kind",
            ["first.fixture", "second.fixture"], summary);
        var message = new FileArtifactMessages("execution", "instance", [artifact]);

        await fixture.Connection.SendMessageAsync(message);

        FileArtifactMessages actual = Assert.ContainsSingle(sent);
        Assert.AreEqual("execution", actual.ExecutionId);
        Assert.AreEqual("instance", actual.InstanceId);
        FileArtifactMessage actualArtifact = Assert.ContainsSingle(actual.FileArtifacts);
        Assert.AreEqual(artifact with { CoverageSummary = enabled ? summary : null }, actualArtifact);
        Assert.AreSequenceEqual(["first.fixture", "second.fixture"], actualArtifact.InputArtifactPaths);
        Assert.AreSame(summary, artifact.CoverageSummary);
        Assert.AreSame(artifact, Assert.ContainsSingle(message.FileArtifacts));
    }

    [TestMethod]
    [DataRow("run", "run")]
    [DataRow("help", "help")]
    [DataRow("discover", "discover")]
    [DataRow("tool", "tool")]
    public async Task IsCompatibleProtocolAsync_ReportsExecutionModeAndAllowsAdditionalPropertyOverride(string input, string expected)
    {
        CommandLineParseOption[] options = input switch
        {
            "help" => [new("help", [])],
            "discover" => [new(PlatformCommandLineProvider.DiscoverTestsOptionKey, [])],
            _ => [],
        };
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken, options, input == "tool" ? "sample" : null);
        HandshakeMessage? sent = null;
        fixture.Client.Respond(new HandshakeMessage(null), TestContext.CancellationToken, message => sent = message);

        Assert.IsFalse(await fixture.Connection.IsCompatibleProtocolAsync(
            HandshakeMessageHostTypes.ArtifactPostProcessor,
            new Dictionary<byte, string> { [HandshakeMessagePropertyNames.ModulePath] = "override.dll" }));
        Assert.IsNotNull(sent);
        Assert.IsNotNull(sent.Properties);
        Assert.AreEqual(expected, sent.Properties[HandshakeMessagePropertyNames.ExecutionMode]);
        Assert.AreEqual("override.dll", sent.Properties[HandshakeMessagePropertyNames.ModulePath]);
        Assert.IsFalse(sent.Properties.ContainsKey(HandshakeMessagePropertyNames.AttemptNumber));
        Assert.IsFalse(fixture.Connection.IsIDE);
        Assert.IsFalse(fixture.Connection.IsRequiredArtifactPostProcessingSupported);
        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
    }

    [TestMethod]
    [DataRow(null, "1")]
    [DataRow("", "1")]
    [DataRow("1", "1")]
    [DataRow("002", "2")]
    [DataRow("2147483647", "2147483647")]
    public async Task IsCompatibleProtocolAsync_NormalizesPositiveAttemptNumbers(string? value, string expected)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        fixture.Environment.Setup(environment => environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns(value);
        HandshakeMessage? sent = null;
        fixture.Client.Respond(new HandshakeMessage([]), TestContext.CancellationToken, message => sent = message);

        Assert.IsFalse(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.ServerTestHost));
        Assert.IsNotNull(sent);
        Assert.IsNotNull(sent.Properties);
        Assert.AreEqual(expected, sent.Properties[HandshakeMessagePropertyNames.AttemptNumber]);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("2147483648")]
    [DataRow("invalid")]
    public async Task IsCompatibleProtocolAsync_RejectsInvalidAttemptBeforeSendingHandshake(string value)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        fixture.Environment.Setup(environment => environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER)).Returns(value);
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost));
        Assert.AreEqual($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER}' must contain a positive integer.", exception.Message);
        Assert.IsEmpty(fixture.Client.Mock.Invocations);
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow("false", "false", "")]
    [DataRow("invalid", "invalid", "control")]
    public async Task IsCompatibleProtocolAsync_HttpDoesNotEnableControlPipeAndInvalidFlagsRemainFalse(string? ide, string? required, string? control)
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.Http);
        Dictionary<byte, string> properties = new() { [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0" };
        if (ide is not null)
        {
            properties[HandshakeMessagePropertyNames.IsIDE] = ide;
            properties[HandshakeMessagePropertyNames.RequiredPostProcessingSupported] = required!;
            properties[HandshakeMessagePropertyNames.ServerControlPipeName] = control!;
        }

        fixture.Client.Respond(new HandshakeMessage(properties), TestContext.CancellationToken);
        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHostController));
        Assert.IsFalse(fixture.Connection.IsIDE);
        Assert.IsFalse(fixture.Connection.IsRequiredArtifactPostProcessingSupported);
        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
        bool cancelled = false;
        await fixture.Connection.StartServerControlChannelAsync(_ =>
        {
            cancelled = true;
            return Task.CompletedTask;
        });
        Assert.IsFalse(cancelled);
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_SupportedButPipeNameEmptyDoesNotCreateListener()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        await NegotiateServerControlChannelAsync(fixture, TestContext.CancellationToken);
        SetField(fixture.Connection, "_serverControlPipeName", string.Empty);

        await fixture.Connection.StartServerControlChannelAsync(_ => Task.CompletedTask);

        Assert.IsTrue(fixture.Connection.IsServerControlChannelSupported);
        Assert.IsNull(GetField(fixture.Connection, "_serverControlPipeClient"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerTask"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerCts"));
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_AlreadyStartedDoesNotReplaceExistingListener()
    {
        using CancellationTokenSource listenerCts = new();
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        await NegotiateServerControlChannelAsync(fixture, TestContext.CancellationToken);
        using var existingPipeClient = (IDisposable)Activator.CreateInstance(
            typeof(DotnetTestConnection).GetField("_serverControlPipeClient", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType,
            new object?[] { "server-control", fixture.Environment.Object, false })!;
        Task listenerTask = Task.CompletedTask;
        SetField(fixture.Connection, "_serverControlPipeClient", existingPipeClient);
        SetField(fixture.Connection, "_serverControlListenerTask", listenerTask);
        SetField(fixture.Connection, "_serverControlListenerCts", listenerCts);

        await fixture.Connection.StartServerControlChannelAsync(_ => Task.CompletedTask);

        Assert.AreSame(existingPipeClient, GetField(fixture.Connection, "_serverControlPipeClient"));
        Assert.AreSame(listenerTask, GetField(fixture.Connection, "_serverControlListenerTask"));
        Assert.AreSame(listenerCts, GetField(fixture.Connection, "_serverControlListenerCts"));
        Assert.IsFalse(listenerCts.IsCancellationRequested);
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_UnsupportedDoesNotCreateListenerDespitePipeName()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.NamedPipe);
        SetField(fixture.Connection, "_serverControlPipeName", "server-control");

        await fixture.Connection.StartServerControlChannelAsync(_ => Task.CompletedTask);

        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
        Assert.IsNull(GetField(fixture.Connection, "_serverControlPipeClient"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerTask"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerCts"));
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_EmptyPipeNameDespiteNegotiatedSupportDoesNotConnect()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.NamedPipe);
        SetField(fixture.Connection, "_serverControlPipeName", string.Empty);
        typeof(DotnetTestConnection).GetProperty(nameof(DotnetTestConnection.IsServerControlChannelSupported))!.SetValue(fixture.Connection, true);
        bool cancelled = false;

        await fixture.Connection.StartServerControlChannelAsync(_ =>
        {
            cancelled = true;
            return Task.CompletedTask;
        });

        Assert.IsFalse(cancelled);
        Assert.IsNull(GetField(fixture.Connection, "_serverControlPipeClient"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerTask"));
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_AlreadyStartedDoesNotReplaceTheExistingClient()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.NamedPipe);
        SetField(fixture.Connection, "_serverControlPipeName", "already-started-control");
        typeof(DotnetTestConnection).GetProperty(nameof(DotnetTestConnection.IsServerControlChannelSupported))!.SetValue(fixture.Connection, true);
        Type namedPipeClientType = typeof(DotnetTestConnection).Assembly.GetType("Microsoft.Testing.Platform.IPC.NamedPipeClient", throwOnError: true)!;
        object existingClient = Activator.CreateInstance(namedPipeClientType, new object?[] { "already-started-control", fixture.Environment.Object, false })!;
        Task existingListenerTask = Task.CompletedTask;
        SetField(fixture.Connection, "_serverControlPipeClient", existingClient);
        SetField(fixture.Connection, "_serverControlListenerTask", existingListenerTask);
        bool cancelled = false;

        try
        {
            await fixture.Connection.StartServerControlChannelAsync(_ =>
            {
                cancelled = true;
                return Task.CompletedTask;
            });

            Assert.IsFalse(cancelled);
            Assert.AreSame(existingClient, GetField(fixture.Connection, "_serverControlPipeClient"));
            Assert.AreSame(existingListenerTask, GetField(fixture.Connection, "_serverControlListenerTask"));
        }
        finally
        {
            if (!ReferenceEquals(existingClient, GetField(fixture.Connection, "_serverControlPipeClient")))
            {
                ((IDisposable)existingClient).Dispose();
            }
        }
    }

    [TestMethod]
    public async Task StartServerControlChannelAsync_CapabilityNotNegotiatedDoesNotConnectEvenWithAPipeName()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.NamedPipe);
        SetField(fixture.Connection, "_serverControlPipeName", "unnegotiated-control");
        bool cancelled = false;

        await fixture.Connection.StartServerControlChannelAsync(_ =>
        {
            cancelled = true;
            return Task.CompletedTask;
        });

        Assert.IsFalse(fixture.Connection.IsServerControlChannelSupported);
        Assert.IsFalse(cancelled);
        Assert.IsNull(GetField(fixture.Connection, "_serverControlPipeClient"));
        Assert.IsNull(GetField(fixture.Connection, "_serverControlListenerTask"));
    }

    [TestMethod]
    public async Task HelpInvokedAsync_SortsOrdinaryOptionsAndExcludesToolProviders()
    {
        var ordinary = new Mock<ICommandLineOptionsProvider>();
        ordinary.SetupGet(provider => provider.Uid).Returns("provider");
        ordinary.Setup(provider => provider.GetCommandLineOptions()).Returns(
            [new("z-last", "last description", ArgumentArity.ExactlyOne, true), new("a-first", "first description", ArgumentArity.Zero, false)]);
        var tool = new Mock<IToolCommandLineOptionsProvider>();
        tool.Setup(provider => provider.GetCommandLineOptions()).Returns([new("tool-only", "tool", ArgumentArity.Zero, false)]);
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken, providers: [tool.Object, ordinary.Object]);
        CommandLineOptionMessages? sent = null;
        fixture.Client.RespondVoid<CommandLineOptionMessages>(TestContext.CancellationToken, message => sent = message);

        await fixture.Connection.HelpInvokedAsync();
        Assert.IsNotNull(sent);
        Assert.AreEqual("test-module.dll", sent.ModulePath);
        Assert.IsNotNull(sent.CommandLineOptionMessageList);
        Assert.AreSequenceEqual(["a-first", "z-last"], sent.CommandLineOptionMessageList.Select(option => option.Name));
        Assert.AreEqual("last description", sent.CommandLineOptionMessageList[1].Description);
        Assert.AreEqual("provider", sent.CommandLineOptionMessageList[1].ProviderUid);
        Assert.IsTrue(sent.CommandLineOptionMessageList[1].IsHidden);
        Assert.AreEqual(1, sent.CommandLineOptionMessageList[1].MinimumArity);
        Assert.AreEqual(1, sent.CommandLineOptionMessageList[1].MaximumArity);
    }

    [TestMethod]
    public async Task SendMessageAsync_DispatchesEachTypedRequestWithApplicationToken()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        fixture.Client.RespondVoid<DiscoveredTestMessages>(TestContext.CancellationToken);
        fixture.Client.RespondVoid<TestResultMessages>(TestContext.CancellationToken);
        fixture.Client.RespondVoid<FileArtifactMessages>(TestContext.CancellationToken);
        fixture.Client.RespondVoid<TestSessionEvent>(TestContext.CancellationToken);
        fixture.Client.RespondVoid<AzureDevOpsLogMessage>(TestContext.CancellationToken);
        fixture.Client.RespondVoid<DisplayMessage>(TestContext.CancellationToken);
        var discovered = new DiscoveredTestMessages("execution", "instance", []);
        var results = new TestResultMessages("execution", "instance", [], []);
        var artifacts = new FileArtifactMessages("execution", "instance", []);
        var session = new TestSessionEvent(SessionEventTypes.TestSessionEnd, "session", "execution");
        var log = new AzureDevOpsLogMessage("execution", "instance", "##[warning]detail");
        var display = new DisplayMessage("execution", "instance", DisplayMessageLevels.Error, "error detail");
        await fixture.Connection.SendMessageAsync(discovered);
        await fixture.Connection.SendMessageAsync(results);
        await fixture.Connection.SendMessageAsync(artifacts);
        await fixture.Connection.SendMessageAsync(session);
        await fixture.Connection.SendMessageAsync(log);
        await fixture.Connection.SendMessageAsync(display);
        Assert.AreSequenceEqual<object>([discovered, results, artifacts, session, log, display], fixture.Client.Mock.Invocations.Select(invocation => invocation.Arguments[0]));
        Assert.IsTrue(fixture.Client.Mock.Invocations.All(invocation => (CancellationToken)invocation.Arguments[1] == TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DisconnectedOperationsRejectRequestsAndSetupWithoutServerDoesNotConnect()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        SetField(fixture.Connection, "_transportClient", null);
        Assert.IsFalse(fixture.Connection.IsServerMode);
        await fixture.Connection.AfterCommonServiceSetupAsync();
        InvalidOperationException help = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Connection.HelpInvokedAsync());
        InvalidOperationException handshake = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Connection.IsCompatibleProtocolAsync("host"));
        InvalidOperationException message = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Connection.SendMessageAsync(new DisplayMessage(null, null, 0, "detail")));
        Assert.AreEqual("The dotnet test transport client is not connected.", help.Message);
        Assert.AreEqual(help.Message, handshake.Message);
        Assert.AreEqual(help.Message, message.Message);
        fixture.Environment.Verify(environment => environment.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        await fixture.Connection.OnExitAsync();
    }

    [TestMethod]
    public async Task OnExitAsync_CancelsExistingListenerAndDisposeClosesTransportEvenWhenListenerFaults()
    {
        using ConnectionFixture fixture = CreateConnection(TestContext.CancellationToken);
        CancellationTokenSource listenerCancellation = new();
        SetField(fixture.Connection, "_serverControlListenerCts", listenerCancellation);
        SetField(fixture.Connection, "_serverControlListenerTask", Task.FromException(new InvalidOperationException("listener")));
        fixture.Client.SetConnected();
        Assert.IsTrue(fixture.Connection.IsServerMode);
        await fixture.Connection.OnExitAsync();
        Assert.IsTrue(listenerCancellation.IsCancellationRequested);
        fixture.Dispose();
        Assert.ContainsSingle(fixture.Client.Mock.Invocations.Where(invocation => invocation.Method.Name == "Dispose"));
    }

    [TestMethod]
    [DataRow("1.0.0", "1.0.0", true, DisplayName = "SingleEntryExactMatch")]
    [DataRow("1.1.0", "1.0.0;1.1.0;1.2.0", true, DisplayName = "MultipleEntriesMiddleMatch")]
    [DataRow("1.2.0", "1.0.0;1.1.0;1.2.0", true, DisplayName = "MultipleEntriesLastMatch")]
    [DataRow("2.0.0", "1.0.0;1.1.0;1.2.0", false, DisplayName = "VersionNotInList")]
    [DataRow("1.2", "1.2.0", false, DisplayName = "PartialVersionIsNotAMatch")]
    [DataRow("1.0", "1.0.0;1.10.0", false, DisplayName = "VersionSubstringOfAnotherEntryIsNotAMatch")]
    public void IsVersionCompatible_ReturnsExpectedResult(string protocolVersion, string supportedProtocolVersions, bool expected)
    {
        bool actual = DotnetTestConnection.IsVersionCompatible(protocolVersion, supportedProtocolVersions);

        Assert.AreEqual(expected, actual);
    }

    internal static ConnectionFixture CreateConnection(CancellationToken token, CommandLineParseOption[]? options = null, string? tool = null, ICommandLineOptionsProvider[]? providers = null, ILogger? logger = null)
    {
        var environment = new Mock<IEnvironment>();
        environment.SetupGet(value => value.ProcessId).Returns(123);
        environment.Setup(value => value.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)).Returns("execution");
        var module = new Mock<ITestApplicationModuleInfo>();
        module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns("test-module.dll");
        var handler = new CommandLineHandler(
            new CommandLineParseResult(tool, options ?? [], []), providers ?? [], [], module.Object, Mock.Of<IRuntimeFeature>());
        var cancellation = new Mock<ITestApplicationCancellationTokenSource>();
        cancellation.SetupGet(value => value.CancellationToken).Returns(token);
        ConnectionClient client = new();
        DotnetTestConnection connection = logger is null
            ? new DotnetTestConnection(handler, environment.Object, module.Object, cancellation.Object)
            : new DotnetTestConnection(handler, environment.Object, module.Object, cancellation.Object, logger);
        SetField(connection, "_transportClient", client.Object);
        return new ConnectionFixture(connection, client, environment);
    }

    internal static Task<bool> NegotiateCoverageSummaryAsync(
        ConnectionFixture fixture, CancellationToken token, string? baseVersion = "1.3.0", string? coverageVersion = "1")
    {
        Dictionary<byte, string> properties = [];
        if (baseVersion is not null)
        {
            properties[HandshakeMessagePropertyNames.SupportedProtocolVersions] = baseVersion;
        }

        if (coverageVersion is not null)
        {
            properties[20] = coverageVersion;
        }

        fixture.Client.Respond(new HandshakeMessage(properties), token);
        return fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHost);
    }

    private static void SetField(DotnetTestConnection connection, string name, object? value)
        => typeof(DotnetTestConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(connection, value);

    private static object? GetField(DotnetTestConnection connection, string name)
        => typeof(DotnetTestConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection);

    private static async Task NegotiateServerControlChannelAsync(ConnectionFixture fixture, CancellationToken token)
    {
        SetField(fixture.Connection, "_transportKind", DotnetTestTransportKind.NamedPipe);
        fixture.Client.Respond(
            new HandshakeMessage(new()
            {
                [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0",
                [HandshakeMessagePropertyNames.ServerControlPipeName] = "server-control",
            }),
            token);

        Assert.IsTrue(await fixture.Connection.IsCompatibleProtocolAsync(HandshakeMessageHostTypes.TestHostController));
        Assert.IsTrue(fixture.Connection.IsServerControlChannelSupported);
    }

    // Embedded protocol types are deliberately hidden from the C# importer. The IPC tests
    // have their own source-linked contracts; those cannot implement this connection's field.
    // Reflect only the contract identity, then let Moq invoke the real implementation.
    internal sealed class ConnectionClient
    {
        private readonly Type _type = typeof(DotnetTestConnection).GetField("_transportClient", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;

        public ConnectionClient() => Mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(_type))!;

        public Mock Mock { get; }

        public object Object => Mock.Object;

        public void Respond(HandshakeMessage response, CancellationToken token, Action<HandshakeMessage>? capture = null)
            => Configure(typeof(HandshakeMessage), typeof(HandshakeMessage), response, token, value => capture?.Invoke((HandshakeMessage)value));

        public void RespondVoid<TRequest>(CancellationToken token, Action<TRequest>? capture = null)
        {
            Type responseType = typeof(DotnetTestConnection).Assembly.GetType("Microsoft.Testing.Platform.IPC.Models.VoidResponse", throwOnError: true)!;
            Configure(typeof(TRequest), responseType, Activator.CreateInstance(responseType)!, token, value => capture?.Invoke((TRequest)value));
        }

        public void SetConnected()
            => typeof(ConnectionClient).GetMethod(nameof(ConfigureConnected), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(_type).Invoke(null, [Mock]);

        private void Configure(Type request, Type response, object value, CancellationToken token, Action<object> capture)
            => typeof(ConnectionClient).GetMethod(nameof(ConfigureResponse), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(_type, request, response).Invoke(null, [Mock, value, token, capture]);

        private static void ConfigureConnected<TClient>(Mock<TClient> mock)
            where TClient : class
        {
            ParameterExpression client = Expression.Parameter(typeof(TClient), "client");
            mock.Setup(Expression.Lambda<Func<TClient, bool>>(Expression.Property(client, "IsConnected"), client)).Returns(true);
        }

        private static void ConfigureResponse<TClient, TRequest, TResponse>(Mock<TClient> mock, object response, CancellationToken token, Action<object> capture)
            where TClient : class
        {
            ParameterExpression client = Expression.Parameter(typeof(TClient), "client");
            MethodInfo method = typeof(TClient).GetMethod("RequestReplyAsync")!.MakeGenericMethod(typeof(TRequest), typeof(TResponse));
            MethodInfo any = typeof(It).GetMethod(nameof(It.IsAny))!.MakeGenericMethod(typeof(TRequest));
            MethodCallExpression call = Expression.Call(client, method, Expression.Call(any), Expression.Constant(token));
            mock.Setup(Expression.Lambda<Func<TClient, Task<TResponse>>>(call, client))
                .Callback<TRequest, CancellationToken>((request, _) => capture(request!))
                .ReturnsAsync((TResponse)response);
        }
    }

    internal sealed class ConnectionFixture(DotnetTestConnection connection, ConnectionClient client, Mock<IEnvironment> environment) : IDisposable
    {
        private bool _disposed;

        public DotnetTestConnection Connection { get; } = connection;

        public ConnectionClient Client { get; } = client;

        public Mock<IEnvironment> Environment { get; } = environment;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Connection.Dispose();
            }
        }
    }

    [TestMethod]
    [UnsupportedOSPlatform("browser")]
#if !NETFRAMEWORK
    [UnsupportedOSPlatform("wasi")]
#endif
    public async Task Connection_RealSetupNegotiatesIdentityAndReverseCancellation()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("connection-wire");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = child.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();
        Assert.AreSequenceEqual(
            ["primary-handshakes:1", "execution-id:generated", "instance-id:negotiated", "reverse-cancels:1", "primary-disconnected:True"],
            records);
    }

    [UnsupportedOSPlatform("browser")]
#if !NETFRAMEWORK
    [UnsupportedOSPlatform("wasi")]
#endif
    internal static async Task RunConnectionWireChildAsync(Action<string> record)
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID, null);
        SystemEnvironment environment = new();
        using CTRLPlusCCancellationTokenSource application = new();
        Mock<ILoggerFactory> factory = new();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        using TestHostControllerCancellationServer control = new(null, environment, factory.Object, new SystemTask());
        control.Start();
        int handshakes = 0;
        string? wireInstance = null;
        string? executionId = null;
        OwnedProtocolServerPeer primary = new(
            (id, payload) =>
            {
                Assert.AreEqual(9, id);
                HandshakeMessage handshake = OwnedProtocolServerPeer.Deserialize<HandshakeMessage>(new HandshakeMessageSerializer(), payload);
                Assert.AreEqual("TestHost", handshake.Properties![HandshakeMessagePropertyNames.HostType]);
                Assert.AreEqual("run", handshake.Properties[HandshakeMessagePropertyNames.ExecutionMode]);
                Assert.AreEqual("wire-fixture.dll", handshake.Properties[HandshakeMessagePropertyNames.ModulePath]);
                Assert.AreEqual(environment.ProcessId.ToString(CultureInfo.InvariantCulture), handshake.Properties[HandshakeMessagePropertyNames.PID]);
                wireInstance = handshake.Properties[HandshakeMessagePropertyNames.InstanceId];
                executionId = handshake.Properties[HandshakeMessagePropertyNames.ExecutionId];
                Interlocked.Increment(ref handshakes);
                return (9, OwnedProtocolServerPeer.Serialize(new HandshakeMessageSerializer(), new HandshakeMessage(new()
                {
                    [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.4.0",
                    [HandshakeMessagePropertyNames.ServerControlPipeName] = control.PipeName,
                })));
            });
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.GetCurrentTestApplicationFullPath()).Returns("wire-fixture.dll");
        CommandLineHandler handler = new(
            CommandLineParser.Parse(["--server", "dotnettestcli", "--dotnet-test-pipe", primary.PipeName], environment),
            [], [new PlatformCommandLineProvider()], module.Object, Mock.Of<IRuntimeFeature>());
        ConcurrentQueue<string> logs = new();
        Mock<ILogger> logger = new();
        logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, text, _, _) => logs.Enqueue(text))
            .Returns(Task.CompletedTask);
        DotnetTestConnection connection = new(handler, environment, module.Object, application, logger.Object);
        try
        {
            await connection.AfterCommonServiceSetupAsync();
            Assert.IsTrue(connection.IsServerMode);
            Assert.IsTrue(await connection.IsCompatibleProtocolAsync("TestHost"));
            Assert.IsTrue(connection.IsServerControlChannelSupported);
            Assert.AreEqual(1, handshakes);
            Assert.IsTrue(Guid.TryParseExact(executionId, "N", out Guid execution));
            Assert.AreNotEqual(Guid.Empty, execution);
            IPushOnlyProtocol protocol = connection;
            Assert.AreEqual(protocol.InstanceId, wireInstance);
            Assert.IsTrue(Guid.TryParseExact(wireInstance, "N", out Guid instance));
            Assert.AreNotEqual(Guid.Empty, instance);
            Assert.AreNotEqual(execution, instance);
            Assert.AreEqual(executionId, environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID));
            int cancellations = 0;
            TaskCompletionSource<CancellationToken> canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await protocol.StartServerControlChannelAsync(token =>
            {
                Interlocked.Increment(ref cancellations);
                canceled.TrySetResult(token);
                return Task.CompletedTask;
            });
            await control.WaitForRequestAsync().TimeoutAfterAsync(TimeSpan.FromSeconds(30));
            control.RequestCancellation();
            await canceled.Task.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
            Assert.AreEqual(application.CancellationToken, await canceled.Task);
            Assert.IsFalse(application.CancellationToken.IsCancellationRequested);
            await protocol.StartServerControlChannelAsync(_ => throw new AssertFailedException("Control channel was started twice."));
            await protocol.OnExitAsync();
            Assert.AreEqual(1, cancellations);
            Assert.Contains($"Connected to the server control pipe '{control.PipeName}'.", logs);
            record("primary-handshakes:" + handshakes.ToString(CultureInfo.InvariantCulture));
            record("execution-id:generated");
            record("instance-id:negotiated");
            record("reverse-cancels:" + cancellations.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            connection.Dispose();
            await primary.WaitForDisconnectAsync();
            record("primary-disconnected:True");
            await primary.DisposeAsync();
        }
    }
}
