#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryDataConsumerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConsumeAsync_SupersededRetryAttempt_IsIgnored()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["uid"], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", new FailedTestNodeStateProperty(), new RetryAttemptProperty(1, isSuperseded: true)),
            TestContext.CancellationToken);
        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, fixture.Server.TotalTestRan);
        Assert.AreEqual(0, fixture.Server.FailedTestResults);
        Assert.AreSequenceEqual(["uid"], fixture.Server.RecoveredTests);
        Assert.IsEmpty(fixture.Server.FailedTests);
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task ConsumeAsync_FoldedTestWithPassingAndFailingRows_IsNotRecoveredRegardlessOfOrder(bool passFirst)
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["uid"], TestContext.CancellationToken);
        TestNodeUpdateMessage passed = CreateUpdate("uid", PassedTestNodeStateProperty.CachedInstance);
        TestNodeUpdateMessage failed = CreateUpdate("uid", new FailedTestNodeStateProperty());

        await fixture.Consumer.ConsumeAsync(null!, passFirst ? passed : failed, TestContext.CancellationToken);
        await fixture.Consumer.ConsumeAsync(null!, passFirst ? failed : passed, TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(2, fixture.Server.TotalTestRan);
        Assert.AreEqual(1, fixture.Server.FailedTestResults);
        Assert.IsEmpty(fixture.Server.RecoveredTests);
        Assert.IsTrue(fixture.Server.FailedTests.ContainsKey("uid"));
    }

    [TestMethod]
    public async Task ConsumeAsync_SkippedRetriedTest_IsNotRecovered()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["uid"], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", SkippedTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, fixture.Server.TotalTestRan);
        Assert.AreEqual(1, fixture.Server.SkippedTests);
        Assert.IsEmpty(fixture.Server.RecoveredTests);
    }

    [TestMethod]
    public async Task ConsumeAsync_PassedTests_RecoversOnlyTestsFromRetrySet()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["retried"], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("retried", PassedTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("not-retried", PassedTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(2, fixture.Server.TotalTestRan);
        Assert.AreSequenceEqual(["retried"], fixture.Server.RecoveredTests);
    }

    [TestMethod]
    public async Task ConsumeAsync_MessagesWithoutFinalOutcome_AreIgnored()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["uid"], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(null!, CreateUpdate("uid"), TestContext.CancellationToken);
        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", InProgressTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, fixture.Server.TotalTestRan);
        Assert.AreEqual(0, fixture.Server.SkippedTests);
        Assert.IsEmpty(fixture.Server.RecoveredTests);
        Assert.IsEmpty(fixture.Server.FailedTests);
    }

    [DataRow("failed")]
    [DataRow("error")]
    [DataRow("timeout")]
    [DataRow("cancelled")]
    [TestMethod]
    public async Task ConsumeAsync_NonPassingState_ReportsFailureAndDoesNotRecover(string state)
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(["uid"], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", CreateNonPassingState(state)),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, fixture.Server.TotalTestRan);
        Assert.AreEqual(1, fixture.Server.FailedTestResults);
        Assert.IsEmpty(fixture.Server.RecoveredTests);
        Assert.AreEqual("uid", fixture.Server.FailedTests["uid"]);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_NullRetrySet_DoesNotEnableRecoveryTracking()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync(
            ["uid"],
            TestContext.CancellationToken,
            startSessionBeforeConnecting: true);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", PassedTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.IsEmpty(fixture.Server.RecoveredTests);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_EmptyRetrySet_DoesNotEnableRecoveryTracking()
    {
        using ConnectedConsumer fixture = await ConnectedConsumer.CreateAsync([], TestContext.CancellationToken);

        await fixture.Consumer.ConsumeAsync(
            null!,
            CreateUpdate("uid", PassedTestNodeStateProperty.CachedInstance),
            TestContext.CancellationToken);
        await fixture.FinishAsync(TestContext.CancellationToken);

        Assert.IsEmpty(fixture.Server.RecoveredTests);
    }

    private static TestNodeUpdateMessage CreateUpdate(string uid, params IProperty[] properties)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(properties),
            });

    private static TestNodeStateProperty CreateNonPassingState(string state)
        => state switch
        {
            "failed" => new FailedTestNodeStateProperty(),
            "error" => new ErrorTestNodeStateProperty(),
            "timeout" => new TimeoutTestNodeStateProperty(),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            "cancelled" => new CancelledTestNodeStateProperty(),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unexpected test state."),
        };

    private static ITestSessionContext CreateSessionContext(CancellationToken cancellationToken)
        => Mock.Of<ITestSessionContext>(context => context.CancellationToken == cancellationToken);

    private static ServiceProvider CreateServiceProvider()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new SystemEnvironment());
        serviceProvider.AddService(new SystemTask());
        serviceProvider.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(
            source => source.CancellationToken == CancellationToken.None));
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        serviceProvider.AddService(loggerFactory.Object);
        return serviceProvider;
    }

    private sealed class ConnectedConsumer : IDisposable
    {
        private readonly RetryLifecycleCallbacks _lifecycleCallbacks;

        private ConnectedConsumer(
            RetryDataConsumer consumer,
            RetryLifecycleCallbacks lifecycleCallbacks,
            RetryFailedTestsPipeServer server)
        {
            Consumer = consumer;
            _lifecycleCallbacks = lifecycleCallbacks;
            Server = server;
        }

        public RetryDataConsumer Consumer { get; }

        public RetryFailedTestsPipeServer Server { get; }

        public static async Task<ConnectedConsumer> CreateAsync(
            string[] retrySet,
            CancellationToken cancellationToken,
            bool startSessionBeforeConnecting = false)
        {
            ServiceProvider serviceProvider = CreateServiceProvider();
            var server = new RetryFailedTestsPipeServer(serviceProvider, retrySet, Mock.Of<ILogger>());
            serviceProvider.AddService(new TestCommandLineOptions(new()
            {
                [RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = [server.PipeName],
            }));
            var lifecycleCallbacks = new RetryLifecycleCallbacks(serviceProvider);
            serviceProvider.AddService(lifecycleCallbacks);
            var consumer = new RetryDataConsumer(serviceProvider);
            await consumer.InitializeAsync();

            if (startSessionBeforeConnecting)
            {
                await consumer.OnTestSessionStartingAsync(CreateSessionContext(cancellationToken));
            }

            Task connection = server.WaitForConnectionAsync(cancellationToken);
            await lifecycleCallbacks.BeforeRunAsync(cancellationToken);
            await connection;
            if (!startSessionBeforeConnecting)
            {
                await consumer.OnTestSessionStartingAsync(CreateSessionContext(cancellationToken));
            }

            return new ConnectedConsumer(consumer, lifecycleCallbacks, server);
        }

        public Task FinishAsync(CancellationToken cancellationToken)
            => Consumer.OnTestSessionFinishingAsync(CreateSessionContext(cancellationToken));

        public void Dispose()
        {
            _lifecycleCallbacks.Dispose();
            Server.Dispose();
        }
    }
}
