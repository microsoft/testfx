// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#endif

using Microsoft.Testing.Extensions.Telemetry;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AppInsightsProviderTests
{
    private const BindingFlags StaticNonPublicBindingFlags = BindingFlags.Static | BindingFlags.NonPublic;

#if NETCOREAPP
    private static readonly FieldInfo IsDisposedField =
        typeof(AppInsightsProvider).GetField("_isDisposed", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider._isDisposed.");
#endif

    private static readonly PropertyInfo ContinueOnCapturedContextProperty =
        typeof(AppInsightsProvider).GetProperty("ContinueOnCapturedContext", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.ContinueOnCapturedContext.");

    private static readonly MethodInfo StartTelemetryTaskAsyncMethod =
        typeof(AppInsightsProvider).GetMethod("StartTelemetryTaskAsync", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.StartTelemetryTaskAsync.");

#if DEBUG
    private static readonly MethodInfo IsKnownUnhashedPropertyMethod =
        typeof(AppInsightsProvider).GetMethod("IsKnownUnhashedProperty", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.IsKnownUnhashedProperty.");

    private static readonly MethodInfo IsValidHashMethod =
        typeof(AppInsightsProvider).GetMethod("IsValidHash", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.IsValidHash.");
#endif

#if NETCOREAPP
    private static readonly MethodInfo CreatePayloadChannelOptionsMethod =
        typeof(AppInsightsProvider).GetMethod("CreatePayloadChannelOptions", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.CreatePayloadChannelOptions.");

    private static readonly MethodInfo WaitForTelemetryTaskAsyncMethod =
        typeof(AppInsightsProvider).GetMethod("WaitForTelemetryTaskAsync", StaticNonPublicBindingFlags)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider.WaitForTelemetryTaskAsync.");
#endif

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SessionIdEnvironmentVariable_HasExpectedName()
        => Assert.AreEqual("TESTINGPLATFORM_APPINSIGHTS_SESSIONID", AppInsightsProvider.SessionIdEnvVar);

    [TestMethod]
    public void ContinueOnCapturedContext_IsDisabled()
        => Assert.IsFalse((bool)ContinueOnCapturedContextProperty.GetValue(null)!);

#if NETCOREAPP
    [TestMethod]
    public void CreatePayloadChannelOptions_ConfiguresSingleReaderAndAsynchronousContinuations()
    {
        var options = (UnboundedChannelOptions)CreatePayloadChannelOptionsMethod.Invoke(null, null)!;

        Assert.IsTrue(options.SingleReader);
        Assert.IsFalse(options.SingleWriter);
        Assert.IsFalse(options.AllowSynchronousContinuations);
    }
#endif

    [TestMethod]
    public void StartTelemetryTask_OnSingleThreadedRuntime_DoesNotScheduleIngestLoop()
    {
        Mock<ITask> task = new(MockBehavior.Strict);

        var telemetryTask = (Task)StartTelemetryTaskAsyncMethod.Invoke(
            null,
            [false, task.Object, (Func<Task>)(static () => Task.CompletedTask), CancellationToken.None])!;

        Assert.AreSame(Task.CompletedTask, telemetryTask);
        task.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void StartTelemetryTask_OnMultiThreadedRuntime_SchedulesIngestLoop()
    {
        Mock<ITask> task = new(MockBehavior.Strict);
        Func<Task> ingestLoop = static () => Task.CompletedTask;
        Task scheduledTask = Task.FromResult(true);
#if NETCOREAPP
        task.Setup(x => x.Run(ingestLoop, CancellationToken.None)).Returns(scheduledTask);
#else
        task.Setup(x => x.RunLongRunning(ingestLoop, "AppInsights telemetry ingest", CancellationToken.None)).Returns(scheduledTask);
#endif

        var telemetryTask = (Task)StartTelemetryTaskAsyncMethod.Invoke(
            null,
            [true, task.Object, ingestLoop, CancellationToken.None])!;

        Assert.AreSame(scheduledTask, telemetryTask);
#if NETCOREAPP
        task.Verify(x => x.Run(ingestLoop, CancellationToken.None), Times.Once);
#else
        task.Verify(x => x.RunLongRunning(ingestLoop, "AppInsights telemetry ingest", CancellationToken.None), Times.Once);
#endif
        task.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisposeWithoutPayload_DoesNotInitializeTelemetryClient()
    {
        Mock<ITelemetryClient> telemetryClient = new();
        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(telemetryClient.Object);

        AppInsightsProvider appInsightsProvider = CreateProvider(telemetryClientFactory);

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        telemetryClientFactory.Verify(x => x.Create(It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
        telemetryClient.Verify(x => x.Flush(), Times.Never);
    }

#if !NETCOREAPP
    [TestMethod]
    public void Constructor_StartsTelemetryConsumerOnDedicatedThread()
    {
        Mock<ITask> task = new();
        task.Setup(x => x.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ = new AppInsightsProvider(
            new Mock<IEnvironment>().Object,
            new Mock<ITestApplicationCancellationTokenSource>().Object,
            task.Object,
            new Mock<ILoggerFactory>().Object,
            new Mock<IClock>().Object,
            new Mock<IConfiguration>().Object,
            new Mock<ITelemetryInformation>().Object,
            new Mock<ITelemetryClientFactory>().Object,
            "sessionId");

        task.Verify(
            x => x.RunLongRunning(It.IsAny<Func<Task>>(), "AppInsights telemetry ingest", It.IsAny<CancellationToken>()),
            Times.Once);
        task.Verify(
            x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
#endif

    [TestMethod]
    public async Task FirstPayload_InitializesTelemetryClientOnce()
    {
        TaskCompletionSource<bool> eventTracked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> telemetryClient = new();
        telemetryClient
            .Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> _, Dictionary<string, double> _) => eventTracked.TrySetResult(true));
        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(telemetryClient.Object);

        AppInsightsProvider appInsightsProvider = CreateProvider(telemetryClientFactory);
        telemetryClientFactory.Verify(x => x.Create(It.IsAny<string?>(), It.IsAny<string>()), Times.Never);

        await appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);
        await WaitForSignalAsync(eventTracked.Task, "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        telemetryClientFactory.Verify(x => x.Create("sessionId", "osVersion"), Times.Once);
        telemetryClient.Verify(
            x => x.TrackEvent("Sample", It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ClientInitializationFailure_IsLogged()
    {
        InvalidOperationException exception = new("Initialization failed");
        TaskCompletionSource<bool> initializationFailureLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Throws(exception);
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Error, It.IsAny<string>(), It.IsAny<Exception>(), LoggingExtensions.Formatter))
            .Callback((LogLevel _, string _, Exception? _, Func<string, Exception?, string> _) => initializationFailureLogged.TrySetResult(true))
            .Returns(Task.CompletedTask);

        AppInsightsProvider appInsightsProvider = CreateProvider(telemetryClientFactory, logger.Object);
        await appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);
        await appInsightsProvider.LogEventAsync("Sample2", new Dictionary<string, object>(), CancellationToken.None);
        await WaitForSignalAsync(initializationFailureLogged.Task, "Telemetry client initialization failure was not logged within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        telemetryClientFactory.Verify(x => x.Create("sessionId", "osVersion"), Times.Once);
        logger.Verify(
            x => x.LogAsync(LogLevel.Error, "Failed to initialize telemetry client", exception, LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public async Task FlushFailure_IsLogged()
    {
        InvalidOperationException exception = new("Flush failed");
        TaskCompletionSource<bool> eventTracked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> flushFailureLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> telemetryClient = new();
        telemetryClient
            .Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> _, Dictionary<string, double> _) => eventTracked.TrySetResult(true));
        telemetryClient.Setup(x => x.Flush()).Throws(exception);
        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(telemetryClient.Object);
        Mock<ILogger> logger = new();
        logger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);
        logger
            .Setup(x => x.LogAsync(LogLevel.Error, It.IsAny<string>(), It.IsAny<Exception>(), LoggingExtensions.Formatter))
            .Callback((LogLevel _, string _, Exception? _, Func<string, Exception?, string> _) => flushFailureLogged.TrySetResult(true))
            .Returns(Task.CompletedTask);

        AppInsightsProvider appInsightsProvider = CreateProvider(telemetryClientFactory, logger.Object);
        await appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);
        await WaitForSignalAsync(eventTracked.Task, "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        await WaitForSignalAsync(flushFailureLogged.Task, "Telemetry flush failure was not logged within the timeout.");

        logger.Verify(
            x => x.LogAsync(LogLevel.Error, "Error during telemetry flush.", exception, LoggingExtensions.Formatter),
            Times.Once);
    }

#if DEBUG
    [TestMethod]
    [DataRow("mstest.config_source")]
    [DataRow("mstest.attribute_usage")]
    [DataRow("mstest.custom_test_method_types")]
    [DataRow("mstest.custom_test_class_types")]
    [DataRow("mstest.assertion_usage")]
    [DataRow("mstest.setting.parallelization_scope")]
    public void IsKnownUnhashedProperty_ReturnsTrueForMSTestTelemetryProperty(string propertyName)
        => Assert.IsTrue((bool)IsKnownUnhashedPropertyMethod.Invoke(null, [propertyName])!);

    [TestMethod]
    public void IsValidHash_RequiresExactly64LowercaseHexCharacters()
    {
        string validHash = new('a', 64);

        Assert.IsTrue((bool)IsValidHashMethod.Invoke(null, [validHash])!);
        Assert.IsFalse((bool)IsValidHashMethod.Invoke(null, [validHash + "a"])!);
        Assert.IsFalse((bool)IsValidHashMethod.Invoke(null, [new string('A', 64)])!);
    }

    [TestMethod]
    public async Task LogEvent_WithValidHashProperty_PreservesValue()
    {
        string validHash = new('a', 64);

        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object> { ["custom.hash"] = validHash });

        Assert.AreEqual(validHash, properties["custom.hash"]);
    }
#endif

    [TestMethod]
    public async Task LogEvent_AddsCommonProperties()
    {
        Mock<ITelemetryInformation> telemetryInformation = new();
        telemetryInformation.SetupGet(x => x.Version).Returns("20");

        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object>(),
            telemetryInformation: telemetryInformation.Object);

        Assert.AreEqual("20", properties[TelemetryProperties.VersionPropertyName]);
        Assert.AreEqual("sessionId", properties[TelemetryProperties.SessionId]);
        Assert.IsTrue(Guid.TryParse(properties[TelemetryProperties.ReporterIdPropertyName], out _));
        Assert.AreEqual(TelemetryProperties.False, properties[TelemetryProperties.IsCIPropertyName]);
    }

    [TestMethod]
    [DataRow("COPILOT_CLI", "1", "true")]
    [DataRow("CLAUDECODE", "1", "true")]
    [DataRow("AI_AGENT", "github_copilot_app_agent", "true")]
    [DataRow("AI_AGENT", "unknown", "false")]
    [DataRow("COPILOT_CLI", "", "false")]
    [DataRow("COPILOT_CLI", null, "false")]
    public async Task LogEvent_ReportsLLMContext(string variable, string? value, string expected)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(variable)).Returns(value);

        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object>(),
            environment: environment.Object);

        Assert.ContainsEntry("is llm", expected, properties);
        Assert.ContainsEntry("CopilotAgentSessionId", "not_present", properties);
    }

    [TestMethod]
    [DataRow(null, "not_present")]
    [DataRow("", "not_present")]
    [DataRow("aBCDEfAB-1234-1567-8ABC-DEFaBCDEF012", "aBCDEfAB-1234-1567-8ABC-DEFaBCDEF012")]
    [DataRow("aBCDEfAB-1234-4567-9ABC-DEFaBCDEF012", "aBCDEfAB-1234-4567-9ABC-DEFaBCDEF012")]
    [DataRow("aBCDEfAB-1234-8567-bABC-DEFaBCDEF012", "aBCDEfAB-1234-8567-bABC-DEFaBCDEF012")]
    [DataRow("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    [DataRow("build-investigation-42", "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2")]
    [DataRow("BUILD-INVESTIGATION-42", "8495a77b0853a7baf0eaccc4f2cb4b53bd36872da0f13744527a2f8c4142a30f")]
    [DataRow(" build-investigation-42 ", "d7bdcf1b188a689f9649ea46a5257b1e5310a1780ce5e884752643cad50ad9f5")]
    [DataRow(" ", "36a9e7f1c95b82ffb99743e0c5c4ce95d83c9a430aac59f84ef3cbfab6145068")]
    [DataRow("not_present", "edcc84b8e36c26b675c1b102b0e42f3507eee0afe5fbda7c203e9cbb758390e3")]
    [DataRow("aBCDEfAB-1234-9567-8ABC-DEFaBCDEF012", "3409b6c07099e67716e185a5c8269e3548739d63d5e29e140b91654768e95708")]
    [DataRow("aBCDEfAB-1234-4567-CABC-DEFaBCDEF012", "114b0c8fb3a916f8d4ec944818aade5fa3b6ff70a8f03af7f348508ca1683920")]
    [DataRow("aBCDEfAB-1234-0567-8ABC-DEFaBCDEF012", "c31e983f514112a8db5f7badc6a10bd62fec9b8c6b8c4a4697996d5374c7b0fb")]
    [DataRow("{aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012}", "3c8cd2ea1393a621bec3d2bc462291191a0a67b1c20c4a599ea545ea1ed69eb7")]
    [DataRow("aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012\n", "de43912d6c54f9f27884ba6f98e7cd82f11eded3fd0551bfc48b471e41ade046", DisplayName = "Copilot UUID with trailing newline")]
    [DataRow("caf\u00e9", "850f7dc43910ff890f8879c0ed26fe697c93a067ad93a7d50f466a7028a9bf4e")]
    public async Task LogEvent_NormalizesCopilotSessionId(string? sessionId, string expected)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable("COPILOT_AGENT_SESSION_ID")).Returns(sessionId);

        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object>(),
            environment: environment.Object);

        Assert.ContainsEntry("CopilotAgentSessionId", expected, properties);
        Assert.DoesNotContain("copilot agent session id", properties.Keys);
        Assert.ContainsEntry("session id", "sessionId", properties);
        Assert.ContainsEntry("is llm", "false", properties);
    }

    [TestMethod]
    public async Task LogEvent_SnapshotsContextBeforeQueueingWithoutMutatingCallerProperties()
    {
        TaskCompletionSource<bool> startConsumer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> allEventsTracked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITask> task = new();
#if NETCOREAPP
        task.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> loop, CancellationToken _) => RunConsumerAsync(loop));
#else
        task.Setup(x => x.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> loop, string _, CancellationToken _) => RunConsumerAsync(loop));
#endif

        List<(string Name, Dictionary<string, string> Properties)> events = [];
        Mock<ITelemetryClient> client = new();
        client.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string name, Dictionary<string, string> properties, Dictionary<string, double> _) =>
            {
                events.Add((name, properties));
                if (events.Count == 3)
                {
                    allEventsTracked.TrySetResult(true);
                }
            });
        Mock<ITelemetryClientFactory> factory = new();
        factory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(client.Object);
        Mock<IEnvironment> environment = new();
        string? currentId = "build-investigation-42";
        string? currentAgent = "github_copilot_app_agent";
        environment.Setup(x => x.GetEnvironmentVariable("COPILOT_AGENT_SESSION_ID")).Returns(() => currentId);
        environment.Setup(x => x.GetEnvironmentVariable("AI_AGENT")).Returns(() => currentAgent);

        AppInsightsProvider provider = CreateProvider(factory, environment: environment.Object, task: task.Object);
        Dictionary<string, object> callerProperties = new()
        {
            ["is llm"] = false,
            ["CopilotAgentSessionId"] = "caller-supplied",
        };

        try
        {
            await provider.LogEventAsync("first", callerProperties, CancellationToken.None);
            currentId = "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012";
            currentAgent = null;
            await provider.LogEventAsync("second", callerProperties, CancellationToken.None);
            currentId = null;
            await provider.LogEventAsync("third", callerProperties, CancellationToken.None);
            currentId = "later-context";
            currentAgent = "github_copilot_app_agent";

            Assert.HasCount(2, callerProperties);
            Assert.IsFalse((bool)callerProperties["is llm"]);
            Assert.AreEqual("caller-supplied", callerProperties["CopilotAgentSessionId"]);
            callerProperties.Clear();
            startConsumer.SetResult(true);
            await WaitForSignalAsync(allEventsTracked.Task, "Telemetry consumer did not track the three queued events.");
        }
        finally
        {
            startConsumer.TrySetResult(true);
#if NETCOREAPP
            await provider.DisposeAsync();
#else
            provider.Dispose();
#endif
        }

        Assert.HasCount(3, events);
        Assert.AreEqual("first", events[0].Name);
        Assert.AreEqual("second", events[1].Name);
        Assert.AreEqual("third", events[2].Name);
        Assert.ContainsEntry("is llm", "true", events[0].Properties);
        Assert.ContainsEntry("CopilotAgentSessionId", "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2", events[0].Properties);
        Assert.ContainsEntry("is llm", "false", events[1].Properties);
        Assert.ContainsEntry("CopilotAgentSessionId", "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012", events[1].Properties);
        Assert.ContainsEntry("is llm", "false", events[2].Properties);
        Assert.ContainsEntry("CopilotAgentSessionId", "not_present", events[2].Properties);

        Task RunConsumerAsync(Func<Task> loop)
            => Task.Run(
                async () =>
                {
                    await startConsumer.Task;
                    await loop();
                },
                TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LogEvent_DevelopmentRepositoryProperty_MatchesConfiguration(bool isDevelopmentRepository)
    {
        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(x => x[PlatformConfigurationConstants.PlatformTelemetryIsDevelopmentRepository])
            .Returns(isDevelopmentRepository.ToString(CultureInfo.InvariantCulture));

        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object>(),
            configuration: configuration.Object);

        Assert.AreEqual(
            isDevelopmentRepository,
            properties.ContainsKey(TelemetryProperties.HostProperties.IsDevelopmentRepositoryPropertyName));
        if (isDevelopmentRepository)
        {
            Assert.AreEqual(
                TelemetryProperties.True,
                properties[TelemetryProperties.HostProperties.IsDevelopmentRepositoryPropertyName]);
        }
    }

    [TestMethod]
    public async Task LogEvent_WithConvertibleProperty_UsesStringRepresentation()
    {
        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object> { ["custom"] = new StringValue("converted") });

        Assert.ContainsEntry("custom", "converted", properties);
    }

    [TestMethod]
    public async Task LogEvent_WithNullProperty_UsesEmptyString()
    {
        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object> { ["custom"] = null! });

        Assert.ContainsEntry("custom", string.Empty, properties);
    }

    [TestMethod]
    public async Task LogEvent_WhenTraceEnabled_LogsEvent()
    {
        Mock<ILogger> logger = new();
        logger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true);
        logger
            .Setup(x => x.LogAsync(LogLevel.Trace, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);

        _ = await TrackEventAsync(
            new Dictionary<string, object> { ["custom"] = new StringValue("converted") },
            logger: logger.Object);

        logger.Verify(
            x => x.LogAsync(
                LogLevel.Trace,
                It.Is<string>(message =>
                    message.Contains("Send telemetry event: Sample")
                    && message.Contains("    custom: converted")),
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void Platform_CancellationToken_Cancellation_Should_Exit_Gracefully()
    {
        Mock<IEnvironment> environment = new();
        Mock<IClock> clock = new();
        Mock<IConfiguration> config = new();
        Mock<ITelemetryInformation> telemetryInformation = new();

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        ManualResetEvent loopInitialized = new(false);
        ManualResetEvent sample2Message = new(false);
        CancellationTokenSource cancellationTokenSource = new();
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(cancellationTokenSource.Token);

        List<string> events = [];
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
        .Callback((string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics) =>
        {
            loopInitialized.Set();
            events.Add(eventName);
            sample2Message.WaitOne();
        });

        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(testTelemetryClient.Object);

        AppInsightsProvider appInsightsProvider = new(
            environment.Object,
            testApplicationCancellationTokenSource.Object,
            new SystemTask(),
            loggerFactory.Object,
            clock.Object,
            config.Object,
            telemetryInformation.Object,
            telemetryClientFactory.Object,
            "sessionId");

        // Fire the consume loop
        _ = appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);

        // Wait for the consume loop
        loopInitialized.WaitOne();

        // Fire the consume loop
        _ = appInsightsProvider.LogEventAsync("Sample2", new Dictionary<string, object>(), CancellationToken.None);

        // Cancel the platform token
        cancellationTokenSource.Cancel();

        sample2Message.Set();

#if NETCOREAPP
        ValueTask valueTask = appInsightsProvider.DisposeAsync();
        while (!valueTask.IsCompleted)
        {
        }
#else
        appInsightsProvider.Dispose();
#endif

        // We expect to not consume the second event because we exit the inner loop for the cancellation token
        Assert.AreEqual("Sample", events.Single());
    }

    [TestMethod]
    public void Timeout_During_Dispose_Should_Exit_Gracefully()
    {
        Mock<IEnvironment> environment = new();
        Mock<IClock> clock = new();
        Mock<IConfiguration> config = new();
        Mock<ITelemetryInformation> telemetryInformation = new();

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        ManualResetEvent loopInitialized = new(false);
        CancellationTokenSource cancellationTokenSource = new();
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(cancellationTokenSource.Token);

        int calls = 0;
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
        .Callback((string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics) =>
        {
            if (calls == 0)
            {
                loopInitialized.Set();
                calls++;
                return;
            }

            if (calls == 1)
            {
                // Deliberately block the synchronous Moq callback to simulate a slow telemetry
                // client and validate that the provider's dispose path times out gracefully.
                // The signature of Moq's Callback is Action<...>, so an async alternative is not viable here.
#pragma warning disable MSTEST0067 // Avoid 'Thread.Sleep' in test code as it can cause test flakiness
                Thread.Sleep(10_000);
#pragma warning restore MSTEST0067
            }
        });

        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(testTelemetryClient.Object);

        AppInsightsProvider appInsightsProvider = new(
            environment.Object,
            testApplicationCancellationTokenSource.Object,
            new SystemTask(),
            loggerFactory.Object,
            clock.Object,
            config.Object,
            telemetryInformation.Object,
            telemetryClientFactory.Object,
            "sessionId");

        // Fire the consume loop
        _ = appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);

        // Wait for the consume loop
        loopInitialized.WaitOne();

        // Dispose the application token
        cancellationTokenSource.Dispose();

        // Fire the second loop that will timeout
        Task logTask = appInsightsProvider.LogEventAsync("Sample", new Dictionary<string, object>(), CancellationToken.None);
#if NETCOREAPP
        ValueTask valueTask = appInsightsProvider.DisposeAsync();
        while (!valueTask.IsCompleted)
        {
        }
#else
        appInsightsProvider.Dispose();
#endif
    }

    [TestMethod]
    public async Task LogEvent_WithBooleanProperty_ConvertsValueToTelemetryString()
    {
        Mock<IEnvironment> environment = new();
        Mock<IClock> clock = new();
        Mock<IConfiguration> config = new();
        Mock<ITelemetryInformation> telemetryInformation = new();

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        Dictionary<string, string> capturedProperties = [];
        TaskCompletionSource<bool> trackEventCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> properties, Dictionary<string, double> _) =>
            {
                foreach (KeyValuePair<string, string> pair in properties)
                {
                    capturedProperties[pair.Key] = pair.Value;
                }

                trackEventCalled.TrySetResult(true);
            });

        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(testTelemetryClient.Object);

        CancellationTokenSource cancellationTokenSource = new();
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(cancellationTokenSource.Token);

        AppInsightsProvider appInsightsProvider = new(
            environment.Object,
            testApplicationCancellationTokenSource.Object,
            new SystemTask(),
            loggerFactory.Object,
            clock.Object,
            config.Object,
            telemetryInformation.Object,
            telemetryClientFactory.Object,
            "sessionId");

        await appInsightsProvider.LogEventAsync(
            "Sample",
            new Dictionary<string, object> { ["my.bool"] = true },
            CancellationToken.None);

        // Wait for the consumer loop to actually invoke TrackEvent before disposing,
        // otherwise the dispose-time flush window can elapse on slower runners (notably net472)
        // before the payload is processed.
        await WaitForSignalAsync(trackEventCalled.Task, "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        Assert.IsTrue(capturedProperties.TryGetValue("my.bool", out string? value), "Expected 'my.bool' property in tracked event.");
        Assert.AreEqual(TelemetryProperties.True, value);
    }

    [TestMethod]
    [DataRow("None")]
    [DataRow("Result")]
    [DataRow("Live")]
    public async Task LogEvent_WithOutputCaptureMode_TracksWellKnownEnumValue(string mode)
    {
        Mock<IEnvironment> environment = new();
        Mock<IClock> clock = new();
        Mock<IConfiguration> config = new();
        Mock<ITelemetryInformation> telemetryInformation = new();

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        Dictionary<string, string> capturedProperties = [];
        TaskCompletionSource<bool> trackEventCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> properties, Dictionary<string, double> _) =>
            {
                foreach (KeyValuePair<string, string> pair in properties)
                {
                    capturedProperties[pair.Key] = pair.Value;
                }

                trackEventCalled.TrySetResult(true);
            });

        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(testTelemetryClient.Object);

        CancellationTokenSource cancellationTokenSource = new();
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(cancellationTokenSource.Token);

        AppInsightsProvider appInsightsProvider = new(
            environment.Object,
            testApplicationCancellationTokenSource.Object,
            new SystemTask(),
            loggerFactory.Object,
            clock.Object,
            config.Object,
            telemetryInformation.Object,
            telemetryClientFactory.Object,
            "sessionId");

        await appInsightsProvider.LogEventAsync(
            "Sample",
            new Dictionary<string, object> { ["mstest.setting.output_capture_mode"] = mode },
            CancellationToken.None);

        await WaitForSignalAsync(trackEventCalled.Task, "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        Assert.IsTrue(capturedProperties.TryGetValue("mstest.setting.output_capture_mode", out string? value), "Expected output capture mode property in tracked event.");
        Assert.AreEqual(mode, value);
    }

    [TestMethod]
    public async Task Dispose_FlushesTelemetryClientOnceAfterAllTrackedEvents()
    {
        // Regression: previously AppInsightTelemetryClient.TrackEvent called Flush() per event,
        // which serialized the ingest loop on each network round-trip. On slow Linux CI a
        // single slow flush could exhaust the 3-second dispose timeout before later payloads
        // (e.g. mstest sessionexit) were ever read from the channel and ship their
        // "Send telemetry event:" trace line. The fix removes per-event flushing and flushes
        // exactly once after the ingest loop drains.
        Mock<IEnvironment> environment = new();
        Mock<IClock> clock = new();
        Mock<IConfiguration> config = new();
        Mock<ITelemetryInformation> telemetryInformation = new();

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        List<string> trackedEvents = [];
        int flushCallCount = 0;
        TaskCompletionSource<bool> secondEventTracked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> flushInvoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string eventName, Dictionary<string, string> _, Dictionary<string, double> _) =>
            {
                lock (trackedEvents)
                {
                    trackedEvents.Add(eventName);
                    if (trackedEvents.Count >= 2)
                    {
                        secondEventTracked.TrySetResult(true);
                    }
                }
            });
        testTelemetryClient.Setup(x => x.Flush())
            .Callback(() =>
            {
                Interlocked.Increment(ref flushCallCount);
                flushInvoked.TrySetResult(true);
            });

        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(testTelemetryClient.Object);

        CancellationTokenSource cancellationTokenSource = new();
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(cancellationTokenSource.Token);

        AppInsightsProvider appInsightsProvider = new(
            environment.Object,
            testApplicationCancellationTokenSource.Object,
            new SystemTask(),
            loggerFactory.Object,
            clock.Object,
            config.Object,
            telemetryInformation.Object,
            telemetryClientFactory.Object,
            "sessionId");

        await appInsightsProvider.LogEventAsync("FirstEvent", new Dictionary<string, object>(), CancellationToken.None);
        await appInsightsProvider.LogEventAsync("SecondEvent", new Dictionary<string, object>(), CancellationToken.None);

        await WaitForSignalAsync(secondEventTracked.Task, "Telemetry consumer did not invoke TrackEvent for both events within the timeout.");

        // Flush must not have happened per-event: it should only occur during shutdown drain.
        Assert.AreEqual(0, Volatile.Read(ref flushCallCount), "Flush should not be invoked per-event; only once during dispose drain.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        // Dispose only waits up to 3 seconds for the ingest task to drain, and may proceed even
        // before the task reaches its finally block (especially under thread-pool pressure on CI).
        // Wait explicitly for the Flush callback so the assertions below are not racing the
        // background continuation.
        await WaitForSignalAsync(flushInvoked.Task, "Flush was not invoked within the timeout after dispose.");

        Assert.HasCount(2, trackedEvents);
        Assert.AreEqual("FirstEvent", trackedEvents[0]);
        Assert.AreEqual("SecondEvent", trackedEvents[1]);
        Assert.AreEqual(1, Volatile.Read(ref flushCallCount), "Flush should be invoked exactly once after the ingest loop drains.");
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_CompletesWriterAndMarksProviderDisposed()
    {
        Mock<ITask> task = new();
        task.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        AppInsightsProvider provider = CreateProvider(new Mock<ITelemetryClientFactory>(), task: task.Object);

        await provider.DisposeAsync();

        Assert.IsTrue((bool)IsDisposedField.GetValue(provider)!);
        await Assert.ThrowsExactlyAsync<ChannelClosedException>(
            () => provider.LogEventAsync("after-dispose", new Dictionary<string, object>(), CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_WaitsForTelemetryTask()
    {
        TaskCompletionSource<bool> telemetryTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITask> task = new();
        task.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(telemetryTask.Task);
        AppInsightsProvider provider = CreateProvider(new Mock<ITelemetryClientFactory>(), task: task.Object);

        ValueTask disposeTask = provider.DisposeAsync();

        Assert.IsFalse(disposeTask.IsCompleted);
        telemetryTask.SetResult(true);
        await disposeTask;
        Assert.IsTrue((bool)IsDisposedField.GetValue(provider)!);
    }

    [TestMethod]
    public async Task WaitForTelemetryTaskAsync_WhenTaskTimesOut_CancelsAndLogsWarning()
    {
        TaskCompletionSource<bool> telemetryTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource flushTimeoutOrStop = new();
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);

        var waitTask = (Task)WaitForTelemetryTaskAsyncMethod.Invoke(
            null,
            [telemetryTask.Task, flushTimeoutOrStop, logger.Object, 0])!;
        await waitTask;

        Assert.IsTrue(flushTimeoutOrStop.IsCancellationRequested);
        logger.Verify(
            x => x.LogAsync(
                LogLevel.Warning,
                "Telemetry task didn't flush after '0', some payload could be lost",
                null,
                LoggingExtensions.Formatter),
            Times.Once);

        telemetryTask.TrySetResult(true);
    }
#endif

    private async Task<(Dictionary<string, string> Properties, Dictionary<string, double> Metrics)> TrackEventAsync(
        IDictionary<string, object> paramsMap,
        string eventName = "Sample",
        ILogger? logger = null,
        IConfiguration? configuration = null,
        ITelemetryInformation? telemetryInformation = null,
        IEnvironment? environment = null)
    {
        TaskCompletionSource<(Dictionary<string, string>, Dictionary<string, double>)> eventTracked =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITelemetryClient> telemetryClient = new();
        telemetryClient
            .Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> properties, Dictionary<string, double> metrics) =>
                eventTracked.TrySetResult((properties, metrics)));
        Mock<ITelemetryClientFactory> telemetryClientFactory = new();
        telemetryClientFactory.Setup(x => x.Create(It.IsAny<string?>(), It.IsAny<string>())).Returns(telemetryClient.Object);

        AppInsightsProvider provider = CreateProvider(
            telemetryClientFactory,
            logger,
            configuration,
            telemetryInformation,
            environment);
        await provider.LogEventAsync(eventName, paramsMap, CancellationToken.None);
        await WaitForSignalAsync(eventTracked.Task, "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await provider.DisposeAsync();
#else
        provider.Dispose();
#endif

        return await eventTracked.Task;
    }

    private async Task WaitForSignalAsync(Task signal, string failureMessage)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var timeout = Task.Delay(TimeSpan.FromSeconds(30), timeoutCancellationTokenSource.Token);
        Task completedTask = await Task.WhenAny(signal, timeout);
        if (completedTask != signal)
        {
            TestContext.CancellationToken.ThrowIfCancellationRequested();
        }

        Assert.AreSame(signal, completedTask, failureMessage);
        timeoutCancellationTokenSource.Cancel();
        await signal;
    }

    private static AppInsightsProvider CreateProvider(
        Mock<ITelemetryClientFactory> telemetryClientFactory,
        ILogger? logger = null,
        IConfiguration? configuration = null,
        ITelemetryInformation? telemetryInformation = null,
        IEnvironment? environment = null,
        ITask? task = null)
    {
        if (environment is null)
        {
            Mock<IEnvironment> environmentMock = new();
            environmentMock.Setup(x => x.OsVersion).Returns("osVersion");
            environment = environmentMock.Object;
        }

        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        testApplicationCancellationTokenSource.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger ?? new Mock<ILogger>().Object);

        return new AppInsightsProvider(
            environment,
            testApplicationCancellationTokenSource.Object,
            task ?? new SystemTask(),
            loggerFactory.Object,
            new Mock<IClock>().Object,
            configuration ?? new Mock<IConfiguration>().Object,
            telemetryInformation ?? new Mock<ITelemetryInformation>().Object,
            telemetryClientFactory.Object,
            "sessionId");
    }

    private sealed class StringValue(string value)
    {
        public override string ToString()
            => value;
    }
}
