// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using Moq;

    using Utilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestFrameworkV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using Ignore = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute;

    [TestClass]
    public class DesktopTestDeploymentTests
    {
        private Mock<ReflectionUtility> mockReflectionUtility;
        private Mock<FileUtility> mockFileUtility;

        private IList<string> warnings;

        private const string DefaultDeploymentItemPath = @"c:\temp";
        private const string DefaultDeploymentItemOutputDirectory = "out";

        [TestInitialize]
        public void TestInit()
        {
            this.mockReflectionUtility = new Mock<ReflectionUtility>();
            this.mockFileUtility = new Mock<FileUtility>();
            this.warnings = new List<string>();
            
            // Reset adapter settings.
            MSTestSettingsProvider.Reset();
        }

        #region GetDeploymentItems tests.

        [TestMethod]
        public void GetDeploymentItemsReturnsNullWhenNoDeploymentItems()
        {
            var methodInfo =
                typeof(DesktopTestDeploymentTests).GetMethod("GetDeploymentItemsReturnsNullWhenNoDeploymentItems");

            Assert.IsNull(new TestDeployment().GetDeploymentItems(methodInfo, typeof(DesktopTestDeploymentTests), this.warnings));
        }

        [TestMethod]
        public void GetDeploymentItemsReturnsDeploymentItems()
        {
            // Arrange.
            var testDeployment = new TestDeployment(new DeploymentItemUtility(this.mockReflectionUtility.Object), null, null);

            // setup mocks
            var methodLevelDeploymentItems = new[]
                                                  {
                                                   new KeyValuePair<string, string>(
                                                       DefaultDeploymentItemPath,
                                                       DefaultDeploymentItemOutputDirectory)
                                               };
            var classLevelDeploymentItems = new[]
                                                  {
                                                   new KeyValuePair<string, string>(
                                                       DefaultDeploymentItemPath + "\\temp2",
                                                       DefaultDeploymentItemOutputDirectory)
                                               };
            var memberInfo =
                typeof(DesktopTestDeploymentTests).GetMethod(
                    "GetDeploymentItemsReturnsDeploymentItems");
            this.SetupDeploymentItems(memberInfo, methodLevelDeploymentItems);
            this.SetupDeploymentItems(typeof(DesktopTestDeploymentTests), classLevelDeploymentItems);

            // Act.
            var deploymentItems = testDeployment.GetDeploymentItems(memberInfo, typeof(DesktopTestDeploymentTests), this.warnings);

            // Assert.
            var expectedDeploymentItems = new KeyValuePair<string, string>[]
                                              {
                                                  new KeyValuePair<string, string>(
                                                      DefaultDeploymentItemPath,
                                                      DefaultDeploymentItemOutputDirectory),
                                                  new KeyValuePair<string, string>(
                                                      DefaultDeploymentItemPath + "\\temp2",
                                                      DefaultDeploymentItemOutputDirectory)
                                              };

            CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
        }

        #endregion

        #region Cleanup tests

        [TestMethod]
        public void CleanupShouldNotDeleteDirectoriesIfRunDirectoiresIsNull()
        {
            var testDeployment = new TestDeployment(null, null, this.mockFileUtility.Object);

            testDeployment.Cleanup();

            this.mockFileUtility.Verify(fu => fu.DeleteDirectories(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void CleanupShouldNotDeleteDirectoriesIfRunSettingsSpecifiesSo()
        {

            string runSettingxml =
                @"<DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            mstestSettingsProvider.Load(reader);
            
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            testDeployment.Cleanup();

            this.mockFileUtility.Verify(fu => fu.DeleteDirectories(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void CleanupShouldDeleteRootDeploymentDirectory()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            // Act.
            testDeployment.Cleanup();

            this.mockFileUtility.Verify(fu => fu.DeleteDirectories(testRunDirectories.RootDeploymentDirectory), Times.Once);
        }

        #endregion

        #region GetDeploymentDirectory tests

        [TestMethod]
        public void GetDeploymentDirectoryShouldReturnNullIfDeploymentDirectoryIsNull()
        {
            Assert.IsNull(new TestDeployment().GetDeploymentDirectory());
        }

        [TestMethod]
        public void GetDeploymentDirectoryShouldReturnDeploymentOutputDirectory()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));


            // Act.
            Assert.AreEqual(testRunDirectories.OutDirectory, testDeployment.GetDeploymentDirectory());
        }

        #endregion

        #region Deploy tests

        [TestMethod]
        public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseButHasDeploymentItems()
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
            testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, new[]
                    {
                        new KeyValuePair<string, string>(
                            DefaultDeploymentItemPath,
                            DefaultDeploymentItemOutputDirectory)
                    });

            var testDeployment = new TestDeployment(
                new DeploymentItemUtility(this.mockReflectionUtility.Object),
                new DeploymentUtility(),
                this.mockFileUtility.Object);

            string runSettingxml =
                 @"<DeploymentEnabled>False</DeploymentEnabled>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            mstestSettingsProvider.Load(reader);

            //Deployment should not happen
            Assert.IsFalse(testDeployment.Deploy(new List<TestCase> { testCase }, null, null));
            //Deplyment directories should not be created
            Assert.IsNull(testDeployment.GetDeploymentDirectory());
        }

        [TestMethod]
        public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseAndHasNoDeploymentItems()
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
            testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
            var testDeployment = new TestDeployment(
                new DeploymentItemUtility(this.mockReflectionUtility.Object),
                new DeploymentUtility(),
                this.mockFileUtility.Object);

            string runSettingxml =
                @"<DeploymentEnabled>False</DeploymentEnabled>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            mstestSettingsProvider.Load(reader);

            //Deployment should not happen
            Assert.IsFalse(testDeployment.Deploy(new List<TestCase> { testCase }, null, null));
            //Deployment directories should get created
            Assert.IsNotNull(testDeployment.GetDeploymentDirectory());
        }

        [TestMethod]
        public void DeployShouldReturnFalseWhenDeploymentEnabledSetToTrueButHasNoDeploymentItems()
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
            testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
            var testDeployment = new TestDeployment(
                new DeploymentItemUtility(this.mockReflectionUtility.Object),
                new DeploymentUtility(),
                this.mockFileUtility.Object);

            string runSettingxml =
                @"<DeploymentEnabled>True</DeploymentEnabled>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            mstestSettingsProvider.Load(reader);

            //Deployment should not happen
            Assert.IsFalse(testDeployment.Deploy(new List<TestCase> { testCase }, null, null));
            //Deployment directories should get created
            Assert.IsNotNull(testDeployment.GetDeploymentDirectory());
        }
        
        //[Todo] This test has to have mocks. It actually deploys stuff and we cannot assume that all the dependencies get copied over to bin\debug.
        [TestMethod]
        [Ignore]
        public void DeployShouldReturnTrueWhenDeploymentEnabledSetToTrueAndHasDeploymentItems()
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
            testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, new[]
                    {
                        new KeyValuePair<string, string>(
                            DefaultDeploymentItemPath,
                            DefaultDeploymentItemOutputDirectory)
                    });
            var testDeployment = new TestDeployment(
                new DeploymentItemUtility(this.mockReflectionUtility.Object),
                new DeploymentUtility(),
                this.mockFileUtility.Object);

            string runSettingxml =
                @"<DeploymentEnabled>True</DeploymentEnabled>";
            StringReader stringReader = new StringReader(runSettingxml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            MSTestSettingsProvider mstestSettingsProvider = new MSTestSettingsProvider();
            mstestSettingsProvider.Load(reader);

            //Deployment should happen
            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, null, new Mock<IFrameworkHandle>().Object));

            //Deployment directories should get created
            Assert.IsNotNull(testDeployment.GetDeploymentDirectory());
        }

        [TestMethod]
        public void DeployShouldCreateDeploymentDirectories()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);
            
            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));


            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(testRunDirectories.RootDeploymentDirectory), Times.Once);
        }

        [TestMethod]
        public void DeployShouldDeployFilesInASourceAndReturnTrue()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            string warning;
            var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(sourceFile)),
                    Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                    out warning),
                Times.Once);
        }

        [TestMethod]
        public void DeployShouldDeployFilesInMultipleSourcesAndReturnTrue()
        {
            TestRunDirectories testRunDirectories;
            var testCase1 = this.GetTestCase(Assembly.GetExecutingAssembly().Location);
            var sourceFile2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "a.dll");
            var testCase2 = this.GetTestCase(sourceFile2);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase1, testCase2 }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            string warning;
            var sourceFile1 = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(sourceFile1)),
                    Path.Combine(testRunDirectories.OutDirectory, sourceFile1),
                    out warning),
                Times.Once);
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains("a.dll")),
                    Path.Combine(testRunDirectories.OutDirectory, "a.dll"),
                    out warning),
                Times.Once);
        }

        #endregion

        #region GetDeploymentInformation tests

        [TestMethod]
        public void GetDeploymentInformationShouldReturnAppBaseDirectoryIfRunDirectoryIsNull()
        {
            TestDeployment.Reset();
            var properties = TestDeployment.GetDeploymentInformation(Assembly.GetExecutingAssembly().Location);
            
            var applicationBaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var expectedProperties = new Dictionary<string, object>
                                         {
                                             {
                                                 TestContextPropertyStrings.TestRunDirectory,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.DeploymentDirectory,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.ResultsDirectory,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestRunResultsDirectory,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestResultsDirectory,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDir,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDeploymentDir,
                                                 applicationBaseDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestLogsDir,
                                                 applicationBaseDirectory
                                             }
                                         };
            Assert.IsNotNull(properties);
            CollectionAssert.AreEqual(expectedProperties.ToList(), properties.ToList());
        }

        [TestMethod]
        public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNull()
        {
            // Arrange.
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            // Act.
            var properties = TestDeployment.GetDeploymentInformation(null);

            // Assert.
            var expectedProperties = new Dictionary<string, object>
                                         {
                                             {
                                                 TestContextPropertyStrings.TestRunDirectory,
                                                 testRunDirectories.RootDeploymentDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.DeploymentDirectory,
                                                 testRunDirectories.OutDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.ResultsDirectory,
                                                 testRunDirectories.InDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestRunResultsDirectory,
                                                 testRunDirectories.InMachineNameDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestResultsDirectory,
                                                 testRunDirectories.InDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDir,
                                                 testRunDirectories.RootDeploymentDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDeploymentDir,
                                                 testRunDirectories.OutDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestLogsDir,
                                                 testRunDirectories.InMachineNameDirectory
                                             }
                                         };

            Assert.IsNotNull(properties);
            CollectionAssert.AreEqual(expectedProperties.ToList(), properties.ToList());
        }

        [TestMethod]
        public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNotNull()
        {
            // Arrange.
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCase(Assembly.GetExecutingAssembly().Location);

            // Setup mocks.
            var testDeployment = this.CreateAndSetupDeploymentRelatedUtilities(out testRunDirectories);

            var mockRunContext = new Mock<IRunContext>();
            mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

            Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

            // Act.
            var properties = TestDeployment.GetDeploymentInformation(Assembly.GetExecutingAssembly().Location);

            // Assert.
            var expectedProperties = new Dictionary<string, object>
                                         {
                                             {
                                                 TestContextPropertyStrings.TestRunDirectory,
                                                 testRunDirectories.RootDeploymentDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.DeploymentDirectory,
                                                 testRunDirectories.OutDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.ResultsDirectory,
                                                 testRunDirectories.InDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestRunResultsDirectory,
                                                 testRunDirectories.InMachineNameDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings
                                                 .TestResultsDirectory,
                                                 testRunDirectories.InDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDir,
                                                 testRunDirectories.RootDeploymentDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestDeploymentDir,
                                                 testRunDirectories.OutDirectory
                                             },
                                             {
                                                 TestContextPropertyStrings.TestLogsDir,
                                                 testRunDirectories.InMachineNameDirectory
                                             }
                                         };

            Assert.IsNotNull(properties);
            CollectionAssert.AreEqual(expectedProperties.ToList(), properties.ToList());
        }

        #endregion

        #region private methods

        private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
        {
            var deploymentItemAttributes = new List<TestFrameworkV2.DeploymentItemAttribute>();

            foreach (var deploymentItem in deploymentItems)
            {
                deploymentItemAttributes.Add(new TestFrameworkV2.DeploymentItemAttribute(deploymentItem.Key, deploymentItem.Value));
            }

            this.mockReflectionUtility.Setup(
                ru =>
                ru.GetCustomAttributes(
                    memberInfo,
                    typeof(TestFrameworkV2.DeploymentItemAttribute))).Returns((object[])deploymentItemAttributes.ToArray());
        }

        private TestCase GetTestCase(string source)
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), source);
            testCase.SetPropertyValue(
                DeploymentItemUtilityTests.DeploymentItemsProperty,
                new[]
                    {
                        new KeyValuePair<string, string>(
                            DefaultDeploymentItemPath,
                            DefaultDeploymentItemOutputDirectory)
                    });

            return testCase;
        }

        private TestDeployment CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories)
        {
            var currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            testRunDirectories = new TestRunDirectories(currentExecutingFolder);

            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            var mockAssemblyUtility = new Mock<AssemblyUtility>();
            mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });
            this.mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(testRunDirectories.RootDeploymentDirectory);

            var deploymentItemUtility = new DeploymentItemUtility(this.mockReflectionUtility.Object);

            return new TestDeployment(
                deploymentItemUtility,
                new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, this.mockFileUtility.Object),
                this.mockFileUtility.Object);
        }
        #endregion
    }
}
