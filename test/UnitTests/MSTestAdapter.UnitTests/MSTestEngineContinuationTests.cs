// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WIN_UI && !WINDOWS_UWP
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestEngineContinuationTests : TestContainer
{
    public async Task SelectedElementsAreMaterializedOnlyAfterSettingsAndUseResolvedRecorder()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        var services = new TestablePlatformServiceProvider();
        var sourceHandler = new Mock<ITestSourceHandler>();
        sourceHandler.SetupGet(value => value.ValidSourceExtensions).Returns([".dll"]);
        var recorder = new Mock<ITestResultRecorder>(MockBehavior.Strict);
        var factory = new UnusedFactory();
        List<string> events = [];
        services.MockTestDeployment.Setup(value => value.Cleanup()).Callback(() => events.Add("cleanup"));
        var engine = new MSTestEngine(CancellationToken.None) { TestClassInstanceFactory = factory };

        try
        {
            PlatformServiceProvider.Instance = services;
            await engine.RunFromTestElementsAsync(
                () =>
                {
                    TestClassInstanceFactoryProvider.Current.Should().BeSameAs(factory);
                    MSTestSettings.CurrentSettings.MapInconclusiveToFailed.Should().BeTrue();
                    events.Add("elements");
                    return Array.Empty<UnitTestElement>();
                },
                ["source.dll"], "<RunSettings><MSTest><MapInconclusiveToFailed>true</MapInconclusiveToFailed></MSTest></RunSettings>", "results",
                Mock.Of<IAdapterMessageLogger>(), settings =>
                {
                    settings.Should().BeSameAs(MSTestSettings.CurrentSettings);
                    events.Add("recorder");
                    return recorder.Object;
                }, null, null, sourceHandler.Object);

            events.Should().Equal("elements", "recorder", "cleanup");
            TestClassInstanceFactoryProvider.Current.Should().BeNull();
            recorder.VerifyNoOtherCalls();
        }
        finally
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = previous;
        }
    }

    public async Task CancellationIsObservedWithoutLeakingTheActivationFactory()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        var services = new TestablePlatformServiceProvider();
        var sourceHandler = new Mock<ITestSourceHandler>();
        sourceHandler.SetupGet(value => value.ValidSourceExtensions).Returns([".dll"]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new MSTestEngine(cancellation.Token);
        var factory = new UnusedFactory();
        engine.TestClassInstanceFactory = factory;
        try
        {
            PlatformServiceProvider.Instance = services;
            Func<Task> run = () => engine.RunFromTestElementsAsync(
                () => [new UnitTestElement(new TestMethod("Method", "Type", "source.dll", null))],
                ["source.dll"], null, null, Mock.Of<IAdapterMessageLogger>(), _ => Mock.Of<ITestResultRecorder>(), null, null, sourceHandler.Object);

            (await run.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cancellation.Token);
            TestClassInstanceFactoryProvider.Current.Should().BeNull();
            services.MockTestSourceHost.Invocations.Should().BeEmpty();
        }
        finally
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = previous;
        }
    }

    private sealed class UnusedFactory : ITestClassInstanceFactory
    {
        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
            => throw new InvalidOperationException("An empty or canceled run must not activate a test class.");
    }

    public async Task CanceledApartmentWaitReportsHostSpecificDiagnosticAndCompletesOwnedWork()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        var services = new TestablePlatformServiceProvider();
        var sourceHandler = new Mock<ITestSourceHandler>();
        sourceHandler.SetupGet(value => value.ValidSourceExtensions).Returns([".dll"]);
        using var cancellation = new CancellationTokenSource();
        var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread? entryThread = null;
        services.MockTestDeployment.Setup(value => value.Cleanup()).Callback(() =>
        {
            entryThread = Thread.CurrentThread;
            completed.TrySetResult(null);
        });
        List<(MessageLevel Level, string Text)> messages = [];
        var logger = new Mock<IAdapterMessageLogger>();
        logger.Setup(value => value.SendMessage(It.IsAny<MessageLevel>(), It.IsAny<string>()))
            .Callback<MessageLevel, string>((level, text) => messages.Add((level, text)));
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        ApartmentState requested = windows && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? ApartmentState.MTA : ApartmentState.STA;
        var engine = new MSTestEngine(cancellation.Token);
        try
        {
            PlatformServiceProvider.Instance = services;
            await engine.RunFromTestElementsAsync(
                () => Array.Empty<UnitTestElement>(), ["source.dll"],
                $"<RunSettings><RunConfiguration><ExecutionThreadApartmentState>{requested}</ExecutionThreadApartmentState></RunConfiguration></RunSettings>",
                null, logger.Object, _ =>
                {
                    cancellation.Cancel();
                    return Mock.Of<ITestResultRecorder>();
                }, null, null, sourceHandler.Object);
            await completed.Task;
            if (entryThread != Thread.CurrentThread)
            {
                entryThread!.Join();
            }

            if (windows)
            {
                messages.Where(message => message.Level == MessageLevel.Error).Should().ContainSingle()
                    .Which.Text.Should().Contain(typeof(TaskCanceledException).FullName);
            }
            else
            {
                messages.Where(message => message.Level == MessageLevel.Error).Should().BeEmpty();
                messages.Where(message => message.Level == MessageLevel.Warning).Select(message => message.Text).Should().Equal(
                    Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource.STAIsOnlySupportedOnWindowsWarning);
            }
        }
        finally
        {
            if (entryThread is { IsAlive: true } && entryThread != Thread.CurrentThread)
            {
                entryThread.Join();
            }

            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = previous;
        }
    }
}
#endif
