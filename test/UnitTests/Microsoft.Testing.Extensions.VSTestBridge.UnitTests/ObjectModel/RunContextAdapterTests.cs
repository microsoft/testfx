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
    private readonly Mock<ICommandLineOptions> _commandLineOptions = new(MockBehavior.Loose);
    private readonly Mock<IRunSettings> _runSettings = new(MockBehavior.Loose);

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
}
