// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// Bridge implementation of <see cref="ITestCaseDiscoverySink"/> that forwards calls to VSTest and Microsoft Testing Platforms.
/// </summary>
internal sealed class TestCaseDiscoverySinkAdapter : ITestCaseDiscoverySink
{
    /// <remarks>
    /// Not null when used in the context of VSTest.
    /// </remarks>
    private readonly ITestCaseDiscoverySink? _testCaseDiscoverySink;
    private readonly ILogger<TestCaseDiscoverySinkAdapter> _logger;
    private readonly INamedFeatureCapability? _namedFeatureCapability;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IClientInfo _clientInfo;
    private readonly IMessageBus _messageBus;
    private readonly bool _isTrxEnabled;
    private readonly VSTestBridgedTestFrameworkBase _adapterExtension;
    private readonly TestSessionContext _session;
    private readonly CancellationToken _cancellationToken;
    private readonly string? _testAssemblyPath;

    public TestCaseDiscoverySinkAdapter(
        VSTestBridgedTestFrameworkBase adapterExtension,
        TestSessionContext session,
        string[] testAssemblyPaths,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        INamedFeatureCapability? namedFeatureCapability,
        ICommandLineOptions commandLineOptions,
        IClientInfo clientInfo,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        bool isTrxEnabled,
        CancellationToken cancellationToken,
        ITestCaseDiscoverySink? testCaseDiscoverySink = null)
    {
        if (testAssemblyPaths.Length == 0)
        {
            throw new ArgumentException($"{nameof(testAssemblyPaths)} should contain at least one test assembly.");
        }
        else if (testAssemblyPaths.Length > 1)
        {
            _testAssemblyPath = testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

            if (!testAssemblyPaths.Contains(_testAssemblyPath))
            {
                throw new ArgumentException("None of the test assemblies are the test application.");
            }
        }
        else
        {
            _testAssemblyPath = testAssemblyPaths[0];
        }

        _testCaseDiscoverySink = testCaseDiscoverySink;
        _logger = loggerFactory.CreateLogger<TestCaseDiscoverySinkAdapter>();
        _namedFeatureCapability = namedFeatureCapability;
        _commandLineOptions = commandLineOptions;
        _clientInfo = clientInfo;
        _messageBus = messageBus;
        _isTrxEnabled = isTrxEnabled;
        _adapterExtension = adapterExtension;
        _session = session;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc/>
    public void SendTestCase(TestCase discoveredTest)
    {
        _logger.LogTrace("BridgeTestCaseDiscoverySink.SendTestCase");

        _cancellationToken.ThrowIfCancellationRequested();

        discoveredTest.FixUpTestCase(_testAssemblyPath);

        // Forward call to VSTest
        _testCaseDiscoverySink?.SendTestCase(discoveredTest);

        // Publish node state change to Microsoft Testing Platform
        var testNode = discoveredTest.ToTestNode(_isTrxEnabled, _adapterExtension.UseFullyQualifiedNameAsTestNodeUid, _namedFeatureCapability, _commandLineOptions, _clientInfo);
        var testNodeChange = new TestNodeUpdateMessage(_session.SessionUid, testNode);
        testNodeChange.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
        _messageBus.PublishAsync(_adapterExtension, testNodeChange).Await();
    }
}
