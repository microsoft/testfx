// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class RequestFilterProviderTests
{
    private sealed class MockCommandLineOptions : ICommandLineOptions
    {
        private readonly Dictionary<string, string[]> _options = [];

        public void AddOption(string key, string[] values) => _options[key] = values;

        public bool IsOptionSet(string optionName) => _options.ContainsKey(optionName);

        public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        {
            if (_options.TryGetValue(optionName, out string[]? values))
            {
                arguments = values;
                return true;
            }

            arguments = null;
            return false;
        }
    }

    [TestMethod]
    public void TestNodeUidRequestFilterProvider_CanHandle_ReturnsTrueWhenTestNodesProvided()
    {
        TestNodeUidRequestFilterProvider provider = new();
        TestNode[] testNodes = [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }];
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestExecutionRequestContext(new RunRequestArgs(Guid.NewGuid(), testNodes, null)));

        bool result = provider.CanHandle(serviceProvider);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TestNodeUidRequestFilterProvider_CanHandle_ReturnsFalseWhenTestNodesNull()
    {
        TestNodeUidRequestFilterProvider provider = new();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestExecutionRequestContext(new RunRequestArgs(Guid.NewGuid(), null, "/Some/Filter")));

        bool result = provider.CanHandle(serviceProvider);

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
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestExecutionRequestContext(new RunRequestArgs(Guid.NewGuid(), testNodes, null)));

        ITestExecutionFilter filter = await provider.CreateFilterAsync(serviceProvider);

        Assert.IsInstanceOfType<TestNodeUidListFilter>(filter);
        var uidFilter = (TestNodeUidListFilter)filter;
        Assert.HasCount(2, uidFilter.TestNodeUids);
    }

    [TestMethod]
    public void TreeNodeRequestFilterProvider_CanHandle_ReturnsTrueWhenGraphFilterProvided()
    {
        TreeNodeRequestFilterProvider provider = new();
        ServiceProvider serviceProvider = new();
        var commandLineOptions = new MockCommandLineOptions();
        commandLineOptions.AddOption(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, ["/**/Test*"]);
        serviceProvider.AddService(commandLineOptions);

        bool result = provider.CanHandle(serviceProvider);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TreeNodeRequestFilterProvider_CanHandle_ReturnsFalseWhenGraphFilterNull()
    {
        TreeNodeRequestFilterProvider provider = new();
        ServiceProvider serviceProvider = new();
        var commandLineOptions = new MockCommandLineOptions();
        serviceProvider.AddService(commandLineOptions);

        bool result = provider.CanHandle(serviceProvider);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TreeNodeRequestFilterProvider_CreateFilterAsync_CreatesTreeNodeFilter()
    {
        TreeNodeRequestFilterProvider provider = new();
        ServiceProvider serviceProvider = new();
        var commandLineOptions = new MockCommandLineOptions();
        commandLineOptions.AddOption(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, ["/**/Test*"]);
        serviceProvider.AddService(commandLineOptions);

        ITestExecutionFilter filter = await provider.CreateFilterAsync(serviceProvider);

        Assert.IsInstanceOfType<TreeNodeFilter>(filter);
    }

    [TestMethod]
    public void NopRequestFilterProvider_CanHandle_AlwaysReturnsTrue()
    {
        NopRequestFilterProvider provider = new();
        ServiceProvider serviceProvider1 = new();
        ServiceProvider serviceProvider2 = new();
        ServiceProvider serviceProvider3 = new();

        Assert.IsTrue(provider.CanHandle(serviceProvider1));
        Assert.IsTrue(provider.CanHandle(serviceProvider2));
        Assert.IsTrue(provider.CanHandle(serviceProvider3));
    }

    [TestMethod]
    public async Task NopRequestFilterProvider_CreateFilterAsync_CreatesNopFilter()
    {
        NopRequestFilterProvider provider = new();
        ServiceProvider serviceProvider = new();

        ITestExecutionFilter filter = await provider.CreateFilterAsync(serviceProvider);

        Assert.IsInstanceOfType<NopFilter>(filter);
    }

    [TestMethod]
    public void ProviderChain_TestNodeUidProvider_HasHigherPriorityThanGraphFilter()
    {
        TestNodeUidRequestFilterProvider uidProvider = new();
        TreeNodeRequestFilterProvider treeProvider = new();
        TestNode[] testNodes = [new() { Uid = new TestNodeUid("test1"), DisplayName = "Test1" }];
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestExecutionRequestContext(new RunRequestArgs(Guid.NewGuid(), testNodes, "/**/Test*")));
        var commandLineOptions = new MockCommandLineOptions();
        commandLineOptions.AddOption(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, ["/**/Test*"]);
        serviceProvider.AddService(commandLineOptions);

        Assert.IsTrue(uidProvider.CanHandle(serviceProvider), "UID provider should handle when TestNodes is provided");
        Assert.IsTrue(treeProvider.CanHandle(serviceProvider), "Tree provider should also handle when GraphFilter is provided");
    }

    [TestMethod]
    public void ProviderChain_NopProvider_ActsAsFallback()
    {
        TestNodeUidRequestFilterProvider uidProvider = new();
        TreeNodeRequestFilterProvider treeProvider = new();
        NopRequestFilterProvider nopProvider = new();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestExecutionRequestContext(new RunRequestArgs(Guid.NewGuid(), null, null)));
        var commandLineOptions = new MockCommandLineOptions();
        serviceProvider.AddService(commandLineOptions);

        Assert.IsFalse(uidProvider.CanHandle(serviceProvider), "UID provider should not handle when TestNodes is null");
        Assert.IsFalse(treeProvider.CanHandle(serviceProvider), "Tree provider should not handle when GraphFilter is null");
        Assert.IsTrue(nopProvider.CanHandle(serviceProvider), "Nop provider should always handle");
    }
}
