// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestApplicationResultCountingTests
{
    [TestMethod]
    public async Task ConsumeAsync_PassedTests_AreCountedAsRan()
    {
        using TestApplicationResult result = Create();

        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(2, result.GetStatistics().TotalRanTests);
        Assert.AreEqual(0, result.GetStatistics().TotalFailedTests);
    }

    [TestMethod]
    public async Task ConsumeAsync_FailedErrorAndTimeoutTests_AreCountedAsFailedAndRan()
    {
        using TestApplicationResult result = Create();

        await ConsumeAsync(result, new FailedTestNodeStateProperty("failed"));
        await ConsumeAsync(result, new ErrorTestNodeStateProperty("error"));
        await ConsumeAsync(result, new TimeoutTestNodeStateProperty("timeout"));

        Assert.AreEqual(3, result.GetStatistics().TotalRanTests);
        Assert.AreEqual(3, result.GetStatistics().TotalFailedTests);
        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, result.GetProcessExitCode());
    }

    [TestMethod]
    public async Task ConsumeAsync_SkippedTests_AreNotCountedAsRan()
    {
        using TestApplicationResult result = Create();

        await ConsumeAsync(result, new SkippedTestNodeStateProperty("skipped"));

        Assert.AreEqual(0, result.GetStatistics().TotalRanTests);
        Assert.AreEqual(0, result.GetStatistics().TotalFailedTests);
    }

    [TestMethod]
    public async Task ConsumeAsync_DiscoveredTest_IsCountedOnlyInDiscoveryMode()
    {
        using TestApplicationResult discovery = Create(new Dictionary<string, string[]> { [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [] });
        using TestApplicationResult run = Create();

        await ConsumeAsync(discovery, DiscoveredTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(run, DiscoveredTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(1, discovery.GetStatistics().TotalRanTests);
        Assert.AreEqual(0, run.GetStatistics().TotalRanTests);
    }

    [TestMethod]
    [DataRow(1, (int)ExitCode.Success)]
    [DataRow(2, (int)ExitCode.Success)]
    [DataRow(3, (int)ExitCode.MinimumExpectedTestsPolicyViolation)]
    public async Task GetProcessExitCode_MinimumExpectedTests_IsInclusiveBoundary(int minimum, int expected)
    {
        using TestApplicationResult result = Create(new Dictionary<string, string[]> { [PlatformCommandLineProvider.MinimumExpectedTestsOptionKey] = [minimum.ToString(CultureInfo.InvariantCulture)] });

        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);
        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(expected, result.GetProcessExitCode());
    }

    [TestMethod]
    public async Task GetProcessExitCode_MaxFailedTestsTriggered_ReturnsStoppedForMaxFailedTests()
    {
        Mock<IStopPoliciesService> policies = new();
        policies.SetupGet(x => x.IsMaxFailedTestsTriggered).Returns(true);
        using TestApplicationResult result = Create(policies: policies.Object);

        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual((int)ExitCode.TestExecutionStoppedForMaxFailedTests, result.GetProcessExitCode());
    }

    [TestMethod]
    public async Task GetProcessExitCode_MaxFailedTestsNotTriggered_ReturnsSuccess()
    {
        using TestApplicationResult result = Create();

        await ConsumeAsync(result, PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual((int)ExitCode.Success, result.GetProcessExitCode());
    }

    private static TestApplicationResult Create(Dictionary<string, string[]>? options = null, IStopPoliciesService? policies = null)
        => new(
            Mock.Of<IOutputDevice>(),
            new TestCommandLineOptions(options ?? []),
            Mock.Of<IEnvironment>(),
            policies ?? Mock.Of<IStopPoliciesService>(),
            otelService: null);

    private static Task ConsumeAsync(TestApplicationResult result, IProperty state)
        => result.ConsumeAsync(
            new Producer(),
            new TestNodeUpdateMessage(default, new TestNode { Uid = "uid", DisplayName = "test", Properties = new PropertyBag(state) }),
            CancellationToken.None);

    private sealed class Producer : IDataProducer
    {
        public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

        public string Uid => nameof(Producer);

        public string Version => "1.0.0";

        public string DisplayName => nameof(Producer);

        public string Description => nameof(Producer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
