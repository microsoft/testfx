// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
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
    public void Constructor_ExposesInProcessContextAndUsableFilter()
    {
        const string xml = "<RunSettings><RunConfiguration><ResultsDirectory>chosen-results</ResultsDirectory><TestCaseFilter>Name=Selected</TestCaseFilter></RunConfiguration></RunSettings>";
        _runSettings.SetupGet(value => value.SettingsXml).Returns(xml);
        var context = new RunContextAdapter(_commandLineOptions.Object, _runSettings.Object, new NopFilter());

        ITestCaseFilterExpression? filter = context.GetTestCaseFilter(null, _ => null);

        Assert.AreSame(_runSettings.Object, context.RunSettings);
        Assert.AreEqual("chosen-results", context.TestRunDirectory);
        Assert.IsFalse(context.KeepAlive);
        Assert.IsFalse(context.InIsolation);
        Assert.IsFalse(context.IsDataCollectionEnabled);
        Assert.AreEqual(System.Diagnostics.Debugger.IsAttached, context.IsBeingDebugged);
        Assert.IsNull(context.SolutionDirectory);
        Assert.IsNotNull(filter);
        Assert.AreEqual("(Name=Selected)", filter.TestCaseFilterValue);
        Assert.IsTrue(filter.MatchTestCase(null!, key => key == "Name" ? "Selected" : null));
        Assert.IsFalse(filter.MatchTestCase(null!, key => key == "Name" ? "Other" : null));
    }
}
