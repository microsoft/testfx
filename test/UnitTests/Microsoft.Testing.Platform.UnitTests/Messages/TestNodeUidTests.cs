// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestNodeUidTests
{
    [TestMethod]
    public void TestNodeUid_EqualityChecks_ShouldWorkAsExpected()
    {
        TestNodeUid testNodeUid = "TestNodeUid";
        string testNodeUidString = testNodeUid;
        Assert.AreEqual(new TestNodeUid("TestNodeUid"), testNodeUid);
        Assert.AreEqual<string>("TestNodeUid", testNodeUid);
        Assert.AreNotEqual<string>("TestNodeUid2", testNodeUid);
        Assert.AreEqual<string>(testNodeUidString, testNodeUid);
    }

    [TestMethod]
    public void TestNodeUid_NullValue_ShouldFail()
    {
        // This won't fail, TestNodeUid is a record (not a record struct) and assigning null is a compile time check
        // not a runtime check. No construction of the object is happening, so we won't throw.
        // Assert.Throw<ArgumentNullException>(() => { TestNodeUid testNode = null!; });

        // Implicit conversion from a null, empty or whitespace string should throw.
        Assert.ThrowsExactly<ArgumentNullException>(() => { TestNodeUid testNode = (string)null!; });
        Assert.ThrowsExactly<ArgumentException>(() => { TestNodeUid testNode = string.Empty; });
        Assert.ThrowsExactly<ArgumentException>(() => { TestNodeUid testNode = " "; });

        // Providing null, empty, or whitespace id should throw.
        Assert.ThrowsExactly<ArgumentNullException>(() => { TestNodeUid testNode = new(null!); });
        Assert.ThrowsExactly<ArgumentException>(() => { TestNodeUid testNode = new(string.Empty); });
        Assert.ThrowsExactly<ArgumentException>(() => { TestNodeUid testNode = new(" "); });
    }
}
