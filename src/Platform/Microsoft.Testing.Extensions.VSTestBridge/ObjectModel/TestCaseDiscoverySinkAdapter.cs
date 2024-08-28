// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
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
    private readonly IMessageBus _messageBus;
    private readonly bool _isTrxEnabled;
    private readonly IClientInfo _clientInfo;
    private readonly VSTestBridgedTestFrameworkBase _adapterExtension;
    private readonly TestSessionContext _session;
    private readonly CancellationToken _cancellationToken;
    private readonly string? _testAssemblyPath;

    public TestCaseDiscoverySinkAdapter(VSTestBridgedTestFrameworkBase adapterExtension, TestSessionContext session, string[] testAssemblyPaths,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory,
        IMessageBus messageBus, bool isTrxEnabled,
        IClientInfo clientInfo,
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
        _messageBus = messageBus;
        _isTrxEnabled = isTrxEnabled;
        _clientInfo = clientInfo;
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
        var testNode = discoveredTest.ToTestNode(_isTrxEnabled, _clientInfo);
        testNode.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
        var testNodeChange = new TestNodeUpdateMessage(_session.SessionUid, testNode);

        _messageBus.PublishAsync(_adapterExtension, testNodeChange).Await();
    }
}
