// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Moq;
using MSTest.TestAdapter;

using TestFramework.ForTestingMSTest;

using TestPlatform.ObjectModel.Adapter;

public class MSTestExecutorTests : TestContainer
{
    private Mock<IRunContext> _mockRunContext;
    private Mock<IRunSettings> _mockRunSettings;
    private Mock<IFrameworkHandle> _mockFrameworkHandle;
    private MSTestExecutor _mstestExecutor;

    [TestInitialize]
    public void TestInit()
    {
        _mockRunContext = new Mock<IRunContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _mockFrameworkHandle = new Mock<IFrameworkHandle>();
        _mstestExecutor = new MSTestExecutor();
    }

    public void MSTestExecutorShouldProvideTestExecutionUri()
    {
        var testExecutor = new MSTestExecutor();

        var extensionUriString =
            testExecutor.GetType().GetCustomAttributes(typeof(ExtensionUriAttribute), false).Single() as
            ExtensionUriAttribute;

        Assert.AreEqual<string>(MSTest.TestAdapter.Constants.ExecutorUriString, extensionUriString.ExtensionUri);
    }

    public void RunTestsShouldNotExecuteTestsIfTestSettingsIsGiven()
    {
        var testCase = new TestCase("DummyName", new Uri("executor://MSTestAdapter/v2"), Assembly.GetExecutingAssembly().Location);
        TestCase[] tests = new[] { testCase };
        string runSettingxml =
        @"<RunSettings>   
                    <MSTest>   
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                        <ForcedLegacyMode>true</ForcedLegacyMode>    
                        <IgnoreTestImpact>true</IgnoreTestImpact>  
                    </MSTest>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _mstestExecutor.RunTests(tests, _mockRunContext.Object, _mockFrameworkHandle.Object);

        // Test should not start if TestSettings is given.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
    }

    public void RunTestsShouldReportErrorAndBailOutOnSettingsException()
    {
        var testCase = new TestCase("DummyName", new Uri("executor://MSTestAdapter/v2"), Assembly.GetExecutingAssembly().Location);
        TestCase[] tests = new[] { testCase };
        string runSettingxml =
        @"<RunSettings>   
                    <MSTest>   
                        <Parallelize>
                          <Scope>Pond</Scope>
                        </Parallelize>
                    </MSTest>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        // Act.
        _mstestExecutor.RunTests(tests, _mockRunContext.Object, _mockFrameworkHandle.Object);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public void RunTestsWithSourcesShouldNotExecuteTestsIfTestSettingsIsGiven()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingxml =
        @"<RunSettings>
                    <MSTest>   
                        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                        <ForcedLegacyMode>true</ForcedLegacyMode>    
                        <IgnoreTestImpact>true</IgnoreTestImpact>
                    </MSTest>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _mstestExecutor.RunTests(sources, _mockRunContext.Object, _mockFrameworkHandle.Object);

        // Test should not start if TestSettings is given.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
    }

    public void RunTestsWithSourcesShouldReportErrorAndBailOutOnSettingsException()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingxml =
        @"<RunSettings>   
                    <MSTest>   
                        <Parallelize>
                          <Scope>Pond</Scope>
                        </Parallelize>
                    </MSTest>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        // Act.
        _mstestExecutor.RunTests(sources, _mockRunContext.Object, _mockFrameworkHandle.Object);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public void RunTestsWithSourcesShouldSetDefaultCollectSourceInformationAsTrue()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingxml =
        @"<RunSettings>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _mstestExecutor.RunTests(sources, _mockRunContext.Object, _mockFrameworkHandle.Object);

        Verify(MSTestSettings.RunConfigurationSettings.CollectSourceInformation);
    }

    public void RunTestsWithSourcesShouldSetCollectSourceInformationAsFalseIfSpecifiedInRunSettings()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingxml =
        @"<RunSettings>
                <RunConfiguration>
                    <CollectSourceInformation>false</CollectSourceInformation>
                </RunConfiguration>
            </RunSettings>";
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _mstestExecutor.RunTests(sources, _mockRunContext.Object, _mockFrameworkHandle.Object);

        Verify(!MSTestSettings.RunConfigurationSettings.CollectSourceInformation);
    }
}
