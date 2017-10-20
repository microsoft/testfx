// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ParallelTestMethods
{
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTest2
    {
        int x = 1;
        [TestMethod]
        public void SimpleTest21()
        {
            Assert.AreEqual(0, 0);
        }

        [TestMethod]
        public void SimpleTest22()
        {
            Thread.Sleep(1000);
            x++;
            Assert.Fail();

        }

        [TestMethod]
        [DoNotParallelize]
        public void IsolatedTest()
        {
            Thread.Sleep(3000);
            Assert.IsTrue(x == 2);
        }
    }
}
