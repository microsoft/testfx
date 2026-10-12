// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Requests;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConsoleTestExecutionFilterFactoryTests
{
    [TestMethod]
    public async Task TryCreateAsync_WhenNeitherOptionIsSet_ReturnsNopFilter()
    {
        ConsoleTestExecutionFilterFactory factory = new(new Mock<ICommandLineOptions>().Object);

        (bool success, ITestExecutionFilter? filter) = await factory.TryCreateAsync();

        Assert.IsTrue(success);
        Assert.IsInstanceOfType<NopFilter>(filter);
    }

    [TestMethod]
    public async Task TryCreateAsync_WhenOnlyTreeNodeFilterIsSet_ReturnsTreeNodeFilterWithGivenPattern()
    {
        const string Pattern = "/Assembly/Namespace/Class/Method";
        string[]? patterns = [Pattern];
        Mock<ICommandLineOptions> options = new();
        options.Setup(o => o.TryGetOptionArgumentList(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out patterns))
            .Returns(true);
        ConsoleTestExecutionFilterFactory factory = new(options.Object);

        (bool success, ITestExecutionFilter? filter) = await factory.TryCreateAsync();

        Assert.IsTrue(success);
        TreeNodeFilter treeFilter = Assert.IsInstanceOfType<TreeNodeFilter>(filter);
        Assert.AreEqual(Pattern, treeFilter.Filter);
    }

    [TestMethod]
    public async Task TryCreateAsync_WhenOnlyFilterUidIsSet_ReturnsUidFilterWithGivenUidsInOrder()
    {
        string[]? uids = ["second", "first", "third"];
        Mock<ICommandLineOptions> options = new();
        options.Setup(o => o.TryGetOptionArgumentList(PlatformCommandLineProvider.FilterUidOptionKey, out uids))
            .Returns(true);
        ConsoleTestExecutionFilterFactory factory = new(options.Object);

        (bool success, ITestExecutionFilter? filter) = await factory.TryCreateAsync();

        Assert.IsTrue(success);
        TestNodeUidListFilter uidFilter = Assert.IsInstanceOfType<TestNodeUidListFilter>(filter);
        Assert.HasCount(3, uidFilter.TestNodeUids);
        Assert.AreEqual("second", uidFilter.TestNodeUids[0].Value);
        Assert.AreEqual("first", uidFilter.TestNodeUids[1].Value);
        Assert.AreEqual("third", uidFilter.TestNodeUids[2].Value);
    }

    [TestMethod]
    public async Task TryCreateAsync_WhenBothOptionsAreSet_ThrowsUnreachableException()
    {
        string[]? patterns = ["/Assembly/Namespace/Class/Method"];
        string[]? uids = ["first"];
        Mock<ICommandLineOptions> options = new();
        options.Setup(o => o.TryGetOptionArgumentList(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter, out patterns))
            .Returns(true);
        options.Setup(o => o.TryGetOptionArgumentList(PlatformCommandLineProvider.FilterUidOptionKey, out uids))
            .Returns(true);
        ConsoleTestExecutionFilterFactory factory = new(options.Object);

        Exception exception = await Assert.ThrowsAsync<Exception>(() => factory.TryCreateAsync());

        Assert.AreEqual(typeof(global::System.Diagnostics.UnreachableException).FullName, exception.GetType().FullName);
    }
}
