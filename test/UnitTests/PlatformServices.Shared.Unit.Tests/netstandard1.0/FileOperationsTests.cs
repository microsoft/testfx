// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Portable.Tests.Services
{
    extern alias FrameworkV1;

    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using MSTestAdapter.TestUtilities;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

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
            var filePath = Assembly.GetExecutingAssembly().Location;

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
        public void CreateNavigationSessionShouldReurnNullIfSourceIsNull()
        {
            try
            {
                FileOperations.Initialize(typeof(MockDiaSession).AssemblyQualifiedName, typeof(MockDiaNavigationData).AssemblyQualifiedName);

                Assert.IsNull(this.fileOperations.CreateNavigationSession(null));
                Assert.IsTrue(MockDiaSession.IsConstructorInvoked);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void CreateNavigationSessionShuldReturnNullIfDiaSessionNotFound()
        {
            try
            {
                FileOperations.Initialize(string.Empty, string.Empty);

                Assert.IsNull(this.fileOperations.CreateNavigationSession(null));
                Assert.IsFalse(MockDiaSession.IsConstructorInvoked);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void CreateNavigationSessionShouldReturnDiaSession()
        {
            try
            {
                FileOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);

                var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);

                Assert.IsTrue(diaSession is MockDiaSession);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void GetNavigationDataShouldReturnDataFromNavigationSession()
        {
            try
            {
                FileOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData() { FileName = "mock", MinLineNumber = 123 };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
                int minLineNumber;
                string fileName;
                this.fileOperations.GetNavigationData(
                    diaSession,
                    typeof(FileOperationsTests).FullName,
                    "GetNavigationDataShouldReturnDataFromNavigationSession",
                    out minLineNumber,
                    out fileName);

                Assert.AreEqual(navigationData.MinLineNumber, minLineNumber);
                Assert.AreEqual(navigationData.FileName, fileName);
                Assert.IsTrue(MockDiaSession.IsGetNavigationDataInvoked);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void GetNavigationDataShouldNotThrowOnNullNavigationSession()
        {
            FileOperations.Initialize(string.Empty, string.Empty);

            int minLineNumber;
            string fileName;
            this.fileOperations.GetNavigationData(
                null,
                typeof(FileOperationsTests).FullName,
                "GetNavigationDataShouldReturnDataFromNavigationSession",
                out minLineNumber,
                out fileName);

            Assert.AreEqual(-1, minLineNumber);
            Assert.IsNull(fileName);
        }

        [TestMethod]
        public void GetNavigationDataShouldNotThrowOnMissingFileNameField()
        {
            try
            {
                FileOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData3).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData3() { MinLineNumber = 123 };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
                int minLineNumber;
                string fileName;
                this.fileOperations.GetNavigationData(
                    diaSession,
                    typeof(FileOperationsTests).FullName,
                    "GetNavigationDataShouldReturnDataFromNavigationSession",
                    out minLineNumber,
                    out fileName);

                Assert.AreEqual(123, minLineNumber);
                Assert.IsNull(fileName);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void GetNavigationDataShouldNotThrowOnMissingLineNumberField()
        {
            try
            {
                FileOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData2).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData2() { FileName = "mock" };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
                int minLineNumber;
                string fileName;
                this.fileOperations.GetNavigationData(
                    diaSession,
                    typeof(FileOperationsTests).FullName,
                    "GetNavigationDataShouldReturnDataFromNavigationSession",
                    out minLineNumber,
                    out fileName);

                Assert.AreEqual(-1, minLineNumber);
                Assert.AreEqual(navigationData.FileName, fileName);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void GetFullFilePathShouldReturnAssemblyFileName()
        {
            Assert.AreEqual(null, this.fileOperations.GetFullFilePath(null));
            Assert.AreEqual("assemblyFileName", this.fileOperations.GetFullFilePath("assemblyFileName"));
        }

        [TestMethod]
        public void DisposeNavigationSessionShouldDisposeDiaSession()
        {
            try
            {
                FileOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);

                var diaSession = this.fileOperations.CreateNavigationSession(Assembly.GetExecutingAssembly().Location);
                this.fileOperations.DisposeNavigationSession(diaSession);

                Assert.IsTrue(MockDiaSession.IsDisposeInvoked);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }
    }

    public class MockDiaSession : IDisposable
    {
        static MockDiaSession()
        {
            IsConstructorInvoked = false;
            IsGetNavigationDataInvoked = false;
            IsDisposeInvoked = false;
        }

        public MockDiaSession(string source)
        {
            IsConstructorInvoked = true;
            if (string.IsNullOrEmpty(source))
            {
                throw new Exception();
            }
        }

        public static bool IsConstructorInvoked { get; set; }

        public static IDiaNavigationData DiaNavigationData { get; set; }

        public static bool IsGetNavigationDataInvoked { get; set; }

        public static bool IsDisposeInvoked { get; set; }

        public static void Reset()
        {
            IsConstructorInvoked = false;
            IsGetNavigationDataInvoked = false;
        }

        public object GetNavigationData(string className, string methodName)
        {
            IsGetNavigationDataInvoked = true;
            return DiaNavigationData;
        }

        public void Dispose()
        {
            IsDisposeInvoked = true;
        }
    }

    public interface IDiaNavigationData
    {
    }

    public class MockDiaNavigationData : IDiaNavigationData
    {
        public string FileName { get; set; }

        public int MinLineNumber { get; set; }
    }

    public class MockDiaNavigationData2 : IDiaNavigationData
    {
        public string FileName { get; set; }
    }

    public class MockDiaNavigationData3 : IDiaNavigationData
    {
        public int MinLineNumber { get; set; }
    }
}
