// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public sealed class DiscoveryContextAdapterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Constructor_ComposesSettingsCommandLineAndUidFilters(bool explicitUidMode)
    {
        var options = new Mock<ICommandLineOptions>();
        string[]? arguments = ["Name=Selected"];
        options.Setup(value => value.TryGetOptionArgumentList("filter", out arguments)).Returns(true);
        var settings = new Mock<IRunSettings>();
        settings.SetupGet(value => value.SettingsXml).Returns(
            "<RunSettings><RunConfiguration><TestCaseFilter>Category=Fast</TestCaseFilter></RunConfiguration></RunSettings>");
        var filter = new TestNodeUidListFilter([new TestNodeUid("Suite.Selected")]);
        DiscoveryContextAdapter context = explicitUidMode
            ? new(options.Object, settings.Object, filter, useFullyQualifiedNameAsUid: true)
            : new(options.Object, settings.Object, filter);

        ITestCaseFilterExpression? expression = context.GetTestCaseFilter(null, _ => null);

        Assert.AreSame(settings.Object, context.RunSettings);
        Assert.IsNotNull(expression);
        Assert.AreEqual("(Category=Fast) & (Name=Selected) & (FullyQualifiedName=Suite.Selected)", expression.TestCaseFilterValue);
        var test = new TestCase("Suite.Selected", new Uri("executor://bridge"), "tests.dll");
        Assert.IsTrue(expression.MatchTestCase(test, key => key switch
        {
            "Category" => "Fast",
            "Name" => "Selected",
            "FullyQualifiedName" => "Suite.Selected",
            _ => null,
        }));
        Assert.IsFalse(expression.MatchTestCase(test, key => key switch
        {
            "Category" => "Slow",
            "Name" => "Selected",
            "FullyQualifiedName" => "Suite.Selected",
            _ => null,
        }));
    }
}
