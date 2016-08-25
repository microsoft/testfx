// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Portable.Tests.Services
{
    extern alias FrameworkV1;

    using System;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using MSTestAdapter.TestUtilities;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class PortableFileOperationsTests
    {
        private FileOperations fileOperations;

        [TestInitialize]
        public void TestInit()
        {
            this.fileOperations = new FileOperations();
        }

        [TestMethod]
        public void LoadAssemblyShouldThrowExceptionIfTheFileNameHasInvalidCharacters()
        {
            var filePath = "temp<>txt";
            Action a = () => this.fileOperations.LoadAssembly(filePath);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentException));
        }

        [TestMethod]
        public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
        {
            var filePath = "temptxt";
            Action a = () => this.fileOperations.LoadAssembly(filePath);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(FileNotFoundException));
        }

        [TestMethod]
        public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
        {
            var filePath = Assembly.GetExecutingAssembly().Location;
            // This should no throw.
            this.fileOperations.LoadAssembly(filePath);
        }

        [TestMethod]
        public void DoesFileExistReturnsTrueForAllFiles()
        {
            Assert.IsTrue(this.fileOperations.DoesFileExist(null));
            Assert.IsTrue(this.fileOperations.DoesFileExist("foobar"));
        }

        [TestMethod]
        public void CreateNavigationSessionShouldReturnNullForAllSources()
        {
            Assert.IsNull(this.fileOperations.CreateNavigationSession(null));
            Assert.IsNull(this.fileOperations.CreateNavigationSession("foobar"));
        }

        [TestMethod]
        public void GetNavigationDataShouldReturnNullFileName()
        {
            int minLineNumber;
            string fileName;

            this.fileOperations.GetNavigationData(null, null, null, out minLineNumber, out fileName);
            Assert.IsNull(fileName);
            Assert.AreEqual(-1, minLineNumber);
        }

        [TestMethod]
        public void GetFullFilePathShouldReturnAssemblyFileName()
        {
            Assert.AreEqual(null, this.fileOperations.GetFullFilePath(null));
            Assert.AreEqual("assemblyFileName", this.fileOperations.GetFullFilePath("assemblyFileName"));
        }
    }
}
