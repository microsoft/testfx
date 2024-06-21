// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestGroup]
public class RunContextAdapterTests : TestBase
{
    private readonly Mock<ICommandLineOptions> _commandLineOptions = new();
    private readonly Mock<IRunSettings> _runSettings = new();

    public RunContextAdapterTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void TestRunDirectory_IsNotNull_If_ResultsDirectory_Is_Provided()
    {
        string runSettings =
$"""
        <RunSettings>
            <RunConfiguration>
                <ResultsDirectory>/PlatformResultDirectoryFromFile</ResultsDirectory>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object);
        Assert.AreEqual("/PlatformResultDirectoryFromFile", runContextAdapter.TestRunDirectory);
        Assert.AreEqual(runSettings, runContextAdapter.RunSettings!.SettingsXml);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }

    public void TestRunDirectory_IsNull_If_ResultsDirectory_IsNot_Provided()
    {
        string runSettings =
$"""
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object);
        Assert.IsNull(runContextAdapter.TestRunDirectory);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }
}
