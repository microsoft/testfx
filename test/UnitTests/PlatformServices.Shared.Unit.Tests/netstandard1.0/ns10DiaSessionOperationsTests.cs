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
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    [TestClass]
    public class DiaSessionOperationsTests
    {
        private FileOperations fileOperations;

        public DiaSessionOperationsTests()
        {
            this.fileOperations = new FileOperations();
        }

        [TestMethod]
        public void CreateNavigationSessionShouldReurnNullIfSourceIsNull()
        {
            try
            {
                DiaSessionOperations.Initialize(typeof(MockDiaSession).AssemblyQualifiedName, typeof(MockDiaNavigationData).AssemblyQualifiedName);

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
                DiaSessionOperations.Initialize(string.Empty, string.Empty);

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
                DiaSessionOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);

                var diaSession = this.fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);

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
                DiaSessionOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData() { FileName = "mock", MinLineNumber = 86 };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
                this.fileOperations.GetNavigationData(
    diaSession,
    typeof(DiaSessionOperationsTests).FullName,
    "GetNavigationDataShouldReturnDataFromNavigationSession",
    out int minLineNumber,
    out string fileName);

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
            DiaSessionOperations.Initialize(string.Empty, string.Empty);

            this.fileOperations.GetNavigationData(
            null,
            typeof(DiaSessionOperationsTests).FullName,
            "GetNavigationDataShouldReturnDataFromNavigationSession",
            out int minLineNumber,
            out string fileName);

            Assert.AreEqual(-1, minLineNumber);
            Assert.IsNull(fileName);
        }

        [TestMethod]
        public void GetNavigationDataShouldNotThrowOnMissingFileNameField()
        {
            try
            {
                DiaSessionOperations.Initialize(
                typeof(MockDiaSession).AssemblyQualifiedName,
                typeof(MockDiaNavigationData3).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData3() { MinLineNumber = 86 };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
                this.fileOperations.GetNavigationData(
                diaSession,
                typeof(DiaSessionOperationsTests).FullName,
                "GetNavigationDataShouldReturnDataFromNavigationSession",
                out int minLineNumber,
                out string fileName);

                Assert.AreEqual(86, minLineNumber);
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
                DiaSessionOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData2).AssemblyQualifiedName);
                var navigationData = new MockDiaNavigationData2() { FileName = "mock" };
                MockDiaSession.DiaNavigationData = navigationData;

                var diaSession = this.fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
                this.fileOperations.GetNavigationData(
                diaSession,
                typeof(DiaSessionOperationsTests).FullName,
                "GetNavigationDataShouldReturnDataFromNavigationSession",
                out int minLineNumber,
                out string fileName);

                Assert.AreEqual(-1, minLineNumber);
                Assert.AreEqual(navigationData.FileName, fileName);
            }
            finally
            {
                MockDiaSession.Reset();
            }
        }

        [TestMethod]
        public void DisposeNavigationSessionShouldDisposeDiaSession()
        {
            try
            {
                DiaSessionOperations.Initialize(
                    typeof(MockDiaSession).AssemblyQualifiedName,
                    typeof(MockDiaNavigationData).AssemblyQualifiedName);

                var diaSession = this.fileOperations.CreateNavigationSession(typeof(DiaSessionOperationsTests).GetTypeInfo().Assembly.Location);
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

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
