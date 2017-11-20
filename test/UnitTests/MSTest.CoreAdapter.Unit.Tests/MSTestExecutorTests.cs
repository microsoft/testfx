// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Moq;
    using MSTest.TestAdapter;
    using MSTest.TestAdapter.ObjectModel;
    using TestPlatform.ObjectModel.Adapter;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class MSTestExecutorTests
    {
        private Mock<IRunContext> mockRunContext;
        private Mock<IRunSettings> mockRunSettings;
        private Mock<IFrameworkHandle> mockFrameworkHandle;
        private MSTestExecutor mstestExecutor;

        [TestInitialize]
        public void TestInit()
        {
            this.mockRunContext = new Mock<IRunContext>();
            this.mockRunSettings = new Mock<IRunSettings>();
            this.mockFrameworkHandle = new Mock<IFrameworkHandle>();
            this.mstestExecutor = new MSTestExecutor();
        }

        [TestMethod]
        public void MSTestExecutorShouldProvideTestExecutionUri()
        {
            var testExecutor = new MSTestExecutor();

            var extensionUriString =
                testExecutor.GetType().GetCustomAttributes(typeof(ExtensionUriAttribute), false).Single() as
                ExtensionUriAttribute;

            Assert.AreEqual<string>(MSTest.TestAdapter.Constants.ExecutorUriString, extensionUriString.ExtensionUri);
        }

        [TestMethod]
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
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            this.mstestExecutor.RunTests(tests, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            // Test should not start if TestSettings is given.
            this.mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
        }

        [TestMethod]
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
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            // Act.
            this.mstestExecutor.RunTests(tests, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            // Assert.
            this.mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
            this.mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
        }

        [TestMethod]
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
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            this.mstestExecutor.RunTests(sources, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            // Test should not start if TestSettings is given.
            this.mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethod]
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
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            // Act.
            this.mstestExecutor.RunTests(sources, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            // Assert.
            this.mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
            this.mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
        }

        [TestMethod]
        public void RunTestsWithSourcesShouldSetDefaultCollectSourceInformationAsTrue()
        {
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
            string runSettingxml =
            @"<RunSettings>
		    </RunSettings>";
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            this.mstestExecutor.RunTests(sources, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            Assert.IsTrue(MSTestSettings.RunConfigurationSettings.CollectSourceInformation);
        }

        [TestMethod]
        public void RunTestsWithSourcesShouldSetCollectSourceInformationAsFalseIfSpecifiedInRunSettings()
        {
            var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
            string runSettingxml =
            @"<RunSettings>
                <RunConfiguration>
                    <CollectSourceInformation>false</CollectSourceInformation>
                </RunConfiguration>
		    </RunSettings>";
            this.mockRunContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            this.mstestExecutor.RunTests(sources, this.mockRunContext.Object, this.mockFrameworkHandle.Object);

            Assert.IsFalse(MSTestSettings.RunConfigurationSettings.CollectSourceInformation);
        }
    }
}
