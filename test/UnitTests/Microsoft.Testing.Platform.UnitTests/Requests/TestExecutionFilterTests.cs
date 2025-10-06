// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestExecutionFilterTests
{
    [TestMethod]
    public void NopFilter_Matches_ReturnsTrue()
    {
        // Arrange
        NopFilter filter = new();
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "TestMethod1",
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TestNodeUidListFilter_Matches_ReturnsTrueWhenUidInList()
    {
        // Arrange
        TestNodeUid uid1 = new("test1");
        TestNodeUid uid2 = new("test2");
        TestNodeUidListFilter filter = new([uid1, uid2]);
        TestNode testNode = new()
        {
            Uid = uid1,
            DisplayName = "TestMethod1",
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TestNodeUidListFilter_Matches_ReturnsFalseWhenUidNotInList()
    {
        // Arrange
        TestNodeUid uid1 = new("test1");
        TestNodeUid uid2 = new("test2");
        TestNodeUid uid3 = new("test3");
        TestNodeUidListFilter filter = new([uid1, uid2]);
        TestNode testNode = new()
        {
            Uid = uid3,
            DisplayName = "TestMethod3",
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TreeNodeFilter_Matches_UsesTestMethodIdentifierProperty()
    {
        // Arrange
        TreeNodeFilter filter = new("/*MyNamespace*/**");
        TestMethodIdentifierProperty methodIdentifier = new(
            assemblyFullName: "MyAssembly",
            @namespace: "MyNamespace.SubNamespace",
            typeName: "MyTestClass",
            methodName: "MyTestMethod",
            methodArity: 0,
            parameterTypeFullNames: [],
            returnTypeFullName: "System.Void");

        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(methodIdentifier),
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TreeNodeFilter_Matches_FallsBackToDisplayNameWhenNoMethodIdentifier()
    {
        // Arrange
        TreeNodeFilter filter = new("/*MyTest*");
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TreeNodeFilter_Matches_MatchesNamespaceTypeMethod()
    {
        // Arrange
        TreeNodeFilter filter = new("/MyNamespace.SubNamespace/MyTestClass/MyTestMethod");
        TestMethodIdentifierProperty methodIdentifier = new(
            assemblyFullName: "MyAssembly",
            @namespace: "MyNamespace.SubNamespace",
            typeName: "MyTestClass",
            methodName: "MyTestMethod",
            methodArity: 0,
            parameterTypeFullNames: [],
            returnTypeFullName: "System.Void");

        TestNode testNode = new()
        {
            Uid = new TestNodeUid("test1"),
            DisplayName = "MyTestMethod",
            Properties = new PropertyBag(methodIdentifier),
        };

        // Act
        bool result = filter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void AggregateTestExecutionFilter_Matches_ReturnsTrueWhenAllFiltersMatch()
    {
        // Arrange
        TestNodeUid uid1 = new("test1");
        TestNodeUidListFilter uidFilter = new([uid1]);
        TreeNodeFilter treeFilter = new("/**");

        AggregateTestExecutionFilter aggregateFilter = new([uidFilter, treeFilter]);

        TestNode testNode = new()
        {
            Uid = uid1,
            DisplayName = "TestMethod1",
        };

        // Act
        bool result = aggregateFilter.Matches(testNode);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void AggregateTestExecutionFilter_Matches_ReturnsFalseWhenAnyFilterDoesNotMatch()
    {
        // Arrange
        TestNodeUid uid1 = new("test1");
        TestNodeUid uid2 = new("test2");
        TestNodeUidListFilter uidFilter = new([uid1]); // Only matches uid1
        TreeNodeFilter treeFilter = new("/**"); // Matches everything

        AggregateTestExecutionFilter aggregateFilter = new([uidFilter, treeFilter]);

        TestNode testNode = new()
        {
            Uid = uid2, // Different UID
            DisplayName = "TestMethod2",
        };

        // Act
        bool result = aggregateFilter.Matches(testNode);

        // Assert
        Assert.IsFalse(result); // Should be false because uidFilter doesn't match
    }

    [TestMethod]
    public void AggregateTestExecutionFilter_Constructor_ThrowsWhenNoFiltersProvided()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => new AggregateTestExecutionFilter([]));
    }

    [TestMethod]
    public void AggregateTestExecutionFilter_InnerFilters_ReturnsProvidedFilters()
    {
        // Arrange
        TestNodeUidListFilter uidFilter = new([new TestNodeUid("test1")]);
        TreeNodeFilter treeFilter = new("/**");
        List<ITestExecutionFilter> filters = [uidFilter, treeFilter];

        // Act
        AggregateTestExecutionFilter aggregateFilter = new(filters);

        // Assert
        Assert.AreEqual(2, aggregateFilter.InnerFilters.Count);
        Assert.IsTrue(aggregateFilter.InnerFilters.Contains(uidFilter));
        Assert.IsTrue(aggregateFilter.InnerFilters.Contains(treeFilter));
    }

    [TestMethod]
    public void AggregateTestExecutionFilter_Matches_ANDLogic_AllMustMatch()
    {
        // Arrange
        TestNodeUid targetUid = new("test1");

        // Filter 1: Only matches "test1" UID
        TestNodeUidListFilter uidFilter = new([targetUid]);

        // Filter 2: Only matches names starting with "Test"
        TreeNodeFilter nameFilter = new("/Test*");

        AggregateTestExecutionFilter aggregateFilter = new([uidFilter, nameFilter]);

        // Test case 1: Matches both filters
        TestNode matchingNode = new()
        {
            Uid = targetUid,
            DisplayName = "TestMethod1",
        };

        // Test case 2: Matches UID but not name
        TestNode wrongNameNode = new()
        {
            Uid = targetUid,
            DisplayName = "MyMethod",
        };

        // Test case 3: Matches name but not UID
        TestNode wrongUidNode = new()
        {
            Uid = new TestNodeUid("test2"),
            DisplayName = "TestMethod2",
        };

        // Act & Assert
        Assert.IsTrue(aggregateFilter.Matches(matchingNode), "Should match when both filters match");
        Assert.IsFalse(aggregateFilter.Matches(wrongNameNode), "Should not match when name filter fails");
        Assert.IsFalse(aggregateFilter.Matches(wrongUidNode), "Should not match when UID filter fails");
    }
}
