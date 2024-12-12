// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FrameworkTestNodeUid = Microsoft.Testing.Internal.Framework.TestNodeUid;

using TestNodeUid = Microsoft.Testing.Platform.Extensions.Messages.TestNodeUid;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestNodeUidTests
{
    public TestNodeUidTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [TestMethod]
    public void TestNodeUid_EqualityChecks_ShouldWorkAsExpected()
    {
        TestNodeUid testNodeUid = "TestNodeUid";
        string testNodeUidString = testNodeUid;
        Assert.AreEqual(new TestNodeUid("TestNodeUid"), testNodeUid);
        Assert.IsTrue(new FrameworkTestNodeUid("TestNodeUid") == "TestNodeUid");
        Assert.IsTrue(testNodeUid == "TestNodeUid");
        Assert.IsTrue(testNodeUid != "TestNodeUid2");
        Assert.IsTrue(testNodeUidString == testNodeUid);
    }

    [TestMethod]
    public void TestNodeUid_NullValue_ShouldFail()
    {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
        // This won't fail, TestNodeUid is a record (not a record struct) and assigning null is a compile time check
        // not a runtime check. No construction of the object is happening, so we won't throw.
        // Assert.Throw<ArgumentNullException>(() => { TestNodeUid testNode = null!; });

        // Implicit conversion from a null, empty or whitespace string should throw.
        Assert.ThrowsException<ArgumentNullException>(() => { TestNodeUid testNode = (string)null!; });
        Assert.ThrowsException<ArgumentException>(() => { TestNodeUid testNode = string.Empty; });
        Assert.ThrowsException<ArgumentException>(() => { TestNodeUid testNode = " "; });

        // Providing null, empty, or whitespace id should throw.
        Assert.ThrowsException<ArgumentNullException>(() => { TestNodeUid testNode = new(null!); });
        Assert.ThrowsException<ArgumentException>(() => { TestNodeUid testNode = new(string.Empty); });
        Assert.ThrowsException<ArgumentException>(() => { TestNodeUid testNode = new(" "); });
#pragma warning restore CS0219 // Variable is assigned but its value is never used
    }
}
