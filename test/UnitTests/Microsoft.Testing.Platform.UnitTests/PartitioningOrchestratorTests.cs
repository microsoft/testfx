// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "These pure helpers do not launch processes or access platform-specific APIs.")]
public sealed class PartitioningOrchestratorTests
{
    [TestMethod]
    public void RemoveOwnedArguments_PreservesExecutableFiltersAndInnerOrchestratorOptions()
    {
        string[] actual = PartitioningOrchestrator.RemoveOwnedArguments(
        [
            "exec", @"C:\path with spaces\Tests.dll", "--filter", "TestCategory=Fast",
            "--partition-count", "4", "--results-directory=old results", "-progress:off", "--ansi", "on",
            "--retry-failed-tests", "2", "--test-parameter", "value=with spaces",
        ]);

        Assert.AreSequenceEqual(
            new[] { "exec", @"C:\path with spaces\Tests.dll", "--filter", "TestCategory=Fast", "--retry-failed-tests", "2", "--test-parameter", "value=with spaces" },
            actual);
    }

    [TestMethod]
    public void RemoveOwnedArguments_RemovesEverySupportedSpellingAndRepeatedOccurrence()
        => Assert.AreSequenceEqual(
            new[] { "Tests.exe", "--filter-uid", "uid" },
            PartitioningOrchestrator.RemoveOwnedArguments(
            [
                "Tests.exe", "-partition-count:4", "--partition-count=4", "-results-directory", "one",
                "--results-directory:two", "--progress=on", "-ansi:off", "--filter-uid", "uid",
            ]));

    [TestMethod]
    [DataRow(8, 8, 8)]
    [DataRow(8, 0, 0)]
    [DataRow(0, 8, 0)]
    [DataRow(0, 2, 2)]
    [DataRow(8, 9, 9)]
    [DataRow(2, 5, 5)]
    [DataRow(2, 7, 7)]
    [DataRow(7, -123, -123)]
    [DataRow(-123, 3, 3)]
    [DataRow(5, 4, 4)]
    public void AggregateExitCode_EmptySuccessFailureAndCrashRemainDistinct(int current, int next, int expected)
    {
        Assert.AreEqual(expected, PartitioningOrchestrator.AggregateExitCode(current, next));
        Assert.AreEqual(expected, PartitioningOrchestrator.AggregateExitCode(next, current));
    }

    [TestMethod]
    public void IsSupportedBy_RequiresFrameworkToDeclareSupportedPartitioningCapability()
    {
        Assert.IsFalse(PartitioningCommandLineOptionsProvider.IsSupportedBy(null));
        Assert.IsFalse(PartitioningCommandLineOptionsProvider.IsSupportedBy(new TestFrameworkCapabilities()));
        Assert.IsFalse(PartitioningCommandLineOptionsProvider.IsSupportedBy(new TestFrameworkCapabilities(new PartitioningCapability(isSupported: false))));
        Assert.IsTrue(PartitioningCommandLineOptionsProvider.IsSupportedBy(new TestFrameworkCapabilities(new PartitioningCapability(isSupported: true))));
    }

    [TestMethod]
    public void RemoveExtensionsCommandLineOptionsProviders_HidesOnlyMatchingExtensionProvider()
    {
        Mock<ICommandLineOptionsProvider> other = new();
        other.SetupGet(x => x.Uid).Returns("Other");
        Mock<ICommandLineOptionsProvider> system = new();
        system.SetupGet(x => x.Uid).Returns("System");
        PartitioningCommandLineOptionsProvider partitioning = new();
        CommandLineHandler handler = new(
            CommandLineParser.Parse([], new SystemEnvironment()),
            [partitioning, other.Object],
            [system.Object],
            Mock.Of<ITestApplicationModuleInfo>(),
            Mock.Of<IRuntimeFeature>());

        handler.RemoveExtensionsCommandLineOptionsProviders(provider => provider.Uid == partitioning.Uid);

        Assert.AreSequenceEqual(new[] { other.Object }, handler.ExtensionsCommandLineOptionsProviders.ToArray());
        Assert.AreSequenceEqual(new[] { system.Object, other.Object }, handler.CommandLineOptionsProviders.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PartitionCountOption_IsRejectedWhenFrameworkDoesNotSupportPartitioning(bool declareUnsupportedCapability)
    {
        string[] args = ["--partition-count", "2", "--no-banner"];
        TestApplicationBuilder builder = new(
            new ApplicationLoggingState(LogLevel.None, CommandLineParser.Parse(args, new SystemEnvironment())),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            Mock.Of<IUnhandledExceptionsHandler>(),
            args);
        builder.RegisterTestFramework(
            _ => declareUnsupportedCapability ? new TestFrameworkCapabilities(new PartitioningCapability(isSupported: false)) : new TestFrameworkCapabilities(),
            (_, _) => CreateEmptyFramework());

        using ITestApplication application = await builder.BuildAsync();

        Assert.AreEqual((int)ExitCode.InvalidCommandLine, await application.RunAsync());
    }

    private static ITestFramework CreateEmptyFramework()
    {
        Mock<ITestFramework> framework = new();
        framework.SetupGet(f => f.Uid).Returns("empty-framework");
        framework.SetupGet(f => f.Version).Returns("1.0");
        framework.SetupGet(f => f.DisplayName).Returns("Empty framework");
        framework.SetupGet(f => f.Description).Returns("Runs no tests.");
        framework.Setup(f => f.IsEnabledAsync()).ReturnsAsync(true);
        framework.Setup(f => f.CreateTestSessionAsync(It.IsAny<CreateTestSessionContext>())).ReturnsAsync(new CreateTestSessionResult { IsSuccess = true });
        framework.Setup(f => f.CloseTestSessionAsync(It.IsAny<CloseTestSessionContext>())).ReturnsAsync(new CloseTestSessionResult { IsSuccess = true });
        framework.Setup(f => f.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>()))
            .Callback<ExecuteRequestContext>(context => context.Complete())
            .Returns(Task.CompletedTask);
        return framework.Object;
    }

    private sealed class PartitioningCapability(bool isSupported) : ITestPartitioningCapability
    {
        public bool IsSupported { get; } = isSupported;
    }
}
