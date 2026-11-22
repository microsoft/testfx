// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Discovery;

public sealed class UnitTestDiscovererConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public UnitTestDiscovererConfigureAwaitMutationTests()
        => PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task DiscoverTestsAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var sink = new PendingSink();
        var discoverer = new StubDiscoverer([new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))]);

        // The discovery of the source, the sending of the discovered tests and the reporting of each test all wait
        // on the (pending) sink, so each of their awaits suspends while the probe's context is current.
        Task discovery = probe.Run(() => discoverer.DiscoverTestsAsync(["A.dll"], new Mock<IAdapterMessageLogger>().Object, sink, settingsXml: null, filterProvider: null, isMTP: false));
        discovery.IsCompleted.Should().BeFalse();
        sink.Gate.SetResult(true);
        await WaitFor(discovery);

        sink.SentTests.Should().ContainSingle();
        probe.PostCount.Should().Be(0, "discovery must not capture the caller's synchronization context");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private sealed class StubDiscoverer : UnitTestDiscoverer
    {
        private readonly ICollection<UnitTestElement> _elements;

        public StubDiscoverer(ICollection<UnitTestElement> elements)
            : base(new Mock<ITestSourceHandler>().Object)
            => _elements = elements;

        internal override ICollection<UnitTestElement>? GetTestElements(string source, string? settingsXml, bool isMTP, out List<string> warnings, out int discoveredTestCount)
        {
            warnings = [];
            discoveredTestCount = _elements.Count;
            return _elements;
        }
    }

    private sealed class PendingSink : IUnitTestElementSink
    {
        public TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<UnitTestElement> SentTests { get; } = [];

        public Task SendTestElementAsync(UnitTestElement testElement)
        {
            SentTests.Add(testElement);
            return Gate.Task;
        }
    }
}
