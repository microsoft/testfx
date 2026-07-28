// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Bridges a VSTest <see cref="ITestCaseDiscoverySink"/> to the platform-agnostic
/// <see cref="IUnitTestElementSink"/>.
/// </summary>
/// <remarks>
/// This is the single translation point between the neutral <see cref="UnitTestElement"/> model and the
/// VSTest discovery object model (<c>TestCase</c>, <c>ITestCaseDiscoverySink</c>). It materializes a
/// VSTest <c>TestCase</c> for each discovered element via <c>UnitTestElementExtensions.ToTestCase</c>. It is
/// expected to move entirely into the adapter layer once discovery no longer flows VSTest discovery sinks
/// through the platform services (see the tracking issue linked in the pull request that removes the VSTest
/// object model from platform services).
/// </remarks>
internal static class UnitTestElementSinkExtensions
{
    /// <summary>
    /// Wraps a VSTest <see cref="ITestCaseDiscoverySink"/> as an <see cref="IUnitTestElementSink"/>.
    /// </summary>
    /// <param name="discoverySink">The host discovery sink to wrap.</param>
    /// <returns>A platform-agnostic sink that forwards to <paramref name="discoverySink"/>.</returns>
    internal static IUnitTestElementSink ToUnitTestElementSink(this ITestCaseDiscoverySink discoverySink)
        => new HostDiscoverySink(discoverySink ?? throw new ArgumentNullException(nameof(discoverySink)));

    private sealed class HostDiscoverySink : IUnitTestElementSink
    {
        private readonly ITestCaseDiscoverySink _discoverySink;

        public HostDiscoverySink(ITestCaseDiscoverySink discoverySink)
            => _discoverySink = discoverySink;

        public Task SendTestElementAsync(UnitTestElement testElement)
        {
            _discoverySink.SendTestCase(testElement.GetOrCreateHostTestCase());
            return Task.CompletedTask;
        }
    }
}
