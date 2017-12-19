// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ParallelMethodsTestProject
{
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTest2
    {
        private static bool assemblyInitCalled;
        private static bool assemblyCleanCalled;
        private static bool classInitCalled;
        private static bool classCleanCalled;

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            Assert.IsFalse(assemblyInitCalled);
            assemblyInitCalled = true;
        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            Assert.IsFalse(classInitCalled);
            classInitCalled = true;
        }

        [TestInitialize]
        public void Initialize()
        {
        }

        [TestCleanup]
        public void Cleanup()
        {
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Assert.IsFalse(classCleanCalled);
            classCleanCalled = true;
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Assert.IsFalse(assemblyCleanCalled);
            assemblyCleanCalled = true;
        }

        [TestMethod]
        public void SimpleTest21()
        {
            Thread.Sleep(Constants.WaitTimeInMS);
            Assert.AreEqual(0, 0);
        }

        [TestMethod]
        public void SimpleTest22()
        {
            Thread.Sleep(Constants.WaitTimeInMS);
            Assert.Fail();
        }

        [TestMethod]
        [DoNotParallelize]
        public void IsolatedTest()
        {
            Thread.Sleep(Constants.WaitTimeInMS);
            Assert.IsTrue(true);
        }
    }
}
