// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class CollectionAssertTests
{
    [TestMethod]
    public void ThatShouldReturnAnInstanceOfCollectionAssert() => Assert.IsNotNull(TestFrameworkV2.CollectionAssert.That);

    [TestMethod]
    public void ThatShouldCacheCollectionAssertInstance() => Assert.AreEqual(TestFrameworkV2.CollectionAssert.That, TestFrameworkV2.CollectionAssert.That);
}
