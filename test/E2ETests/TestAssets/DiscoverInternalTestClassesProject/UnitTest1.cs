// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DiscoverInternalTestClasses]

namespace DiscoverInternalTestClassesProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    internal class TopLevelInternalClass
    {
        [TestMethod]
        public void TopLevelInternalClass_TestMethod1()
        {
        }

        [TestClass]
        internal class NestedInternalClass
        {
            [TestMethod]
            public void NestedInternalClass_TestMethod1()
            {
            }
        }
    }
}
