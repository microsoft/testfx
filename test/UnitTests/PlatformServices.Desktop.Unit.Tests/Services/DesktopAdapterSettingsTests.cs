// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Fakes;
    using System.Linq;
    using System.Xml;

    using Microsoft.QualityTools.Testing.Fakes;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using TestUtilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using System.Fakes;

    [TestClass]
    public class MSTestAdapterSettingsTests
    {
        #region ResolveEnvironmentVariableAndReturnFullPathIfExist tests.

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedAbsolutePath()
        {
            string path = @"C:\unitTesting\..\MsTest\Adapter";
            string baseDirectory = null;
            string expectedResult = @"C:\MsTest\Adapter";

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => { return true; };
                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWithAnEnvironmentVariable()
        {
            string path = @"%temp%\unitTesting\MsTest\Adapter";
            string baseDirectory = null;
            string expectedResult = @"C:\foo\unitTesting\MsTest\Adapter";
            
            using (ShimsContext.Create())
            {
                ShimEnvironment.ExpandEnvironmentVariablesString = ((str) => { return str.Replace("%temp%", "C:\\foo"); });
                ShimDirectory.ExistsString = (str) => { return true; };

                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithoutDot()
        {
            string path = @"MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";
            string expectedResult = @"C:\unitTesting\MsTest\Adapter";

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => { return true; };

                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithDot()
        {
            string path = @".\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";
            string expectedResult = @"C:\unitTesting\MsTest\Adapter";

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => { return true; };

                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePath()
        {
            string path = @"\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            // instead of returning "C:\unitTesting\MsTest\Adapter", it will return "(Drive from where test is running):\MsTest\Adapter",
            // because path is starting with "\"
            // this is how Path.GetFullPath works
            string currentDirectory = Environment.CurrentDirectory;
            string currentDrive = currentDirectory.Split('\\').First() + "\\";
            string expectedResult = Path.Combine(currentDrive, @"MsTest\Adapter");

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => { return true; };

                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldResolvePathWhenPassedNetworkPath()
        {
            string path = @"\\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            string expectedResult = path;

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => { return true; };

                string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

                Assert.IsNotNull(result);
                Assert.AreEqual(String.Compare(result, expectedResult, true), 0);
            }
        }

        [TestMethod]
        public void ResolveEnvironmentVariableShouldReturnFalseForInvalidPath()
        {
            string path = @"Z:\Program Files (x86)\MsTest\Adapter";
            string baseDirectory = @"C:\unitTesting";

            string result = new MSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

            Assert.IsNull(result);
        }

        #endregion

        #region GetDirectoryListWithRecursiveProperty tests.

        [TestMethod]
        public void GetDirectoryListWithRecursivePropertyShouldReadRunSettingCorrectly()
        {
            string baseDirectory = @"C:\unitTesting";
            string runSettingxml =
                @"<MSTestV2>
                    <AssemblyResolution>
                        <Directory path=""C:\\MsTest\\Adapter"" includeSubDirectories =""true"" />
                        <Directory path=""%temp%\\unitTesting\\MsTest\\Adapter"" includeSubDirectories = ""false"" />
                        <Directory path=""*MsTest\Adapter"" />
                    </AssemblyResolution>
                  </MSTestV2>";

            List<RecursiveDirectoryPath> expectedResult = new List<RecursiveDirectoryPath>();
            expectedResult.Add(new RecursiveDirectoryPath(@"C:\MsTest\Adapter", true));
            expectedResult.Add(new RecursiveDirectoryPath(@"C:\foo\unitTesting\MsTest\Adapter", false));

            using (ShimsContext.Create())
            {
                ShimEnvironment.ExpandEnvironmentVariablesString = ((str) => { return str.Replace("%temp%", "C:\\foo"); });
                ShimDirectory.ExistsString = (str) => { return true; };

                StringReader stringReader = new StringReader(runSettingxml);
                XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
                reader.Read();
                MSTestAdapterSettings sut = MSTestAdapterSettings.ToSettings(reader);

                IList<RecursiveDirectoryPath> result = sut.GetDirectoryListWithRecursiveProperty(baseDirectory);
                Assert.IsNotNull(result);
                Assert.AreEqual(result.Count, 2);

                for (int i = 0; i < 2; i++)
                {
                    Assert.AreEqual(String.Compare(result[i].DirectoryPath, expectedResult[i].DirectoryPath, StringComparison.OrdinalIgnoreCase), 0);
                    Assert.AreEqual(result[i].IncludeSubDirectories, expectedResult[i].IncludeSubDirectories);
                }
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

            Action ShouldThrowException = () => MSTestAdapterSettings.ToSettings(reader);

            ActionUtility.ActionShouldThrowExceptionOfType(ShouldThrowException, typeof(SettingsException));
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
            Assert.AreEqual(adapterSettings.DeploymentEnabled, true);

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
            Assert.AreEqual(adapterSettings.DeploymentEnabled, false);

        }

        #endregion
    }
}
