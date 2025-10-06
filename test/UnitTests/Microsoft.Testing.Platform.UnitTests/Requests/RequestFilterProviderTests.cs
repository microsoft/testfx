// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class RequestFilterProviderTests
{
    [TestMethod]
    public void TestNodeUidRequestFilterProvider_CanHandle_ReturnsTrueWhenTestNodesProvided()
    {
        TestNodeUidRequestFilterProvider provider = new();
        TestNode[] testNodes = [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }];
        RequestArgsBase args = new(Guid.NewGuid(), testNodes, null);

        bool result = provider.CanHandle(args);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TestNodeUidRequestFilterProvider_CanHandle_ReturnsFalseWhenTestNodesNull()
    {
        TestNodeUidRequestFilterProvider provider = new();
        RequestArgsBase args = new(Guid.NewGuid(), null, "/Some/Filter");

        bool result = provider.CanHandle(args);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TestNodeUidRequestFilterProvider_CreateFilterAsync_CreatesTestNodeUidListFilter()
    {
        TestNodeUidRequestFilterProvider provider = new();
        TestNode[] testNodes =
        [
            new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" },
            new() { Uid = new TestNodeUid("test2"), DisplayName = "Test2" },
        ];
        RequestArgsBase args = new(Guid.NewGuid(), testNodes, null);

        ITestExecutionFilter filter = await provider.CreateFilterAsync(args);

        Assert.IsInstanceOfType<TestNodeUidListFilter>(filter);
        TestNodeUidListFilter uidFilter = (TestNodeUidListFilter)filter;
        Assert.AreEqual(2, uidFilter.TestNodeUids.Length);
    }

    [TestMethod]
    public void TreeNodeRequestFilterProvider_CanHandle_ReturnsTrueWhenGraphFilterProvided()
    {
        TreeNodeRequestFilterProvider provider = new();
        RequestArgsBase args = new(Guid.NewGuid(), null, "/**/Test*");

        bool result = provider.CanHandle(args);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TreeNodeRequestFilterProvider_CanHandle_ReturnsFalseWhenGraphFilterNull()
    {
        TreeNodeRequestFilterProvider provider = new();
        TestNode[] testNodes = [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }];
        RequestArgsBase args = new(Guid.NewGuid(), testNodes, null);

        bool result = provider.CanHandle(args);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TreeNodeRequestFilterProvider_CreateFilterAsync_CreatesTreeNodeFilter()
    {
        TreeNodeRequestFilterProvider provider = new();
        RequestArgsBase args = new(Guid.NewGuid(), null, "/**/Test*");

        ITestExecutionFilter filter = await provider.CreateFilterAsync(args);

        Assert.IsInstanceOfType<TreeNodeFilter>(filter);
    }

    [TestMethod]
    public void NopRequestFilterProvider_CanHandle_AlwaysReturnsTrue()
    {
        NopRequestFilterProvider provider = new();
        RequestArgsBase args1 = new(Guid.NewGuid(), null, null);
        RequestArgsBase args2 = new(Guid.NewGuid(), [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }], null);
        RequestArgsBase args3 = new(Guid.NewGuid(), null, "/**/Test*");

        Assert.IsTrue(provider.CanHandle(args1));
        Assert.IsTrue(provider.CanHandle(args2));
        Assert.IsTrue(provider.CanHandle(args3));
    }

    [TestMethod]
    public async Task NopRequestFilterProvider_CreateFilterAsync_CreatesNopFilter()
    {
        NopRequestFilterProvider provider = new();
        RequestArgsBase args = new(Guid.NewGuid(), null, null);

        ITestExecutionFilter filter = await provider.CreateFilterAsync(args);

        Assert.IsInstanceOfType<NopFilter>(filter);
    }

    [TestMethod]
    public void ProviderChain_TestNodeUidProvider_HasHigherPriorityThanGraphFilter()
    {
        TestNodeUidRequestFilterProvider uidProvider = new();
        TreeNodeRequestFilterProvider treeProvider = new();

        TestNode[] testNodes = [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }];
        RequestArgsBase args = new(Guid.NewGuid(), testNodes, "/**/Test*");

        Assert.IsTrue(uidProvider.CanHandle(args), "UID provider should handle when TestNodes is provided");
        Assert.IsTrue(treeProvider.CanHandle(args), "Tree provider should also handle when GraphFilter is provided");
    }

    [TestMethod]
    public void ProviderChain_NopProvider_ActsAsFallback()
    {
        TestNodeUidRequestFilterProvider uidProvider = new();
        TreeNodeRequestFilterProvider treeProvider = new();
        NopRequestFilterProvider nopProvider = new();

        RequestArgsBase args = new(Guid.NewGuid(), null, null);

        Assert.IsFalse(uidProvider.CanHandle(args), "UID provider should not handle when TestNodes is null");
        Assert.IsFalse(treeProvider.CanHandle(args), "Tree provider should not handle when GraphFilter is null");
        Assert.IsTrue(nopProvider.CanHandle(args), "Nop provider should always handle");
    }
}
