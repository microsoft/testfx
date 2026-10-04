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

    private static readonly FieldInfo IsDisposedField =
        typeof(AppInsightsProvider).GetField("_isDisposed", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve AppInsightsProvider._isDisposed.");

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

        Assert.AreEqual("converted", properties["custom"]);
    }

    [TestMethod]
    public async Task LogEvent_WithNullProperty_UsesEmptyString()
    {
        (Dictionary<string, string> properties, _) = await TrackEventAsync(
            new Dictionary<string, object> { ["custom"] = null! });

        Assert.AreEqual(string.Empty, properties["custom"]);
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
