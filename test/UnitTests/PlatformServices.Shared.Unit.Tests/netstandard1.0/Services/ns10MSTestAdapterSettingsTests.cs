// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
#if NETCOREAPP
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using TestUtilities;

    [TestClass]
#pragma warning disable SA1649 // File name must match first type name
    public class MSTestAdapterSettingsTests

#pragma warning restore SA1649 // File name must match first type name
    {
        [TestCleanup]
        public void Cleanup()
        {
            MSTestSettingsProvider.Reset();
        }

        #region ResolveEnvironmentVariableAndReturnFullPathIfExist tests.

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedAbsolutePath()
        {
            string path = @"C:\unitTesting\..\MsTest\Adapter";
            string baseDirectory = null;
            string expectedResult = @"C:\MsTest\Adapter";

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true), 0);
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWithAnEnvironmentVariable()
        {
            string path = @"%temp%\unitTesting\MsTest\Adapter";
            string baseDirectory = null;
            string expectedResult = @"C:\foo\unitTesting\MsTest\Adapter";

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.ExpandEnvironmentVariablesSetter = (str) => { return str.Replace("%temp%", "C:\\foo"); };
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true));
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithoutDot()
        {
            string path = @"MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";
            string expectedResult = @"C:\unitTesting\MsTest\Adapter";

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true));
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithDot()
        {
            string path = @".\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";
            string expectedResult = @"C:\unitTesting\MsTest\Adapter";

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true));
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePath()
        {
            string path = @"\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            // instead of returning "C:\unitTesting\MsTest\Adapter", it will return "(Drive from where test is running):\MsTest\Adapter",
            // because path is starting with "\"
            // this is how Path.GetFullPath works
            string currentDirectory = Directory.GetCurrentDirectory();
            string currentDrive = currentDirectory.Split('\\').First() + "\\";
            string expectedResult = Path.Combine(currentDrive, @"MsTest\Adapter");

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true));
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedNetworkPath()
        {
            string path = @"\\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            string expectedResult = path;

            var adapterSettings = new TestableMSTestAdapterSettings();
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, string.Compare(result, expectedResult, true));
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldReturnFalseForInvalidPath()
        {
            string path = @"Z:\Program Files (x86)\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            string result = new TestableMSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNull(result);
        }

        #endregion

        #region GetDirectoryListWithRecursiveProperty tests.

        [TestMethod]
        public void GetDirectoryListWithRecursivePropertyShouldReadRunSettingCorrectly()
        {
            string baseDirectory = @"C:\unitTesting";

            List<RecursiveDirectoryPath> expectedResult = new List<RecursiveDirectoryPath>();
            expectedResult.Add(new RecursiveDirectoryPath(@"C:\MsTest\Adapter", true));
            expectedResult.Add(new RecursiveDirectoryPath(@"C:\foo\unitTesting\MsTest\Adapter", false));

            var adapterSettings = new TestableMSTestAdapterSettings(expectedResult);
            adapterSettings.ExpandEnvironmentVariablesSetter = (str) => { return str.Replace("%temp%", "C:\\foo"); };
            adapterSettings.DoesDirectoryExistSetter = (str) => { return true; };

            IList<RecursiveDirectoryPath> result = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(0, string.Compare(result[i].DirectoryPath, expectedResult[i].DirectoryPath, StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual(result[i].IncludeSubDirectories, expectedResult[i].IncludeSubDirectories);
            }
        }

        #endregion

        #region ToSettings tests.

        [TestMethod]
        public void ToSettingsShouldNotThrowExceptionWhenRunSettingsXmlUnderTagMSTestv2IsWrong()
        {
            string runSettingxml =
                  @"<MSTestV2>
                    <IgnoreTestImpact>true</IgnoreTestImpact>
                    <AssemblyResolutionBug>
                        <Directory  path=""C:\\MsTest\\Adapter"" includeSubDirectories =""true"" />
                        <Directory  path=""%temp%\\unitTesting\\MsTest\\Adapter"" includeSubDirectories = ""false"" />
                        <Directory path=""*MsTest\Adapter"" />
                    </AssemblyResolutionBug>
                    <InProcMode>true</InProcMode>
                    <CleanUpCommunicationChannels>false</CleanUpCommunicationChannels>
                  </MSTestV2>";

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();

            MSTestAdapterSettings.ToSettings(reader);
        }

        [TestMethod]
        public void ToSettingsShouldThrowExceptionWhenRunSettingsXmlIsWrong()
        {
            string runSettingxml =
                  @"<MSTestV2>
                    <AssemblyResolution>
                        <DirectoryBug  path=""C:\\MsTest\\Adapter"" includeSubDirectories =""true"" />
                        <Directory  path=""%temp%\\unitTesting\\MsTest\\Adapter"" includeSubDirectories = ""false"" />
                        <Directory path=""*MsTest\Adapter"" />
                    </AssemblyResolution>
                  </MSTestV2>";

            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();

            Action shouldThrowException = () => MSTestAdapterSettings.ToSettings(reader);

            ActionUtility.ActionShouldThrowExceptionOfType(shouldThrowException, typeof(SettingsException));
        }

        #endregion

        #region DeploymentEnabled tests.

        [TestMethod]
        public void DeploymentEnabledIsByDefaultTrueWhenNotSpecified()
        {
            string runSettingxml =
                @"<MSTestV2>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
            Assert.IsTrue(adapterSettings.DeploymentEnabled);
        }

        [TestMethod]
        public void DeploymentEnabledShouldBeConsumedFromRunSettingsWhenSpecified()
        {
            string runSettingxml =
                @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
            Assert.IsFalse(adapterSettings.DeploymentEnabled);
        }

        #endregion

        #region DeployTestSourceDependencies tests

        [TestMethod]
        public void DeployTestSourceDependenciesIsEnabledByDefault()
        {
            string runSettingxml =
                @"<MSTestV2>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
            Assert.IsTrue(adapterSettings.DeployTestSourceDependencies);
        }

        [TestMethod]
        public void DeployTestSourceDependenciesWhenFalse()
        {
            string runSettingxml =
                @"<MSTestV2>
                     <DeployTestSourceDependencies>False</DeployTestSourceDependencies>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
            Assert.IsFalse(adapterSettings.DeployTestSourceDependencies);
        }

        [TestMethod]
        public void DeployTestSourceDependenciesWhenTrue()
        {
            string runSettingxml =
                @"<MSTestV2>
                     <DeployTestSourceDependencies>True</DeployTestSourceDependencies>
                  </MSTestV2>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            reader.Read();
            MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
            Assert.IsTrue(adapterSettings.DeployTestSourceDependencies);
        }

        #endregion
    }

    public class TestableMSTestAdapterSettings : MSTestAdapterSettings
    {
        public TestableMSTestAdapterSettings()
        {
        }

        public TestableMSTestAdapterSettings(List<RecursiveDirectoryPath> expectedResult)
        {
            this.SearchDirectories.AddRange(expectedResult);
        }

        public Func<string, bool> DoesDirectoryExistSetter { get; set; }

        public Func<string, string> ExpandEnvironmentVariablesSetter { get; set; }

        protected override bool DoesDirectoryExist(string path)
        {
            if (this.DoesDirectoryExistSetter == null)
            {
                return base.DoesDirectoryExist(path);
            }

            return this.DoesDirectoryExistSetter(path);
        }

        protected override string ExpandEnvironmentVariables(string path)
        {
            if (this.ExpandEnvironmentVariablesSetter == null)
            {
                return base.ExpandEnvironmentVariables(path);
            }

            return this.ExpandEnvironmentVariablesSetter(path);
        }
    }
}
