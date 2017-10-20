// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ParallelTestMethods
{
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void SimpleTest11()
        {
            Assert.AreEqual(1, 1);
        }

        [TestMethod]
        public void SimpleTest12()
        {
            Thread.Sleep(1000);
            Assert.Fail();
        }
    }
}
