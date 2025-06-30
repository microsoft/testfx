// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Framework;

internal sealed class TestArgumentsManager : ITestArgumentsManager
{
    private readonly Dictionary<TestNodeUid, Func<TestArgumentsContext, ITestArgumentsEntry>> _testArgumentsEntryProviders = [];
    private bool _isRegistrationFrozen;

    public void RegisterTestArgumentsEntryProvider<TArguments>(
        TestNodeUid testNodeStableUid,
        Func<TestArgumentsContext, InternalUnsafeTestArgumentsEntry<TArguments>> argumentPropertiesProviderCallback)
    {
        if (_isRegistrationFrozen)
        {
            throw new InvalidOperationException("Cannot register TestArgumentsEntry provider after registration is frozen.");
        }

        if (_testArgumentsEntryProviders.ContainsKey(testNodeStableUid))
        {
            throw new InvalidOperationException($"TestArgumentsEntry provider is already registered for test node with UID '{testNodeStableUid}'.");
        }

        _testArgumentsEntryProviders.Add(testNodeStableUid, argumentPropertiesProviderCallback);
    }

    internal void FreezeRegistration() => _isRegistrationFrozen = true;

    internal static bool IsExpandableTestNode(TestNode testNode)
        => testNode is IExpandableTestNode
        && !testNode.Properties.OfType<FrameworkEngineMetadataProperty>().SingleOrDefault().PreventArgumentsExpansion;

    internal async Task<TestNode> ExpandTestNodeAsync(TestNode currentNode)
    {
        RoslynDebug.Assert(IsExpandableTestNode(currentNode), "Test node is not expandable");

        var expandableTestNode = (IExpandableTestNode)currentNode;

        int argumentsRowIndex = -1;
        bool isIndexArgumentPropertiesProvider = false;
        if (!_testArgumentsEntryProviders.TryGetValue(
            currentNode.StableUid,
            out Func<TestArgumentsContext, ITestArgumentsEntry>? argumentPropertiesProvider))
        {
            isIndexArgumentPropertiesProvider = true;
            argumentPropertiesProvider = argument =>
            {
                string fragment = $"[{argumentsRowIndex}]";
                return new InternalUnsafeTestArgumentsEntry<object>(argument.Arguments, fragment, fragment);
            };
        }

        HashSet<TestNodeUid> expandedTestNodeUids = [];
        List<TestNode> expandedTestNodes = [.. currentNode.Tests];
        switch (currentNode)
        {
            case IParameterizedTestNode parameterizedTestNode:
                foreach (object? arguments in parameterizedTestNode.GetArguments())
                {
                    ExpandNodeWithArguments(currentNode, arguments, ref argumentsRowIndex, expandedTestNodes,
                        expandedTestNodeUids, argumentPropertiesProvider, isIndexArgumentPropertiesProvider);
                }

                break;

            case ITaskParameterizedTestNode parameterizedTestNode:
                foreach (object? arguments in await parameterizedTestNode.GetArguments().ConfigureAwait(false))
                {
                    ExpandNodeWithArguments(currentNode, arguments, ref argumentsRowIndex, expandedTestNodes,
                        expandedTestNodeUids, argumentPropertiesProvider, isIndexArgumentPropertiesProvider);
                }

                break;

#if NET
            case IAsyncParameterizedTestNode parameterizedTestNode:
                await foreach (object? arguments in parameterizedTestNode.GetArguments().ConfigureAwait(false))
                {
                    ExpandNodeWithArguments(currentNode, arguments, ref argumentsRowIndex, expandedTestNodes,
                        expandedTestNodeUids, argumentPropertiesProvider, isIndexArgumentPropertiesProvider);
                }

                break;
#endif

            default:
                throw new InvalidOperationException($"Unexpected parameterized test node type: '{currentNode.GetType()}'");
        }

        // When the node is expandable, we need to create a new node that is not an action node, but a container
        // node that contains the expanded nodes. This is this node that will be executed.
        TestNode expandedNode = new()
        {
            StableUid = currentNode.StableUid,
            DisplayName = currentNode.DisplayName,
            OverriddenEdgeName = currentNode.OverriddenEdgeName,
            Properties = currentNode.Properties,
            Tests = [.. expandedTestNodes],
        };

        return expandedNode;

        // Local functions
        static void ExpandNodeWithArguments(TestNode testNode, object arguments, ref int argumentsRowIndex, List<TestNode> expandedTestNodes,
            HashSet<TestNodeUid> expandedTestNodeUids, Func<TestArgumentsContext, ITestArgumentsEntry> argumentPropertiesProvider,
            bool isIndexArgumentPropertiesProvider)
        {
            // We need to increase the index before calling the argumentPropertiesProvider, because it is capturing it's value
            argumentsRowIndex++;
            bool shouldWrapInParenthesis = true;
            if (arguments is not ITestArgumentsEntry testArgumentsEntry)
            {
                shouldWrapInParenthesis = !isIndexArgumentPropertiesProvider;
                testArgumentsEntry = argumentPropertiesProvider(new(arguments, testNode))!;
            }

            string argumentFragmentUid = GetArgumentFragmentUid(testArgumentsEntry, shouldWrapInParenthesis);
            string argumentFragmentDisplayName = GetArgumentFragmentDisplayName(testArgumentsEntry, shouldWrapInParenthesis);
            TestNode expandedTestNode = ((IExpandableTestNode)testNode).GetExpandedTestNode(arguments, argumentFragmentUid,
                argumentFragmentDisplayName);
            if (!expandedTestNodeUids.Add(expandedTestNode.StableUid))
            {
                throw new InvalidOperationException(
                    $"Expanded test node with UID '{expandedTestNode.StableUid}' is not unique for test node '{testNode.StableUid}'.");
            }

            expandedTestNodes.Add(expandedTestNode);
        }

        static string GetArgumentFragmentUid(ITestArgumentsEntry testArgumentsEntry, bool shouldWrapInParenthesis)
            => CreateWrappedName(testArgumentsEntry.UidFragment, shouldWrapInParenthesis);

        static string GetArgumentFragmentDisplayName(ITestArgumentsEntry testArgumentsEntry, bool shouldWrapInParenthesis)
            => CreateWrappedName(testArgumentsEntry.DisplayNameFragment ?? testArgumentsEntry.UidFragment, shouldWrapInParenthesis);

        static string CreateWrappedName(string name, bool shouldWrapInParenthesis)
        {
            StringBuilder displayNameBuilder = new();

            if (shouldWrapInParenthesis)
            {
                displayNameBuilder.Append('(');
            }

            displayNameBuilder.Append(name);

            if (shouldWrapInParenthesis)
            {
                displayNameBuilder.Append(')');
            }

            return displayNameBuilder.ToString();
        }
    }
}
