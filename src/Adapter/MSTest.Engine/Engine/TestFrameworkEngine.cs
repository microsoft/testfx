// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Adapter;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;

using PlatformIConfiguration = Microsoft.Testing.Platform.Configurations.IConfiguration;
using PlatformTestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Framework;

internal sealed class TestFrameworkEngine : IDataProducer
{
    private readonly TestingFrameworkExtension _extension;
    private readonly ITestFrameworkCapabilities _capabilities;
    private readonly IClock _clock;
    private readonly ITask _task;
    private readonly TestFrameworkConfiguration _testFrameworkConfiguration;
    private readonly ITestNodesBuilder[] _testNodesBuilders;
    private readonly ConfigurationWrapper _configuration;

    public TestFrameworkEngine(TestFrameworkConfiguration testFrameworkConfiguration, ITestNodesBuilder[] testNodesBuilders, TestingFrameworkExtension extension, ITestFrameworkCapabilities capabilities,
        IClock clock, ITask task, PlatformIConfiguration configuration)
    {
        _extension = extension;
        _capabilities = capabilities;
        _clock = clock;
        _task = task;
        _testFrameworkConfiguration = testFrameworkConfiguration;
        _testNodesBuilders = testNodesBuilders;
        _configuration = new(configuration);
    }

    public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync().ConfigureAwait(false);

    public async Task<Result> ExecuteRequestAsync(TestExecutionRequest testExecutionRequest, IMessageBus messageBus, CancellationToken cancellationToken)
        => testExecutionRequest switch
        {
            DiscoverTestExecutionRequest discoveryRequest => await ExecuteTestNodeDiscoveryAsync(discoveryRequest, messageBus, cancellationToken).ConfigureAwait(false),
            RunTestExecutionRequest runRequest => await ExecuteTestNodeRunAsync(runRequest, messageBus, cancellationToken).ConfigureAwait(false),
            _ => Result.Fail($"Unexpected request type: '{testExecutionRequest.GetType().FullName}'"),
        };

    private async Task<Result> ExecuteTestNodeRunAsync(RunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        List<TestNode> allRootTestNodes = [];
        TestFixtureManager fixtureManager = new(cancellationToken);
        TestArgumentsManager argumentsManager = new();
        TestSessionContext testSessionContext = new(_configuration, fixtureManager, argumentsManager, request.Session.SessionUid,
            PublishDataAsync, cancellationToken);

        try
        {
            foreach (ITestNodesBuilder testNodeBuilder in _testNodesBuilders)
            {
                TestNode[] testNodes = await testNodeBuilder.BuildAsync(testSessionContext).ConfigureAwait(false);
                allRootTestNodes.AddRange(testNodes);
            }

            // We have built all test nodes, now we need to process them. Before that, we want to make sure to freeze managers
            // to ensure that no new registrations are allowed.
            fixtureManager.FreezeRegistration();
            argumentsManager.FreezeRegistration();

            BFSTestNodeVisitor testNodesVisitor = new(allRootTestNodes, request.Filter, argumentsManager);
            ThreadPoolTestNodeRunner testNodeRunner = new(_testFrameworkConfiguration, _capabilities, _clock, _task, _configuration, request.Session.SessionUid,
                PublishDataAsync, fixtureManager, cancellationToken);

            await testNodesVisitor.VisitAsync((testNode, parentTestNodeUid) =>
            {
                if (testNode is IActionableTestNode)
                {
                    testNodeRunner.EnqueueTest(testNode, parentTestNodeUid);
                }

                string[] fixtureIds = testNode.Properties
                    .OfType<FrameworkEngineMetadataProperty>()
                    .SingleOrDefault()
                    .UsedFixtureIds
                    ?? [];
                fixtureManager.RegisterFixtureUsage(testNode, fixtureIds);

                return Task.CompletedTask;
            }).ConfigureAwait(false);

            if (testNodesVisitor.DuplicatedNodes.Length > 0)
            {
                StringBuilder errorMessageBuilder = new();
                errorMessageBuilder.AppendLine("Found multiple test nodes with the same UID:");
                foreach (KeyValuePair<TestNodeUid, List<TestNode>> duplicate in testNodesVisitor.DuplicatedNodes)
                {
                    errorMessageBuilder.Append(CultureInfo.InvariantCulture, $"- For {duplicate.Key}: tests ");
                    errorMessageBuilder.AppendLine(string.Join(", ", duplicate.Value.Select(x => x.DisplayName)));
                }

                return Result.Fail(errorMessageBuilder.ToString());
            }

            // Then, we want to freeze the registration of the fixtures, so that we can't add new fixtures.
            fixtureManager.FreezeUsageRegistration();
            testNodeRunner.StartTests();

            // Finally, we want to wait for all tests to complete.
            return await testNodeRunner.WaitAllTestsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (ITestNodesBuilder testNodeBuilder in _testNodesBuilders)
            {
                await DisposeHelper.DisposeAsync(testNodeBuilder).ConfigureAwait(false);
            }
        }

        // Local functions
        Task PublishDataAsync(IData data)
        {
            RoslynDebug.Assert(DataTypesProduced.Contains(data.GetType()), "Published data type hasn't been declared");

            return messageBus.PublishAsync(this, data);
        }
    }

    private async Task<Result> ExecuteTestNodeDiscoveryAsync(DiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        List<TestNode> allRootTestNodes = [];
        TestFixtureManager fixtureManager = new(cancellationToken);
        TestArgumentsManager argumentsManager = new();
        TestSessionContext testSessionContext = new(_configuration, fixtureManager, argumentsManager, request.Session.SessionUid,
            PublishDataAsync, cancellationToken);

        try
        {
            foreach (ITestNodesBuilder testNodeBuilder in _testNodesBuilders)
            {
                TestNode[] testNodes = await testNodeBuilder.BuildAsync(testSessionContext).ConfigureAwait(false);
                allRootTestNodes.AddRange(testNodes);
            }

            // We have built all test nodes, now we need to process them. Before that, we want to make sure to freeze managers
            // to ensure that no new registrations are allowed.
            fixtureManager.FreezeRegistration();
            argumentsManager.FreezeRegistration();

            BFSTestNodeVisitor testNodesVisitor = new(allRootTestNodes, request.Filter, argumentsManager);

            await testNodesVisitor.VisitAsync(async (testNode, parentTestNodeUid) =>
            {
                PlatformTestNode progressNode = testNode.ToPlatformTestNode();
                var testNodeUpdateMessage = new TestNodeUpdateMessage(request.Session.SessionUid, progressNode,
                    parentTestNodeUid?.ToPlatformTestNodeUid());
                if (testNode is IActionableTestNode)
                {
                    testNodeUpdateMessage.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
                }

                await messageBus.PublishAsync(this, testNodeUpdateMessage).ConfigureAwait(false);
            }).ConfigureAwait(false);

            if (testNodesVisitor.DuplicatedNodes.Length > 0)
            {
                StringBuilder errorMessageBuilder = new();
                errorMessageBuilder.AppendLine("Found multiple test nodes with the same UID:");
                foreach (KeyValuePair<TestNodeUid, List<TestNode>> duplicate in testNodesVisitor.DuplicatedNodes)
                {
                    errorMessageBuilder.Append(CultureInfo.InvariantCulture, $"- For {duplicate.Key}: tests ");
                    errorMessageBuilder.AppendLine(string.Join(", ", duplicate.Value.Select(x => x.DisplayName)));
                }

                return Result.Fail(errorMessageBuilder.ToString());
            }
        }
        finally
        {
            foreach (ITestNodesBuilder testNodeBuilder in _testNodesBuilders)
            {
                await DisposeHelper.DisposeAsync(testNodeBuilder).ConfigureAwait(false);
            }
        }

        return Result.Ok();

        // Local functions
        Task PublishDataAsync(IData data)
        {
            RoslynDebug.Assert(DataTypesProduced.Contains(data.GetType()), "Published data type hasn't been declared");

            return messageBus.PublishAsync(this, data);
        }
    }
}
