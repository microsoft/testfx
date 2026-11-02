// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Extensions;

[TestClass]
public sealed class AbortForMaxFailedTestsExtensionTests
{
    [TestMethod]
    public async Task IsEnabledAsync_ValidThresholdAndCapability_IsEnabled()
    {
        using var fixture = new Fixture();

        Assert.IsTrue(await fixture.Create("1").IsEnabledAsync());
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow("0", true)]
    [DataRow("-1", true)]
    [DataRow("invalid", true)]
    [DataRow("2147483648", true)]
    [DataRow("1", false)]
    public async Task ConsumeAsync_DisabledConfiguration_DoesNotStop(string? threshold, bool hasCapability)
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create(threshold, hasCapability);

        Assert.IsFalse(await extension.IsEnabledAsync());
        await fixture.ConsumeAsync(extension, new PropertyBag(new FailedTestNodeStateProperty()));

        Assert.IsEmpty(fixture.Calls);
        fixture.Capability.Verify(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Policies.Verify(x => x.ExecuteMaxFailedTestsCallbacksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow("failed")]
    [DataRow("error")]
    [DataRow("timeout")]
    [DataRow("cancelled")]
    public async Task ConsumeAsync_ReachingThreshold_StopsBeforeNotifyingPolicy(string failure)
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create("2");

        await fixture.ConsumeAsync(extension, new PropertyBag(CreateState(failure)), "first");
        Assert.IsEmpty(fixture.Calls, "The failure budget must not stop execution before its exact boundary.");

        await fixture.ConsumeAsync(extension, new PropertyBag(CreateState(failure)), "second");

        Assert.AreSequenceEqual(new[] { "stop", "policy:2" }, fixture.Calls);
        fixture.Capability.Verify(x => x.StopTestExecutionAsync(fixture.ApplicationToken), Times.Once);
        fixture.Policies.Verify(x => x.ExecuteMaxFailedTestsCallbacksAsync(2, fixture.ApplicationToken), Times.Once);
    }

    [TestMethod]
    public async Task ConsumeAsync_AfterPolicyTriggered_DoesNotStopAgain()
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create("1");

        await fixture.ConsumeAsync(extension, new PropertyBag(new FailedTestNodeStateProperty()), "first");
        await fixture.ConsumeAsync(extension, new PropertyBag(new ErrorTestNodeStateProperty()), "second");

        Assert.AreSequenceEqual(new[] { "stop", "policy:1" }, fixture.Calls);
    }

    [TestMethod]
    [DataRow("passed")]
    [DataRow("skipped")]
    [DataRow("discovered")]
    [DataRow("in-progress")]
    public async Task ConsumeAsync_NonFailureState_DoesNotSpendFailureBudget(string state)
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create("1");

        await fixture.ConsumeAsync(extension, new PropertyBag(CreateState(state)));
        Assert.IsEmpty(fixture.Calls);

        await fixture.ConsumeAsync(extension, new PropertyBag(new FailedTestNodeStateProperty()));
        Assert.AreSequenceEqual(new[] { "stop", "policy:1" }, fixture.Calls);
    }

    [TestMethod]
    public async Task ConsumeAsync_MissingState_DoesNotSpendFailureBudget()
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create("1");

        await fixture.ConsumeAsync(extension, new PropertyBag());
        Assert.IsEmpty(fixture.Calls);

        await fixture.ConsumeAsync(extension, new PropertyBag(new FailedTestNodeStateProperty()));
        Assert.AreSequenceEqual(new[] { "stop", "policy:1" }, fixture.Calls);
    }

    [TestMethod]
    public async Task ConsumeAsync_SupersededRetry_DoesNotSpendFailureBudget()
    {
        using var fixture = new Fixture();
        AbortForMaxFailedTestsExtension extension = fixture.Create("1");

        await fixture.ConsumeAsync(
            extension,
            new PropertyBag(new FailedTestNodeStateProperty(), new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true)));
        Assert.IsEmpty(fixture.Calls);

        await fixture.ConsumeAsync(
            extension,
            new PropertyBag(new FailedTestNodeStateProperty(), new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false)));
        Assert.AreSequenceEqual(new[] { "stop", "policy:1" }, fixture.Calls);
    }

    [TestMethod]
    public async Task ConsumeAsync_StopFailure_PropagatesWithoutCommittingPolicy()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("The framework rejected the stop request.");
        fixture.Capability
            .Setup(x => x.StopTestExecutionAsync(fixture.ApplicationToken))
            .Returns(Task.FromException(failure));
        AbortForMaxFailedTestsExtension extension = fixture.Create("1");

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => fixture.ConsumeAsync(extension, new PropertyBag(new FailedTestNodeStateProperty())));

        Assert.AreSame(failure, actual);
        fixture.Policies.Verify(x => x.ExecuteMaxFailedTestsCallbacksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TestNodeStateProperty CreateState(string state)
        => state switch
        {
            "failed" => new FailedTestNodeStateProperty(),
            "error" => new ErrorTestNodeStateProperty(),
            "timeout" => new TimeoutTestNodeStateProperty(),
#pragma warning disable CS0618, MTP0001 // The obsolete cancelled state still counts towards the failure budget.
            "cancelled" => new CancelledTestNodeStateProperty(),
#pragma warning restore CS0618, MTP0001
            "passed" => PassedTestNodeStateProperty.CachedInstance,
            "skipped" => SkippedTestNodeStateProperty.CachedInstance,
            "discovered" => DiscoveredTestNodeStateProperty.CachedInstance,
            "in-progress" => InProgressTestNodeStateProperty.CachedInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    private sealed class Fixture : IDisposable
    {
        private readonly CancellationTokenSource _applicationCancellation = new();
        private bool _triggered;

        public Fixture()
        {
            Capability
                .Setup(x => x.StopTestExecutionAsync(ApplicationToken))
                .Callback(() => Calls.Add("stop"))
                .Returns(Task.CompletedTask);
            Policies.SetupGet(x => x.IsMaxFailedTestsTriggered).Returns(() => _triggered);
            Policies
                .Setup(x => x.ExecuteMaxFailedTestsCallbacksAsync(It.IsAny<int>(), ApplicationToken))
                .Callback<int, CancellationToken>((threshold, _) =>
                {
                    _triggered = true;
                    Calls.Add($"policy:{threshold}");
                })
                .Returns(Task.CompletedTask);
        }

        public Mock<IGracefulStopTestExecutionCapability> Capability { get; } = new(MockBehavior.Strict);

        public Mock<IStopPoliciesService> Policies { get; } = new(MockBehavior.Strict);

        public List<string> Calls { get; } = [];

        public CancellationToken ApplicationToken => _applicationCancellation.Token;

        public AbortForMaxFailedTestsExtension Create(string? threshold, bool hasCapability = true)
        {
            Dictionary<string, string[]> options = [];
            if (threshold is not null)
            {
                options[MaxFailedTestsCommandLineOptionsProvider.MaxFailedTestsOptionKey] = [threshold];
            }

            var cancellation = new Mock<ITestApplicationCancellationTokenSource>(MockBehavior.Strict);
            cancellation.SetupGet(x => x.CancellationToken).Returns(ApplicationToken);
            return new(new TestCommandLineOptions(options), hasCapability ? Capability.Object : null, Policies.Object, cancellation.Object);
        }

        public Task ConsumeAsync(AbortForMaxFailedTestsExtension extension, PropertyBag properties, string uid = "test")
            => extension.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                new TestNodeUpdateMessage(default, new TestNode { Uid = uid, DisplayName = uid, Properties = properties }),
                CancellationToken.None);

        public void Dispose() => _applicationCancellation.Dispose();
    }
}
