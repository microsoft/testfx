// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace Microsoft.Testing.Framework.UnitTests;

[TestClass]
public sealed class BFSTestNodeVisitorTests : TestBase
{
    [TestMethod]
    public async Task Visit_WhenFilterDoesNotUseEncodedSlash_NodeIsNotIncluded()
    {
        // Arrange
        var rootNode = new TestNode
        {
            StableUid = "ID1",
            DisplayName = "A",
            Tests =
            [
                new TestNode
                {
                    StableUid = "ID2",
                    DisplayName = "B/C",
                },
            ],
        };

        var filter = new TreeNodeFilter("/A/B/C");
        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, filter, null!);

        // Act
        List<TestNode> includedTestNodes = [];
        await visitor.VisitAsync((testNode, _) =>
        {
            includedTestNodes.Add(testNode);
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(1, includedTestNodes.Count);
        Assert.AreEqual("ID1", includedTestNodes[0].StableUid);
    }

    [DataRow("/", "%2F")]
    [DataRow("%2F", "%252F")]
    [DataRow("//", "%2F%2F")]
    [TestMethod]
    public async Task Visit_WhenFilterUsesEncodedEntry_NodeIsIncluded(string nodeSpecialString, string filterEncodedSpecialString)
    {
        // Arrange
        var rootNode = new TestNode
        {
            StableUid = "ID1",
            DisplayName = "A",
            Tests =
            [
                new TestNode
                {
                    StableUid = "ID2",
                    DisplayName = "B" + nodeSpecialString + "C",
                },
            ],
        };

        var filter = new TreeNodeFilter("/A/B" + filterEncodedSpecialString + "C");
        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, filter, null!);

        // Act
        List<TestNode> includedTestNodes = [];
        await visitor.VisitAsync((testNode, _) =>
        {
            includedTestNodes.Add(testNode);
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(2, includedTestNodes.Count);
        Assert.AreEqual("ID1", includedTestNodes[0].StableUid);
        Assert.AreEqual("ID2", includedTestNodes[1].StableUid);
    }

    [DataRow(nameof(TestNode))]
    [DataRow(nameof(InternalUnsafeActionTestNode))]
    [TestMethod]
    public async Task Visit_WhenNodeIsNotParameterizedNode_DoesNotExpand(string nonParameterizedTestNode)
    {
        // Arrange
        TestNode rootNode = nonParameterizedTestNode switch
        {
            nameof(TestNode) => new TestNode
            {
                StableUid = "ID1",
                DisplayName = "A",
            },
            nameof(InternalUnsafeActionTestNode) => new InternalUnsafeActionTestNode
            {
                StableUid = "ID1",
                DisplayName = "A",
                Body = _ => { },
            },
            _ => throw new ArgumentException($"Unknown test node type: {nonParameterizedTestNode}", nameof(nonParameterizedTestNode)),
        };

        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, new NopFilter(), null!);

        // Act
        List<TestNode> includedTestNodes = [];
        await visitor.VisitAsync((testNode, _) =>
        {
            includedTestNodes.Add(testNode);
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(1, includedTestNodes.Count);
        Assert.AreEqual("ID1", includedTestNodes[0].StableUid);
    }

    [DataRow(nameof(InternalUnsafeActionParameterizedTestNode<>), true)]
    [DataRow(nameof(InternalUnsafeActionParameterizedTestNode<>), false)]
    [TestMethod]
    public async Task Visit_WhenNodeIsParameterizedNodeAndPropertyIsAbsentOrTrue_ExpandNode(string parameterizedTestNode, bool hasExpansionProperty)
    {
        // Arrange
        TestNode rootNode = CreateParameterizedTestNode(parameterizedTestNode, hasExpansionProperty ? false : null);
        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, new NopFilter(), new TestArgumentsManager());

        // Act
        List<(TestNode Node, TestNodeUid? ParentNodeUid)> includedTestNodes = [];
        await visitor.VisitAsync((testNode, parentNodeUid) =>
        {
            includedTestNodes.Add((testNode, parentNodeUid));
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(3, includedTestNodes.Count);

        Assert.AreEqual("ID1", includedTestNodes[0].Node.StableUid);
        Assert.IsNull(includedTestNodes[0].ParentNodeUid);

        Assert.AreEqual("ID1 [0]", includedTestNodes[1].Node.StableUid);
        Assert.AreEqual("ID1", includedTestNodes[1].ParentNodeUid);

        Assert.AreEqual("ID1 [1]", includedTestNodes[2].Node.StableUid);
        Assert.AreEqual("ID1", includedTestNodes[2].ParentNodeUid);
    }

    [DataRow(nameof(InternalUnsafeActionParameterizedTestNode<>))]
    [TestMethod]
    public async Task Visit_WhenNodeIsParameterizedNodeAndDoesNotAllowExpansion_DoesNotExpand(string parameterizedTestNode)
    {
        // Arrange
        TestNode rootNode = CreateParameterizedTestNode(parameterizedTestNode, true);
        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, new NopFilter(), new TestArgumentsManager());

        // Act
        List<(TestNode Node, TestNodeUid? ParentNodeUid)> includedTestNodes = [];
        await visitor.VisitAsync((testNode, parentNodeUid) =>
        {
            includedTestNodes.Add((testNode, parentNodeUid));
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(1, includedTestNodes.Count);
    }

    [TestMethod]
    public async Task Visit_WithModuleNamespaceClassMethodLevelAndExpansion_DiscoverTestsWithCorrectParentsAndTypes()
    {
        // Arrange
        var rootNode = new TestNode
        {
            StableUid = "MyModule",
            DisplayName = "MyModule",
            Tests =
            [
                new TestNode
                {
                    StableUid = "MyNamespace",
                    DisplayName = "MyNamespace",
                    Tests =
                    [
                        new TestNode
                        {
                            StableUid = "MyType",
                            DisplayName = "MyType",
                            Tests = new[]
                            {
                                new InternalUnsafeActionParameterizedTestNode<byte>
                                {
                                    StableUid = "MyMethod",
                                    DisplayName = "MyMethod",
                                    GetArguments = () => new byte[] { 0, 1, 2 },
                                    Body = (_, _) => { },
                                },
                            },
                        },
                    ],
                },
            ],
        };
        var visitor = new BFSTestNodeVisitor(new[] { rootNode }, new NopFilter(), new TestArgumentsManager());

        // Act
        List<(TestNode Node, TestNodeUid? ParentNodeUid)> includedTestNodes = [];
        await visitor.VisitAsync((testNode, parentNodeUid) =>
        {
            includedTestNodes.Add((testNode, parentNodeUid));
            return Task.CompletedTask;
        });

        // Assert
        Assert.AreEqual(7, includedTestNodes.Count);

        Assert.AreEqual("MyModule", includedTestNodes[0].Node.StableUid);
        Assert.IsNull(includedTestNodes[0].ParentNodeUid);
        Assert.AreEqual(typeof(TestNode), includedTestNodes[0].Node.GetType());

        Assert.AreEqual("MyNamespace", includedTestNodes[1].Node.StableUid);
        Assert.AreEqual("MyModule", includedTestNodes[1].ParentNodeUid);
        Assert.AreEqual(typeof(TestNode), includedTestNodes[1].Node.GetType());

        Assert.AreEqual("MyType", includedTestNodes[2].Node.StableUid);
        Assert.AreEqual("MyNamespace", includedTestNodes[2].ParentNodeUid);
        Assert.AreEqual(typeof(TestNode), includedTestNodes[2].Node.GetType());

        Assert.AreEqual("MyMethod", includedTestNodes[3].Node.StableUid);
        Assert.AreEqual("MyType", includedTestNodes[3].ParentNodeUid);
        Assert.AreEqual(typeof(TestNode), includedTestNodes[3].Node.GetType());

        Assert.AreEqual("MyMethod [0]", includedTestNodes[4].Node.StableUid);
        Assert.AreEqual("MyMethod", includedTestNodes[4].ParentNodeUid);
        Assert.AreEqual(typeof(InternalUnsafeActionTestNode), includedTestNodes[4].Node.GetType());

        Assert.AreEqual("MyMethod [1]", includedTestNodes[5].Node.StableUid);
        Assert.AreEqual("MyMethod", includedTestNodes[5].ParentNodeUid);
        Assert.AreEqual(typeof(InternalUnsafeActionTestNode), includedTestNodes[5].Node.GetType());

        Assert.AreEqual("MyMethod [2]", includedTestNodes[6].Node.StableUid);
        Assert.AreEqual("MyMethod", includedTestNodes[6].ParentNodeUid);
        Assert.AreEqual(typeof(InternalUnsafeActionTestNode), includedTestNodes[6].Node.GetType());
    }

    private static TestNode CreateParameterizedTestNode(string parameterizedTestNode, bool? expansionPropertyValue)
    {
        TestNode rootNode = parameterizedTestNode switch
        {
            nameof(InternalUnsafeActionParameterizedTestNode<>) => new InternalUnsafeActionParameterizedTestNode<byte>
            {
                StableUid = "ID1",
                DisplayName = "A",
                Body = (_, _) => { },
                GetArguments = GetArguments,
                Properties = GetProperties(expansionPropertyValue),
            },
            _ => throw new ArgumentException($"Unknown test node type: {parameterizedTestNode}", nameof(parameterizedTestNode)),
        };

        return rootNode;

        // Local functions
        static IEnumerable<byte> GetArguments() => new byte[] { 0, 1 };
        static IProperty[] GetProperties(bool? hasExpansionProperty)
            => hasExpansionProperty.HasValue
                ?
                [
                    new FrameworkEngineMetadataProperty
                    {
                        PreventArgumentsExpansion = hasExpansionProperty.Value,
                    },
                ]
                : [];
    }
}
