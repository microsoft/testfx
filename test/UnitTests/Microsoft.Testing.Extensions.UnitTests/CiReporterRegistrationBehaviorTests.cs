// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using Moq;

using GitHub = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiReporterRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow(false, false, DisplayName = "AzureDevOps_DirectRegistration")]
    [DataRow(true, true, DisplayName = "AzureDevOps_HookAndArtifactRegistration")]
    public async Task AzureDevOps_RegisteredFactoriesShareRolesAndProduceOwnedOutput(bool hook, bool artifacts)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [path];
        fixture.Options[PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = ["90"];
        var registration = new Registration(artifacts);
        if (hook)
        {
            AzureDevOpsReport.TestingPlatformBuilderHook.AddExtensions(registration.Builder.Object, ["unused hook argument"]);
        }
        else
        {
            registration.Builder.Object.AddAzureDevOpsProvider();
        }

        IDataConsumer[] consumers = registration.Consumers(fixture.Services);
        ITestSessionLifetimeHandler[] sessions = registration.Sessions(fixture.Services);
        Assert.AreSequenceEqual(
            ["AzureDevOpsReporter", "AzureDevOpsArtifactUploader", "AzureDevOpsSummaryReporter", "AzureDevOpsSlowTestReporter", "AzureDevOpsTestResultsPublisher", "AzureDevOpsLogGroupReporter"],
            consumers.Select(consumer => consumer.Uid));
        Assert.AreSequenceEqual(
            ["AzureDevOpsHistoryService", "AzureDevOpsArtifactUploader", "AzureDevOpsSummaryReporter", "AzureDevOpsSlowTestReporter", "AzureDevOpsTestResultsPublisher", "AzureDevOpsLogGroupReporter"],
            sessions.Select(session => session.Uid));
        foreach (IDataConsumer consumer in consumers.Skip(1))
        {
            Assert.AreSame<IExtension>(consumer, sessions.Single(session => session.Uid == consumer.Uid));
        }

        // The plain history factory is shared too, not recreated for a second factory invocation.
        Assert.AreSame(sessions[0], registration.Sessions(fixture.Services)[0]);
        await consumers[0].ConsumeAsync(null!, CiReporterFixture.Update("Tests.Registered", new FailedTestNodeStateProperty("registered failure")), CancellationToken.None);
        AzureDevOpsSummaryReporter summary = Assert.IsInstanceOfType<AzureDevOpsSummaryReporter>(consumers[2]);
        await summary.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await summary.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Passed", new PassedTestNodeStateProperty()), CancellationToken.None);
        await summary.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.Contains("**1 test**", fixture.ReadText(path));
        Assert.Contains("**1 passed**", fixture.ReadText(path));
        Assert.HasCount(2, fixture.Displays.OfType<AzureDevOpsCommandOutputDeviceData>());
        Assert.AreSame<IExtension>(consumers[0], fixture.DisplayAttempts[0].Owner);
        Assert.AreSame(summary, fixture.DisplayAttempts[1].Owner);
        Func<IServiceProvider, Platform.Extensions.TestHostOrchestrator.ITestHostOrchestratorApplicationLifetime> orchestratorFactory = Assert.IsInstanceOfType<Func<IServiceProvider, Microsoft.Testing.Platform.Extensions.TestHostOrchestrator.ITestHostOrchestratorApplicationLifetime>>(
            Assert.ContainsSingle(registration.Orchestrator.Invocations).Arguments[0]);
        AzureDevOpsTestRunOrchestratorLifetime orchestrator = Assert.IsInstanceOfType<AzureDevOpsTestRunOrchestratorLifetime>(orchestratorFactory(fixture.Services));
        Assert.IsFalse(await orchestrator.IsEnabledAsync());
        Assert.IsNull(orchestrator.RunId);
        ICommandLineOptionsProvider provider = registration.CommandLineProvider();
        Assert.Contains("report-azdo", provider.GetCommandLineOptions().Select(option => option.Name));
        Assert.Contains("publish-azdo-test-results", provider.GetCommandLineOptions().Select(option => option.Name));
        Assert.HasCount(artifacts ? 1 : 0, registration.Artifacts.Invocations);
        if (artifacts)
        {
            IArtifactPostProcessor processor = registration.Processor(fixture.Services);
            Assert.IsInstanceOfType<AzureDevOpsSummaryArtifactPostProcessor>(processor);
            Assert.IsTrue(await processor.IsEnabledAsync());
            Assert.AreSequenceEqual(["microsoft.testing.azure-devops-summary-fragment"], processor.SupportedKinds);
        }

        Assert.ContainsSingle((await registration.Controllers.BuildAsync(fixture.Services)).RunCompletionHandlers);
        foreach (IDisposable consumer in consumers.OfType<IDisposable>())
        {
            consumer.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false, false, false, DisplayName = "GitHubActions_DirectRegistration")]
    [DataRow(true, true, false, DisplayName = "GitHubActions_HookAndArtifactRegistration")]
    [DataRow(false, true, true, DisplayName = "GitHubActions_RetryDeferredWithProcessor")]
    [DataRow(true, false, true, DisplayName = "GitHubActions_HookRetryDeferred")]
    public async Task GitHubActions_RegisteredFactoriesShareRolesAndHonorRetryDeferral(bool hook, bool artifacts, bool retry)
    {
        using var fixture = new CiReporterFixture();
        fixture.CreateRealDirectory();
        fixture.ResultsDirectory = fixture.Root;
        fixture.Variables["GITHUB_ACTIONS"] = "true";
        fixture.Variables["RUNNER_OS"] = "Linux";
        string summaryPath = Path.Combine(fixture.Root, "step.md");
        fixture.Variables["GITHUB_STEP_SUMMARY"] = summaryPath;
        fixture.Options[GitHub.GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];
        fixture.Options[PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = ["90"];
        if (retry)
        {
            fixture.Options["internal-retry-pipename"] = ["injected-no-connection"];
        }

        var registration = new Registration(artifacts);
        if (hook)
        {
            GitHub.TestingPlatformBuilderHook.AddExtensions(registration.Builder.Object, ["unused hook argument"]);
        }
        else
        {
            ghactions::Microsoft.Testing.Extensions.GitHubActionsExtensions.AddGitHubActionsProvider(registration.Builder.Object);
        }

        IDataConsumer[] consumers = registration.Consumers(fixture.Services);
        ITestSessionLifetimeHandler[] sessions = registration.Sessions(fixture.Services);
        Assert.AreSequenceEqual(
            ["GitHubActionsAnnotationReporter", "GitHubActionsSummaryReporter", "GitHubActionsSlowTestReporter", "GitHubActionsReporter"],
            consumers.Select(consumer => consumer.Uid));
        Assert.AreSequenceEqual(
            ["GitHubActionsHistoryService", "GitHubActionsAnnotationReporter", "GitHubActionsSummaryReporter", "GitHubActionsSlowTestReporter", "GitHubActionsReporter"],
            sessions.Select(session => session.Uid));
        Assert.AreSame(sessions[0], registration.Sessions(fixture.Services)[0]);
        foreach (IDataConsumer consumer in consumers)
        {
            Assert.AreSame<IExtension>(consumer, sessions.Single(session => session.Uid == consumer.Uid));
        }

        Assert.IsFalse(await sessions[0].IsEnabledAsync());
        await consumers[0].ConsumeAsync(null!, CiReporterFixture.Update("Tests.Registered", new FailedTestNodeStateProperty("registered failure")), CancellationToken.None);
        GitHub.GitHubActionsSummaryReporter summary = Assert.IsInstanceOfType<GitHub.GitHubActionsSummaryReporter>(consumers[1]);
        await summary.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await summary.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Passed", new PassedTestNodeStateProperty()), CancellationToken.None);
        await summary.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        Assert.AreSame<IExtension>(consumers[0], Assert.ContainsSingle(fixture.DisplayAttempts).Owner);
        Assert.Contains("registered failure", Assert.IsInstanceOfType<GitHubActionsCommandOutputDeviceData>(fixture.DisplayAttempts[0].Data).Text);
        Assert.Contains("report-gh", registration.CommandLineProvider().GetCommandLineOptions().Select(option => option.Name));
        if (retry)
        {
            SessionFileArtifact fragment = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Publications));
            Assert.AreEqual("microsoft.testing.github-actions-summary-fragment", fragment.Kind);
            using var document = JsonDocument.Parse(File.ReadAllText(fragment.FileInfo.FullName));
            Assert.AreEqual(1, document.RootElement.GetProperty("module").GetProperty("passedTests").GetInt32());
            Assert.IsFalse(fixture.Files.ContainsKey(summaryPath));
            if (artifacts)
            {
                IArtifactPostProcessor processor = registration.Processor(fixture.Services);
                var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts,
                    new ArtifactPostProcessingRunSummary(1, 1, 0, 0, TimeSpan.Zero, 0, 1));
                ProcessedArtifact? processed = await processor.ProcessAsync(
                    [new InputArtifact(fragment.FileInfo.FullName, fragment.Kind, null, null, null, null)], fixture.Root, context, CancellationToken.None);
                Assert.IsNotNull(processed);
                Assert.AreEqual("microsoft.testing.github-actions-summary-fragment", processed.Kind);
                Assert.Contains("| 1 | 1 | 0 | 0 |", fixture.ReadText(summaryPath));
            }
        }
        else
        {
            Assert.IsEmpty(fixture.Publications);
            Assert.Contains("| 1 | 1 | 0 | 0 |", fixture.ReadText(summaryPath));
        }

        Assert.HasCount(artifacts ? 1 : 0, registration.Artifacts.Invocations);
        if (artifacts && !retry)
        {
            IArtifactPostProcessor processor = registration.Processor(fixture.Services);
            Assert.IsInstanceOfType<GitHub.GitHubActionsSummaryArtifactPostProcessor>(processor);
            Assert.IsTrue(await processor.IsEnabledAsync());
        }

        Assert.ContainsSingle((await registration.Controllers.BuildAsync(fixture.Services)).RunCompletionHandlers);
    }

    private sealed class Registration
    {
        public Registration(bool artifacts)
        {
            if (artifacts)
            {
                Builder.As<IArtifactPostProcessingApplicationBuilder>().SetupGet(value => value.ArtifactPostProcessing).Returns(Artifacts.Object);
            }

            Builder.SetupGet(value => value.TestHost).Returns(Host.Object);
            Builder.SetupGet(value => value.TestHostControllers).Returns(Controllers);
            Builder.SetupGet(value => value.TestHostOrchestrator).Returns(Orchestrator.Object);
            Builder.SetupGet(value => value.CommandLine).Returns(CommandLine.Object);
            var trigger = new Mock<ITestHostEnvironmentVariableProvider>();
            trigger.SetupGet(value => value.Uid).Returns("owned-registration-trigger");
            trigger.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
            Controllers.AddEnvironmentVariableProvider(_ => trigger.Object);
        }

        public Mock<ITestApplicationBuilder> Builder { get; } = new();

        public Mock<ITestHostManager> Host { get; } = new();

        public TestHostControllersManager Controllers { get; } = new();

        public Mock<ITestHostOrchestratorManager> Orchestrator { get; } = new();

        public Mock<ICommandLineManager> CommandLine { get; } = new();

        public Mock<IArtifactPostProcessingManager> Artifacts { get; } = new();

        public IDataConsumer[] Consumers(IServiceProvider services)
            => [.. Host.Invocations.Where(invocation => invocation.Method.Name == nameof(ITestHostManager.AddDataConsumer)).Select(invocation => invocation.Arguments[0] switch
            {
                ICompositeExtensionFactory factory => (IDataConsumer)factory.GetInstance(services),
                Func<IServiceProvider, IDataConsumer> factory => factory(services),
                _ => throw new AssertFailedException("Unexpected consumer registration."),
            })];

        public ITestSessionLifetimeHandler[] Sessions(IServiceProvider services)
            => [.. Host.Invocations.Where(invocation => invocation.Method.Name == nameof(ITestHostManager.AddTestSessionLifetimeHandler)).Select(invocation => invocation.Arguments[0] switch
            {
                ICompositeExtensionFactory factory => (ITestSessionLifetimeHandler)factory.GetInstance(services),
                Func<IServiceProvider, ITestSessionLifetimeHandler> factory => factory(services),
                _ => throw new AssertFailedException("Unexpected session registration."),
            })];

        public ICommandLineOptionsProvider CommandLineProvider()
            => Assert.IsInstanceOfType<Func<ICommandLineOptionsProvider>>(Assert.ContainsSingle(CommandLine.Invocations).Arguments[0])();

        public IArtifactPostProcessor Processor(IServiceProvider services)
            => Assert.IsInstanceOfType<Func<IServiceProvider, IArtifactPostProcessor>>(Assert.ContainsSingle(Artifacts.Invocations).Arguments[0])(services);
    }
}
