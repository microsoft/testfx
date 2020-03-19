// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UWP.UnitTests
{
    extern alias FrameworkV1;

    using System;
    using System.IO;
    using System.Reflection;
    using FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;
    using global::MSTestAdapter.TestUtilities;

    /// <summary>
    /// The universal file operations tests.
    /// </summary>
    [TestClass]
    public class UniversalFileOperationsTests
    {
        private FileOperations fileOperations;

        /// <summary>
        /// The test initialization.
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            this.fileOperations = new FileOperations();
        }

        /// <summary>
        /// The load assembly should throw exception if the file name has invalid characters.
        /// </summary>
        [TestMethod]
        public void LoadAssemblyShouldThrowExceptionIfTheFileNameHasInvalidCharacters()
        {
            var filePath = "temp<>txt";
            Action a = () => this.fileOperations.LoadAssembly(filePath, false);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentException));
        }

        /// <summary>
        /// The load assembly should throw exception if file is not found.
        /// </summary>
        [TestMethod]
        public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
        {
            var filePath = "temptxt";
            Action a = () => this.fileOperations.LoadAssembly(filePath, false);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(FileNotFoundException));
        }

        /// <summary>
        /// The load assembly should load assembly in current context.
        /// </summary>
        [TestMethod]
        public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
        {
            var filePath = Assembly.GetExecutingAssembly().Location;

            // This should not throw.
            this.fileOperations.LoadAssembly(filePath, false);
        }

        /// <summary>
        /// The does file exist returns false if file name has invalid characters.
        /// </summary>
        [TestMethod]
        public void DoesFileExistReturnsFalseIfFileNameHasInvalidCharacters()
        {
            var filePath = "temp<>txt";
            Assert.IsFalse(this.fileOperations.DoesFileExist(filePath));
        }

        /// This Test is not yet validated. Will validate with new adapter.
        /// <summary>
        /// The does file exist returns false if file is not found.
        /// </summary>
        // [TestMethod]
        public void DoesFileExistReturnsFalseIfFileIsNotFound()
        {
            var filePath = "C:\\footemp.txt";
            Action a = () => this.fileOperations.DoesFileExist(filePath);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(FileNotFoundException));
        }

        /// This Test is not yet validated. Will validate with new adapter.
        /// <summary>
        /// The does file exist returns true when file exists.
        /// </summary>
        // [TestMethod]
        public void DoesFileExistReturnsTrueWhenFileExists()
        {
            var filePath = Assembly.GetExecutingAssembly().Location;
            Assert.IsTrue(this.fileOperations.DoesFileExist(filePath));
        }

        /// <summary>
        /// The create navigation session should return null for all sources.
        /// </summary>
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

        // Enable these tests when we take dependency on TpV2 object model
        // In Tpv1 UWP Object model these below methods are not defined.
        /*
        [TestMethod]
        public void CreateNavigationSessionShouldReturnDiaSession()
        {
            var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
            Assert.IsNotNull(diaSession);
        }

        [TestMethod]
        public void GetNavigationDataShouldReturnDataFromNavigationSession()
        {
            var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
            int minLineNumber;
            string fileName;
            this.fileOperations.GetNavigationData(
                diaSession,
                typeof(UniversalFileOperationsTests).FullName,
                "GetNavigationDataShouldReturnDataFromNavigationSession",
                out minLineNumber,
                out fileName);

            Assert.AreNotEqual(-1, minLineNumber);
            Assert.IsNotNull(fileName);
        }

        [TestMethod]
        public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
        {
            int minLineNumber;
            string fileName;
            this.fileOperations.GetNavigationData(
                null,
                typeof(UniversalFileOperationsTests).FullName,
                "GetNavigationDataShouldNotThrowOnNullNavigationSession",
                out minLineNumber,
                out fileName);

            Assert.AreEqual(-1, minLineNumber);
            Assert.IsNull(fileName);
        }

        [TestMethod]
        public void DisposeNavigationSessionShouldNotThrowOnNullNavigationSession()
        {
            // This should not throw.
            this.fileOperations.DisposeNavigationSession(null);
        }
        */

        /// <summary>
        /// The get full file path should return assembly file name.
        /// </summary>
        [TestMethod]
        public void GetFullFilePathShouldReturnAssemblyFileName()
        {
            Assert.IsNull(this.fileOperations.GetFullFilePath(null));
            Assert.AreEqual("assemblyFileName", this.fileOperations.GetFullFilePath("assemblyFileName"));
        }
    }
}
