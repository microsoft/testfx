// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PlatformServices.Desktop.ComponentTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.IO;
    using System.Reflection;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DesktopTestSourceHostTests
    {
        private TestSourceHost testSourceHost;
        private string testSource;

        public DesktopTestSourceHostTests()
        {
            var currentAssemblyPath = Path.GetDirectoryName(typeof(DesktopTestSourceHostTests).Assembly.Location);
            var testAssetPath =
                Path.Combine(
                    Directory.GetParent(Directory.GetParent(Directory.GetParent(currentAssemblyPath).FullName).FullName).FullName,
                    "artifacts",
                    "TestAssets");
            this.testSource = Path.Combine(testAssetPath, "DesktopTestProjectx86Debug.dll");
        }

        [TestMethod]
        public void ParentDomainShouldHonourSearchDirectoriesSpecifiedInRunsettings()
        {
            string runSettingxml =
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>True</DisableAppDomain>   
                </RunConfiguration>  
                <MSTestV2>
                    <AssemblyResolution>
                        <Directory path = "" % Temp %\directory"" includeSubDirectories = ""true"" />
                        <Directory path = ""C:\windows"" includeSubDirectories = ""false"" />
                        <Directory path = "".\ComponentTests"" />
                    </AssemblyResolution>
                </MSTestV2>
             </RunSettings>";

            this.testSourceHost = new TestSourceHost(this.testSource, this.GetMockedIRunSettings(runSettingxml).Object, null);
            this.testSourceHost.SetupHost();

            // Loading TestProjectForAssemblyResolution.dll should not throw.
            // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
            Assembly.Load("TestProjectForAssemblyResolution");
        }

        [TestMethod]
        public void ChildDomainResolutionPathsShouldHaveSearchDirectoriesSpecifiedInRunsettings()
        {
            string runSettingxml =
            @"<RunSettings>   
                <RunConfiguration>  
                    <DisableAppDomain>False</DisableAppDomain>   
                </RunConfiguration>  
                <MSTestV2>
                    <AssemblyResolution>
                        <Directory path = "" % Temp %\directory"" includeSubDirectories = ""true"" />
                        <Directory path = ""C:\windows"" includeSubDirectories = ""false"" />
                        <Directory path = "".\ComponentTests"" />
                    </AssemblyResolution>
                </MSTestV2>
             </RunSettings>";

            this.testSourceHost = new TestSourceHost(this.testSource, this.GetMockedIRunSettings(runSettingxml).Object, null);

            this.testSourceHost.SetupHost();

            // Creating instance of TestProjectForAssemblyResolution should not throw.
            // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
            AppDomainUtilities.CreateInstance(this.testSourceHost.AppDomain, typeof(TestProjectForAssemblyResolution), null);
        }

        [TestMethod]
        public void DisposeShouldUnloadChildAppDomain()
        {
            this.testSourceHost = new TestSourceHost(this.testSource, null, null);
            this.testSourceHost.SetupHost();

            // Check that child appdomain was indeed created
            Assert.IsNotNull(this.testSourceHost.AppDomain);
            this.testSourceHost.Dispose();

            // Check that child-appdomain is now unloaded.
            Assert.IsNull(this.testSourceHost.AppDomain);
        }

        private Mock<IRunSettings> GetMockedIRunSettings(string runSettingxml)
        {
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            reader.ReadToFollowing("MSTestV2");
            mstestSettingsProvider.Load(reader);

            return mockRunSettings;
        }
    }
}
