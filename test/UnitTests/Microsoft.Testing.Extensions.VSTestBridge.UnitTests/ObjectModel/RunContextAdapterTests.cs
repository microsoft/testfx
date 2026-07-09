// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public class RunContextAdapterTests
{
    private readonly Mock<ICommandLineOptions> _commandLineOptions = new();
    private readonly Mock<IRunSettings> _runSettings = new();

    [TestMethod]
    public void TestRunDirectory_IsNotNull_If_ResultsDirectory_Is_Provided()
    {
        string runSettings =
"""
        <RunSettings>
            <RunConfiguration>
                <ResultsDirectory>/PlatformResultDirectoryFromFile</ResultsDirectory>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());
        Assert.AreEqual("/PlatformResultDirectoryFromFile", runContextAdapter.TestRunDirectory);
        Assert.AreEqual(runSettings, runContextAdapter.RunSettings!.SettingsXml);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }

    [TestMethod]
    public void TestRunDirectory_IsNull_If_ResultsDirectory_IsNot_Provided()
    {
        string runSettings =
"""
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());
        Assert.IsNull(runContextAdapter.TestRunDirectory);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }

    [TestMethod]
    public void BuildFilter_WhenUsingFullyQualifiedNameAsUid_GuidShapedName_EmitsFullyQualifiedNameClause()
    {
        _runSettings.Setup(x => x.SettingsXml).Returns("<RunSettings></RunSettings>");

        // A data-driven test whose FullyQualifiedName happens to be exactly a GUID-shaped string.
        const string GuidShapedFullyQualifiedName = "12345678-1234-1234-1234-1234567890ab";
        var filter = new TestNodeUidListFilter([new TestNodeUid(GuidShapedFullyQualifiedName)]);

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, filter, useFullyQualifiedNameAsUid: true);

        ITestCaseFilterExpression? filterExpression = runContextAdapter.GetTestCaseFilter(null, _ => null);
        Assert.IsNotNull(filterExpression);
        Assert.AreEqual($"(FullyQualifiedName={GuidShapedFullyQualifiedName})", filterExpression.TestCaseFilterValue);
    }

    [TestMethod]
    public void BuildFilter_WhenNotUsingFullyQualifiedNameAsUid_GuidValue_EmitsIdClause()
    {
        _runSettings.Setup(x => x.SettingsXml).Returns("<RunSettings></RunSettings>");

        const string GuidValue = "12345678-1234-1234-1234-1234567890ab";
        var filter = new TestNodeUidListFilter([new TestNodeUid(GuidValue)]);

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, filter, useFullyQualifiedNameAsUid: false);

        ITestCaseFilterExpression? filterExpression = runContextAdapter.GetTestCaseFilter(null, _ => null);
        Assert.IsNotNull(filterExpression);
        Assert.AreEqual($"(Id={GuidValue})", filterExpression.TestCaseFilterValue);
    }

    [TestMethod]
    public void BuildFilter_WhenUsingFullyQualifiedNameAsUid_NonGuidName_EmitsFullyQualifiedNameClause()
    {
        _runSettings.Setup(x => x.SettingsXml).Returns("<RunSettings></RunSettings>");

        const string FullyQualifiedName = "MyNamespace.MyClass.MyTest";
        var filter = new TestNodeUidListFilter([new TestNodeUid(FullyQualifiedName)]);

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, filter, useFullyQualifiedNameAsUid: true);

        ITestCaseFilterExpression? filterExpression = runContextAdapter.GetTestCaseFilter(null, _ => null);
        Assert.IsNotNull(filterExpression);
        Assert.AreEqual($"(FullyQualifiedName={FullyQualifiedName})", filterExpression.TestCaseFilterValue);
    }
}
