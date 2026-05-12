// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Web;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Framework;

internal sealed class BFSTestNodeVisitor
{
    private static readonly string PathSeparatorString = TreeNodeFilter.PathSeparator.ToString();

    // Read-only; must never be mutated. Shared across all BFS traversals where ContainsPropertyFilters is false.
    private static readonly PropertyBag EmptyPropertyBag = new();

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
        // Precompute a HashSet<string> for O(1) UID lookups when filtering by UID list.
        // Using string values directly avoids allocating a wrapper for each lookup.
        HashSet<string>? uidFilterSet = _testExecutionFilter is TestNodeUidListFilter listFilter
            ? new HashSet<string>(listFilter.TestNodeUids.Select(static uid => uid.Value), StringComparer.Ordinal)
            : null;

        // This is case sensitive, and culture insensitive, to keep UIDs unique, and comparable between different system.
        Dictionary<TestNodeUid, List<TestNode>> testNodesByUid = [];

        // Use string (instead of StringBuilder) in the queue to avoid allocating one StringBuilder per node.
        // Each node's full path is an immutable string that can be shared as the base for child paths.
        Queue<(TestNode CurrentNode, TestNodeUid? ParentNodeUid, string NodeFullPath)> queue = new();
        foreach (TestNode node in _rootTestNodes)
        {
            queue.Enqueue((node, null, string.Empty));
        }

        while (queue.Count > 0)
        {
            (TestNode currentNode, TestNodeUid? parentNodeUid, string nodeFullPath) = queue.Dequeue();

            if (!testNodesByUid.TryGetValue(currentNode.StableUid, out List<TestNode>? testNodes))
            {
                testNodes = [];
                testNodesByUid.Add(currentNode.StableUid, testNodes);
            }

            testNodes.Add(currentNode);

            // We want to encode the path fragment to avoid conflicts with the separator. We are using URL encoding because it is
            // a well-known proven standard encoding that is reversible.
            string encodedName = EncodeString(currentNode.OverriddenEdgeName ?? currentNode.DisplayName);
            string currentNodeFullPath = nodeFullPath.Length == 0 || nodeFullPath[^1] != TreeNodeFilter.PathSeparator
                ? string.Concat(nodeFullPath, PathSeparatorString, encodedName)
                : string.Concat(nodeFullPath, encodedName);

            // When we are filtering as tree filter and the current node does not match the filter, we skip the node and its children.
            if (_testExecutionFilter is TreeNodeFilter treeNodeFilter)
            {
                PropertyBag filterPropertyBag = treeNodeFilter.ContainsPropertyFilters
                    ? new PropertyBag(currentNode.Properties)
                    : EmptyPropertyBag;
                if (!treeNodeFilter.MatchesFilter(currentNodeFullPath, filterPropertyBag))
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
            if (uidFilterSet is null
                || uidFilterSet.Contains(currentNode.StableUid.Value))
            {
                await onIncludedTestNodeAsync(currentNode, parentNodeUid).ConfigureAwait(false);
            }

            foreach (TestNode childNode in currentNode.Tests)
            {
                queue.Enqueue((childNode, currentNode.StableUid, currentNodeFullPath));
            }
        }

        DuplicatedNodes = [.. testNodesByUid.Where(x => x.Value.Count > 1)];
    }

    private static string EncodeString(string value)
        => HttpUtility.UrlEncode(value);
}
