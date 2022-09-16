// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

using global::TestFramework.ForTestingMSTest;

public class CollectionAssertTests : TestContainer
{
    public void ThatShouldReturnAnInstanceOfCollectionAssert()
    {
        Assert.IsNotNull(TestFrameworkV2.CollectionAssert.That);
    }

    public void ThatShouldCacheCollectionAssertInstance()
    {
        Assert.AreEqual(TestFrameworkV2.CollectionAssert.That, TestFrameworkV2.CollectionAssert.That);
    }
}
