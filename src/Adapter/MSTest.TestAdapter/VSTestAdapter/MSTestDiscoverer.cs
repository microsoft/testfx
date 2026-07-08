// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Contains the discovery logic for this adapter.
/// </summary>
[DefaultExecutorUri(MSTestAdapter.PlatformServices.EngineConstants.ExecutorUriString)]
[FileExtension(".xap")]
[FileExtension(".appx")]
[FileExtension(".dll")]
[FileExtension(".exe")]
internal sealed class MSTestDiscoverer : ITestDiscoverer
{
    private readonly ITestSourceHandler _testSourceHandler;
#if !WINDOWS_UWP && !WIN_UI
    private readonly Func<string, IDictionary<string, object>, Task>? _telemetrySender;
#endif

    // The parameterless constructor is required by VSTest, which instantiates the
    // discoverer via reflection. The internal constructor exists for tests and for the
    // MTP bridge (MSTestBridgedTestFramework) which injects a telemetry sender.
    public MSTestDiscoverer()
        : this(new TestSourceHandler())
    {
    }

    internal MSTestDiscoverer(ITestSourceHandler testSourceHandler, Func<string, IDictionary<string, object>, Task>? telemetrySender = null)
    {
        _testSourceHandler = testSourceHandler;
#if !WINDOWS_UWP && !WIN_UI
        _telemetrySender = telemetrySender;
#else
        _ = telemetrySender;
#endif
    }

    /// <summary>
    /// Discovers the tests available from the provided source. Not supported for .xap source.
    /// </summary>
    /// <param name="sources">Collection of test containers.</param>
    /// <param name="discoveryContext">Context in which discovery is being performed.</param>
    /// <param name="logger">Logger used to log messages.</param>
    /// <param name="discoverySink">Used to send testcases and discovery related events back to Discoverer manager.</param>
    [System.Security.SecurityCritical]
    [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Discovery context can be null.")]
    public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        // VSTest's ITestDiscoverer is a synchronous interface. The telemetry sender is null in
        // this code path (only the MTP bridge supplies one), so the awaited send below completes
        // synchronously and GetAwaiter().GetResult() does not actually block on I/O.
        => DiscoverTestsAsync(sources, discoveryContext, logger, discoverySink, configuration: null, isMTP: false).GetAwaiter().GetResult();

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

        // Initialize telemetry collection if not already set (e.g. first call in the session).
#if !WINDOWS_UWP && !WIN_UI
        if (!MSTestTelemetryDataCollector.IsTelemetryOptedOut())
        {
            _ = MSTestTelemetryDataCollector.EnsureInitialized();
        }
#endif

        try
        {
            IAdapterMessageLogger adapterLogger = logger.ToAdapterMessageLogger();
            IUnitTestElementSink unitTestElementSink = elementSink ?? discoverySink!.ToUnitTestElementSink();
            if (MSTestDiscovererHelpers.InitializeDiscovery(sources, discoveryContext?.RunSettings?.SettingsXml, adapterLogger, configuration, _testSourceHandler))
            {
                await new UnitTestDiscoverer(_testSourceHandler).DiscoverTestsAsync(sources, adapterLogger, unitTestElementSink, discoveryContext?.RunSettings?.SettingsXml, new TestElementFilterProvider(discoveryContext), isMTP).ConfigureAwait(false);
            }
        }
        finally
        {
#if !WINDOWS_UWP && !WIN_UI
            // Send the discovery telemetry event ('mstest/discovery'). This always runs at the
            // end of discovery — for discover-only sessions it is the only event; for sessions
            // where a run follows, MSTestExecutor will send a separate 'mstest/sessionexit' event
            // carrying assertion usage. Keeping the two events distinct avoids settings/attribute
            // duplication and lets each event be self-contained.
            await MSTestTelemetryDataCollector.SendDiscoveryTelemetryAndResetAsync(_telemetrySender).ConfigureAwait(false);
#endif
        }
    }
}
