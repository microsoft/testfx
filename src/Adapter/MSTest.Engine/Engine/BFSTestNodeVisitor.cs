// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Web;

using Microsoft.Testing.Framework.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Framework;

internal sealed class BFSTestNodeVisitor
{
    private readonly IEnumerable<TestNode> _rootTestNodes;
    private readonly ITestExecutionFilter _testExecutionFilter;
    private readonly TestArgumentsManager _testArgumentsManager;

    public BFSTestNodeVisitor(IEnumerable<TestNode> rootTestNodes, ITestExecutionFilter testExecutionFilter,
        TestArgumentsManager testArgumentsManager)
    {
        if (testExecutionFilter is not TreeNodeFilter and not TestNodeUidListFilter and not NopFilter)
        {
            throw new ArgumentOutOfRangeException(nameof(testExecutionFilter));
        }

        _rootTestNodes = rootTestNodes;
        _testExecutionFilter = testExecutionFilter;
        _testArgumentsManager = testArgumentsManager;
    }

    internal KeyValuePair<TestNodeUid, List<TestNode>>[] DuplicatedNodes { get; private set; } = [];

    public async Task VisitAsync(Func<TestNode, TestNodeUid?, Task> onIncludedTestNodeAsync)
    {
        // This is case sensitive, and culture insensitive, to keep UIDs unique, and comparable between different system.
        Dictionary<TestNodeUid, List<TestNode>> testNodesByUid = [];
        Queue<(TestNode CurrentNode, TestNodeUid? ParentNodeUid, StringBuilder NodeFullPath)> queue = new();
        foreach (TestNode node in _rootTestNodes)
        {
            queue.Enqueue((node, null, new()));
        }

        while (queue.Count > 0)
        {
            (TestNode currentNode, TestNodeUid? parentNodeUid, StringBuilder nodeFullPath) = queue.Dequeue();

            if (!testNodesByUid.TryGetValue(currentNode.StableUid, out List<TestNode>? testNodes))
            {
                testNodes = [];
                testNodesByUid.Add(currentNode.StableUid, testNodes);
            }

            testNodes.Add(currentNode);

            StringBuilder nodeFullPathForChildren = new StringBuilder().Append(nodeFullPath);

            if (nodeFullPathForChildren.Length == 0
                || nodeFullPathForChildren[^1] != TreeNodeFilter.PathSeparator)
            {
                nodeFullPathForChildren.Append(TreeNodeFilter.PathSeparator);
            }

            // We want to encode the path fragment to avoid conflicts with the separator. We are using URL encoding because it is
            // a well-known proven standard encoding that is reversible.
            nodeFullPathForChildren.Append(EncodeString(currentNode.OverriddenEdgeName ?? currentNode.DisplayName));
            string currentNodeFullPath = nodeFullPathForChildren.ToString();

            // When we are filtering as tree filter and the current node does not match the filter, we skip the node and its children.
            if (_testExecutionFilter is TreeNodeFilter treeNodeFilter)
            {
                if (!treeNodeFilter.MatchesFilter(currentNodeFullPath, CreatePropertyBagForFilter(currentNode.Properties)))
                {
                    continue;
                }
            }

            // If the node is expandable, we expand it (replacing the original node)
            if (TestArgumentsManager.IsExpandableTestNode(currentNode))
            {
                currentNode = await _testArgumentsManager.ExpandTestNodeAsync(currentNode).ConfigureAwait(false);
            }

            // If the node is not filtered out by the test execution filter, we call the callback with the node.
            if (_testExecutionFilter is not TestNodeUidListFilter listFilter
                || listFilter.TestNodeUids.Any(uid => currentNode.StableUid.ToPlatformTestNodeUid() == uid))
            {
                await onIncludedTestNodeAsync(currentNode, parentNodeUid).ConfigureAwait(false);
            }

            foreach (TestNode childNode in currentNode.Tests)
            {
                queue.Enqueue((childNode, currentNode.StableUid, nodeFullPathForChildren));
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

    private static string EncodeString(string value)
        => HttpUtility.UrlEncode(value);
}
