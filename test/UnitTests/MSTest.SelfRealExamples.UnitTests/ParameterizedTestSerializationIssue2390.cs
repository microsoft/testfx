// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.SelfRealExamples.UnitTests;

// Test for https://github.com/microsoft/testfx/issues/2390
[TestClass]
public class ParameterizedTestSerializationIssue2390
{
    [TestMethod]
    [DataRow((byte)0, new object[] { (byte)0 })]
    [DataRow((short)0, new object[] { (short)0 })]
    [DataRow(0L, new object[] { 0L })]
    public void CheckNestedInputTypes(object expected, object nested)
    {
        object[] array = (object[])nested;
        object actual = Assert.ContainsSingle(array);
#if NETFRAMEWORK
        // Buggy behavior, because of app domains.
        Assert.AreEqual(typeof(int), actual.GetType());
#else
        Assert.AreEqual(expected.GetType(), actual.GetType());
        Assert.AreEqual(expected, actual);
#endif
    }
}
