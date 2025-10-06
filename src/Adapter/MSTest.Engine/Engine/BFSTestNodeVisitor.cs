// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

using PlatformTestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

namespace Microsoft.Testing.Framework;

internal sealed class BFSTestNodeVisitor
{
    private readonly IEnumerable<TestNode> _rootTestNodes;
    private readonly ITestExecutionFilter _testExecutionFilter;
    private readonly TestArgumentsManager _testArgumentsManager;

    public BFSTestNodeVisitor(IEnumerable<TestNode> rootTestNodes, ITestExecutionFilter testExecutionFilter,
        TestArgumentsManager testArgumentsManager)
    {
        _rootTestNodes = rootTestNodes;
        _testExecutionFilter = testExecutionFilter;
        _testArgumentsManager = testArgumentsManager;
    }

    internal KeyValuePair<TestNodeUid, List<TestNode>>[] DuplicatedNodes { get; private set; } = [];

    public async Task VisitAsync(Func<TestNode, TestNodeUid?, Task> onIncludedTestNodeAsync)
    {
        // This is case sensitive, and culture insensitive, to keep UIDs unique, and comparable between different system.
        Dictionary<TestNodeUid, List<TestNode>> testNodesByUid = [];
        Queue<(TestNode CurrentNode, TestNodeUid? ParentNodeUid)> queue = new();
        foreach (TestNode node in _rootTestNodes)
        {
            queue.Enqueue((node, null));
        }

        while (queue.Count > 0)
        {
            (TestNode currentNode, TestNodeUid? parentNodeUid) = queue.Dequeue();

            if (!testNodesByUid.TryGetValue(currentNode.StableUid, out List<TestNode>? testNodes))
            {
                testNodes = [];
                testNodesByUid.Add(currentNode.StableUid, testNodes);
            }

            testNodes.Add(currentNode);

            if (TestArgumentsManager.IsExpandableTestNode(currentNode))
            {
                currentNode = await _testArgumentsManager.ExpandTestNodeAsync(currentNode).ConfigureAwait(false);
            }

            PlatformTestNode platformTestNode = new()
            {
                Uid = currentNode.StableUid.ToPlatformTestNodeUid(),
                DisplayName = currentNode.DisplayName,
                Properties = CreatePropertyBagForFilter(currentNode.Properties),
            };

            if (!_testExecutionFilter.Matches(platformTestNode))
            {
                continue;
            }

            await onIncludedTestNodeAsync(currentNode, parentNodeUid).ConfigureAwait(false);

            foreach (TestNode childNode in currentNode.Tests)
            {
                queue.Enqueue((childNode, currentNode.StableUid));
            }
        }

        DuplicatedNodes = [.. testNodesByUid.Where(x => x.Value.Count > 1)];
    }

    private static PropertyBag CreatePropertyBagForFilter(IProperty[] properties)
    {
        PropertyBag propertyBag = new();
        foreach (IProperty property in properties)
        {
            propertyBag.Add(property);
        }

        return propertyBag;
    }
}
