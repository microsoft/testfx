// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                // Timeout for more than 3 seconds
                Thread.Sleep(10_000);
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

        Dictionary<string, string> capturedProperties = new();
        using ManualResetEventSlim trackEventCalled = new(initialState: false);
        Mock<ITelemetryClient> testTelemetryClient = new();
        testTelemetryClient.Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, double>>()))
            .Callback((string _, Dictionary<string, string> properties, Dictionary<string, double> _) =>
            {
                foreach (KeyValuePair<string, string> pair in properties)
                {
                    capturedProperties[pair.Key] = pair.Value;
                }

                trackEventCalled.Set();
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
        Assert.IsTrue(trackEventCalled.Wait(TimeSpan.FromSeconds(30)), "Telemetry consumer did not invoke TrackEvent within the timeout.");

#if NETCOREAPP
        await appInsightsProvider.DisposeAsync();
#else
        appInsightsProvider.Dispose();
#endif

        Assert.IsTrue(capturedProperties.TryGetValue("my.bool", out string? value), "Expected 'my.bool' property in tracked event.");
        Assert.AreEqual(TelemetryProperties.True, value);
    }
}
