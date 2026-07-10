// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the discovery logic for this adapter.
/// </summary>
internal sealed class MSTestDiscoverer
{
    private readonly ITestSourceHandler _testSourceHandler;
#if !WINDOWS_UWP && !WIN_UI
    private readonly Func<string, IDictionary<string, object>, Task>? _telemetrySender;
#endif

    internal MSTestDiscoverer(ITestSourceHandler testSourceHandler, Func<string, IDictionary<string, object>, Task>? telemetrySender = null)
    {
        _testSourceHandler = testSourceHandler;
#if !WINDOWS_UWP && !WIN_UI
        _telemetrySender = telemetrySender;
#else
        _ = telemetrySender;
#endif
    }

    internal async Task DiscoverTestsAsync(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink, IConfiguration? configuration, bool isMTP)
    {
        if (discoverySink is null)
        {
            throw new ArgumentNullException(nameof(discoverySink));
        }

        await DiscoverTestsCoreAsync(sources, discoveryContext, logger, elementSink: null, discoverySink, configuration, isMTP).ConfigureAwait(false);
    }

    /// <summary>
    /// Discovers tests, reporting each discovered element directly to a platform-agnostic
    /// <see cref="IUnitTestElementSink"/> (used by the native Microsoft.Testing.Platform integration, which
    /// publishes test nodes itself and does not need a VSTest <see cref="ITestCaseDiscoverySink"/>).
    /// </summary>
    internal async Task DiscoverTestsAsync(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, IUnitTestElementSink elementSink, IConfiguration? configuration, bool isMTP)
    {
        if (elementSink is null)
        {
            throw new ArgumentNullException(nameof(elementSink));
        }

        await DiscoverTestsCoreAsync(sources, discoveryContext, logger, elementSink, discoverySink: null, configuration, isMTP).ConfigureAwait(false);
    }

    private async Task DiscoverTestsCoreAsync(IEnumerable<string> sources, IDiscoveryContext? discoveryContext, IMessageLogger logger, IUnitTestElementSink? elementSink, ITestCaseDiscoverySink? discoverySink, IConfiguration? configuration, bool isMTP)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        // Unwrap the VSTest discovery context into neutral inputs at this boundary and hand them to the
        // platform-agnostic engine. The engine owns telemetry lifetime and the calls into the platform-services
        // discovery pipeline; it never depends on the VSTest object model.
        IAdapterMessageLogger adapterLogger = logger.ToAdapterMessageLogger();
        IUnitTestElementSink unitTestElementSink = elementSink ?? discoverySink!.ToUnitTestElementSink();

#if !WINDOWS_UWP && !WIN_UI
        var engine = new MSTestEngine(CancellationToken.None, _telemetrySender);
#else
        var engine = new MSTestEngine(CancellationToken.None);
#endif

        await engine.DiscoverAsync(
            sources,
            discoveryContext?.RunSettings?.SettingsXml,
            adapterLogger,
            unitTestElementSink,
            new TestElementFilterProvider(discoveryContext),
            configuration,
            _testSourceHandler,
            isMTP).ConfigureAwait(false);
    }
}
