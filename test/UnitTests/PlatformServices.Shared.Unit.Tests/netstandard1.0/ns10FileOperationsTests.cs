// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using MSTestAdapter.TestUtilities;

    [TestClass]
    public class FileOperationsTests
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
            Action a = () => this.fileOperations.LoadAssembly(filePath, false);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentException));
        }

        [TestMethod]
        public void LoadAssemblyShouldThrowExceptionIfFileIsNotFound()
        {
            var filePath = "temptxt";
            Action a = () => this.fileOperations.LoadAssembly(filePath, false);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(FileNotFoundException));
        }

        [TestMethod]
        public void LoadAssemblyShouldLoadAssemblyInCurrentContext()
        {
            var filePath = typeof(FileOperationsTests).GetTypeInfo().Assembly.Location;

            // This should not throw.
            this.fileOperations.LoadAssembly(filePath, false);
        }

        [TestMethod]
        public void DoesFileExistReturnsTrueForAllFiles()
        {
            Assert.IsTrue(this.fileOperations.DoesFileExist(null));
            Assert.IsTrue(this.fileOperations.DoesFileExist("foobar"));
        }

        [TestMethod]
        public void GetFullFilePathShouldReturnAssemblyFileName()
        {
            Assert.IsNull(this.fileOperations.GetFullFilePath(null));
            Assert.AreEqual("assemblyFileName", this.fileOperations.GetFullFilePath("assemblyFileName"));
        }
    }
#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
