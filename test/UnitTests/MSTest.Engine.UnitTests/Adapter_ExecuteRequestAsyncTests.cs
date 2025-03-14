﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Framework.UnitTests;

[TestClass]
public class Adapter_ExecuteRequestAsyncTests : TestBase
{
    [TestMethod]
    public async Task ExecutableNode_ThatDoesNotThrow_ShouldReportPassed()
    {
        // Arrange
        var testNode = new InternalUnsafeActionTestNode
        {
            StableUid = "Microsoft.Testing.Framework.UnitTests.Adapter_ExecuteRequestAsyncTests.ExecutableNode_ThatDoesNotThrow_ShouldReportPassed()",
            DisplayName = "Microsoft.Testing.Framework.UnitTests.Adapter_ExecuteRequestAsyncTests.ExecutableNode_ThatDoesNotThrow_ShouldReportPassed()",
            Body = testExecutionContext => { },
        };

        var services = new Services();
        var adapter = new TestFramework(new(), new[] { new FactoryTestNodesBuilder(() => new[] { testNode }) }, new(),
            services.ServiceProvider.GetSystemClock(), services.ServiceProvider.GetTask(), services.ServiceProvider.GetConfiguration(), new Platform.Capabilities.TestFramework.TestFrameworkCapabilities());

        CancellationToken cancellationToken = CancellationToken.None;

        // Act
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        await adapter.ExecuteRequestAsync(new(
            new RunTestExecutionRequest(new(new("id"), new ClientInfo(string.Empty, string.Empty))),
            services.ServiceProvider.GetRequiredService<IMessageBus>(),
            new SemaphoreSlimRequestCompleteNotifier(new SemaphoreSlim(1)),
            cancellationToken));
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Assert
        IEnumerable<TestNodeUpdateMessage> nodeStateChanges = services.MessageBus.Messages.OfType<TestNodeUpdateMessage>();
        Assert.IsTrue(nodeStateChanges.Any(), $"{nameof(nodeStateChanges)} should have at least 1 item.");
        Platform.Extensions.Messages.TestNode lastNode = nodeStateChanges.Last().TestNode;
        _ = lastNode.Properties.Single<PassedTestNodeStateProperty>();
    }

    [TestMethod]
    public async Task ExecutableNode_ThatThrows_ShouldReportError()
    {
        // Arrange
        var testNode = new InternalUnsafeActionTestNode
        {
            StableUid = "Microsoft.Testing.Framework.UnitTests.Adapter_ExecuteRequestAsyncTests.ExecutableNode_ThatThrows_ShouldReportError()",
            DisplayName = "Microsoft.Testing.Framework.UnitTests.Adapter_ExecuteRequestAsyncTests.ExecutableNode_ThatThrows_ShouldReportError()",
            Body = testExecutionContext => throw new InvalidOperationException("Oh no!") { },
        };

        var services = new Services();
        var fakeClock = (FakeClock)services.ServiceProvider.GetService(typeof(FakeClock))!;

        var adapter = new TestFramework(new(), new[] { new FactoryTestNodesBuilder(() => new[] { testNode }) }, new(),
            services.ServiceProvider.GetSystemClock(), services.ServiceProvider.GetTask(), services.ServiceProvider.GetConfiguration(), new Platform.Capabilities.TestFramework.TestFrameworkCapabilities());
        CancellationToken cancellationToken = CancellationToken.None;

        // Act
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        await adapter.ExecuteRequestAsync(new(
            new RunTestExecutionRequest(new(new("id"), new ClientInfo(string.Empty, string.Empty))),
            services.ServiceProvider.GetRequiredService<IMessageBus>(),
            new SemaphoreSlimRequestCompleteNotifier(new SemaphoreSlim(1)),
            cancellationToken));
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Assert
        IEnumerable<TestNodeUpdateMessage> nodeStateChanges = services.MessageBus.Messages.OfType<TestNodeUpdateMessage>();
        Assert.IsTrue(nodeStateChanges.Any(), $"{nameof(nodeStateChanges)} should have at least 1 item.");
        Platform.Extensions.Messages.TestNode lastNode = nodeStateChanges.Last().TestNode;
        _ = lastNode.Properties.Single<ErrorTestNodeStateProperty>();
        Assert.AreEqual("Oh no!", lastNode.Properties.Single<ErrorTestNodeStateProperty>().Exception!.Message);
        Assert.IsTrue(
            lastNode.Properties.Single<ErrorTestNodeStateProperty>().Exception!.StackTrace!
            .Contains(nameof(ExecutableNode_ThatThrows_ShouldReportError)), "lastNode properties should contain the name of the test");
        TimingProperty timingProperty = lastNode.Properties.Single<TimingProperty>();
        Assert.AreEqual(fakeClock.UsedTimes[0], timingProperty.GlobalTiming.StartTime);
        Assert.IsTrue(timingProperty.GlobalTiming.StartTime <= timingProperty.GlobalTiming.EndTime, "start time is before (or the same as) stop time");
        Assert.AreEqual(fakeClock.UsedTimes[1], timingProperty.GlobalTiming.EndTime);
        Assert.IsTrue(timingProperty.GlobalTiming.Duration.TotalMilliseconds > 0, $"duration should be greater than 0");
    }

    private sealed class FakeClock : IClock
    {
        public List<DateTimeOffset> UsedTimes { get; } = new();

        public DateTimeOffset UtcNow
        {
            get
            {
                DateTimeOffset date = DateTimeOffset.UtcNow;
                UsedTimes.Add(date);
                return date;
            }
        }
    }

    private sealed class Services
    {
        public Services()
        {
            MessageBus = new MessageBus();
            ServiceProvider.AddService(MessageBus);
            ServiceProvider.AddService(new LoggerFactory());
            ServiceProvider.AddService(new FakeClock());
            ServiceProvider.AddService(new SystemTask());
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ServiceProvider.AddService(new AggregatedConfiguration(Array.Empty<IConfigurationProvider>(), new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()), new SystemFileSystem(), new(null, [], [])));
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        public MessageBus MessageBus { get; }

        public ServiceProvider ServiceProvider { get; } = new();
    }

    private sealed class MessageBus : IMessageBus
    {
        public List<IData> Messages { get; } = new();

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            Messages.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class LoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }
}
