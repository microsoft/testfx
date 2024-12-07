// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TestApplicationResultTests : TestBase
{
    private readonly TestApplicationResult _testApplicationResult
        = new(new Mock<IOutputDevice>().Object, new Mock<ICommandLineOptions>().Object, new Mock<IEnvironment>().Object, new Mock<IStopPoliciesService>().Object);

    public TestApplicationResultTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public async Task GetProcessExitCodeAsync_If_All_Skipped_Returns_ZeroTestsRan()
    {
        await _testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(SkippedTestNodeStateProperty.CachedInstance),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.ZeroTests, _testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_If_No_Tests_Ran_Returns_ZeroTestsRan()
    {
        await _testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.ZeroTests, _testApplicationResult.GetProcessExitCode());
    }

    [ArgumentsProvider(nameof(FailedState))]
    public async Task GetProcessExitCodeAsync_If_Failed_Tests_Returns_AtLeastOneTestFailed(TestNodeStateProperty testNodeStateProperty)
    {
        await _testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(testNodeStateProperty),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.AtLeastOneTestFailed, _testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_If_Canceled_Returns_TestSessionAborted()
    {
        Mock<ITestApplicationCancellationTokenSource> testApplicationCancellationTokenSource = new();
        // CTS should not be created in SetupGet so that the mocked ITestApplicationCancellationTokenSource returns the same instance on every access
        // which is the case in the real production implementation.
        CancellationTokenSource cancellationTokenSource = new();
        testApplicationCancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(() =>
        {
            cancellationTokenSource.Cancel();
            return cancellationTokenSource.Token;
        });

        TestApplicationResult testApplicationResult
            = new(new Mock<IOutputDevice>().Object, new Mock<ICommandLineOptions>().Object, new Mock<IEnvironment>().Object, new StopPoliciesService(testApplicationCancellationTokenSource.Object));

        await testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.TestSessionAborted, testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_If_TestAdapter_Returns_TestAdapterTestSessionFailure()
    {
        await _testApplicationResult.SetTestAdapterTestSessionFailureAsync("Adapter error");
        await _testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.TestAdapterTestSessionFailure, _testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_If_MinimumExpectedTests_Violated_Returns_MinimumExpectedTestsPolicyViolation()
    {
        TestApplicationResult testApplicationResult
            = new(
                new Mock<IOutputDevice>().Object,
                new CommandLineOption(PlatformCommandLineProvider.MinimumExpectedTestsOptionKey, ["2"]),
                new Mock<IEnvironment>().Object, new Mock<IStopPoliciesService>().Object);

        await testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
            }), CancellationToken.None);

        await testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(InProgressTestNodeStateProperty.CachedInstance),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.MinimumExpectedTestsPolicyViolation, testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_OnDiscovery_No_Tests_Discovered_Returns_ZeroTests()
    {
        TestApplicationResult testApplicationResult
            = new(
                new Mock<IOutputDevice>().Object,
                new CommandLineOption(PlatformCommandLineProvider.DiscoverTestsOptionKey, []),
                new Mock<IEnvironment>().Object, new Mock<IStopPoliciesService>().Object);

        await testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.ZeroTests, testApplicationResult.GetProcessExitCode());
    }

    public async Task GetProcessExitCodeAsync_OnDiscovery_Some_Tests_Discovered_Returns_Success()
    {
        TestApplicationResult testApplicationResult
            = new(
                new Mock<IOutputDevice>().Object,
                new CommandLineOption(PlatformCommandLineProvider.DiscoverTestsOptionKey, []),
                new Mock<IEnvironment>().Object, new Mock<IStopPoliciesService>().Object);

        await testApplicationResult.ConsumeAsync(new DummyProducer(), new TestNodeUpdateMessage(
            default,
            new Extensions.Messages.TestNode
            {
                Uid = new Extensions.Messages.TestNodeUid("id"),
                DisplayName = "DisplayName",
                Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
            }), CancellationToken.None);

        Assert.AreEqual(ExitCodes.Success, testApplicationResult.GetProcessExitCode());
    }

    [Arguments("8", ExitCodes.Success)]
    [Arguments("8;2", ExitCodes.Success)]
    [Arguments("8;", ExitCodes.Success)]
    [Arguments("8;2;", ExitCodes.Success)]
    [Arguments("5", ExitCodes.ZeroTests)]
    [Arguments("5;7", ExitCodes.ZeroTests)]
    [Arguments("5;", ExitCodes.ZeroTests)]
    [Arguments("5;7;", ExitCodes.ZeroTests)]
    [Arguments(";", ExitCodes.ZeroTests)]
    [Arguments(null, ExitCodes.ZeroTests)]
    [Arguments("", ExitCodes.ZeroTests)]
    public void GetProcessExitCodeAsync_IgnoreExitCodes(string argument, int expectedExitCode)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE)).Returns(argument);

        foreach (TestApplicationResult testApplicationResult in new TestApplicationResult[]
        {
            new(
                new Mock<IOutputDevice>().Object,
                new CommandLineOption(PlatformCommandLineProvider.IgnoreExitCodeOptionKey, argument is null ? [] : [argument]),
                new Mock<IEnvironment>().Object, new Mock<IStopPoliciesService>().Object),
            new(
                new Mock<IOutputDevice>().Object,
                new Mock<ICommandLineOptions>().Object,
                environment.Object,
                new Mock<IStopPoliciesService>().Object),
        })
        {
            Assert.AreEqual(expectedExitCode, testApplicationResult.GetProcessExitCode());
        }
    }

    internal static IEnumerable<TestNodeStateProperty> FailedState()
    {
        yield return new FailedTestNodeStateProperty();
        yield return new ErrorTestNodeStateProperty();
        yield return new CancelledTestNodeStateProperty();
        yield return new TimeoutTestNodeStateProperty();
    }

    private sealed class CommandLineOption : ICommandLineOptions
    {
        private readonly string _optionName;
        private readonly string[] _arguments;

        public CommandLineOption(string optionName, string[] arguments)
        {
            _optionName = optionName;
            _arguments = arguments;
        }

        public bool IsOptionSet(string optionName) => _optionName == optionName;

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public bool TryGetOptionArgumentList(string optionName, out string[]? arguments)
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        {
            arguments = _arguments;
            return IsOptionSet(optionName);
        }
    }

    private sealed class DummyProducer : IDataProducer
    {
        public Type[] DataTypesProduced => throw new NotImplementedException();

        public string Uid => nameof(DummyProducer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
    }
}
