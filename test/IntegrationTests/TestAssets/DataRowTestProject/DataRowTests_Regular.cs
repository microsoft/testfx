// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
public class DataRowTests_Regular
{
    [TestMethod]
    [DataRow(10)]
    [DataRow(20)]
    [DataRow(30)]
    [DataRow(40)]
    public void DataRow1(int i) => Assert.IsTrue(i != 0);

    [TestMethod]
    [DataRow(10, "String parameter", true, false)]
    [DataRow(20, "String parameter", true, false)]
    [DataRow(30, "String parameter", true, false)]
    [DataRow(40, "String parameter", true, false)]
    public void DataRow2(int i, string s, bool b1, bool b2) => Assert.IsTrue(i != 200);

    [TestCategory("DataRowOptionalInvalidArguments")]
    [TestMethod]
    [ExpectedException(typeof(System.Reflection.TargetParameterCountException))]
    [DataRow]
    [DataRow(2)]
    [DataRow(2, "DerivedRequiredArgument", "DerivedOptionalArgument", "DerivedExtraArgument")]
    public void DataRowTestMethodFailsWithInvalidArguments(int i1, string requiredString, string s1 = null) => Assert.Fail();

    [TestMethod]
    [DataRow(10.01d, 20.01d)]
    [DataRow(10.02d, 20.02d)]
    public void DataRowTestDouble(double value1, double value2)
    {
        Assert.IsTrue(value1 > 10d);
        Assert.IsTrue(value2 > 20d);
    }

    [TestMethod]
    [DataRow(1, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(2, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(3, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(4, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    public void DataRowTestMixed(int index, byte b, int i, uint u, long l, ulong ul, float f, double d, string s)
    {
        Assert.AreEqual<byte>(10, b);
        Assert.AreEqual(10, i);
        Assert.AreEqual(10U, u);
        Assert.AreEqual(10L, l);
        Assert.AreEqual(10UL, ul);
        Assert.AreEqual(10F, f);
        Assert.AreEqual(10D, d);
        Assert.AreEqual("10", s);
    }

    [TestMethod]
    [DataRow("john.doe@example.com", "abc123", null)]
    [DataRow("john.doe@example.com", "abc123", "/unit/test")]
    public void NullValueInData(string email, string password, string returnUrl)
    {
    }

    [TestMethod]
    [DataRow(null)]
    public void NullValue(object o) => Assert.IsNull(o);

    [TestMethod]
    [DataRow(new string[] { "" })]
    public void OneStringArray(string[] lines) => Assert.AreEqual(1, lines.Length);

    [TestMethod]
    [DataRow(new string[] { "" }, new string[] { "1.4", "message" })]
    public void TwoStringArrays(string[] input1, string[] input2)
    {
        Assert.AreEqual(1, input1.Length);
        Assert.AreEqual(2, input2.Length);
    }

    [TestMethod]
    [DataRow(new object[] { "", 1 })]
    public void OneObjectArray(object[] objects) => Assert.AreEqual(2, objects.Length);

    [TestMethod]
    [DataRow(new object[] { "", 1 }, new object[] { 3 })]
    public void TwoObjectArrays(object[] objects1, object[] objects2)
    {
        Assert.AreEqual(2, objects1.Length);
        Assert.AreEqual(1, objects2.Length);
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 })]
    public void ThreeObjectArrays(object[] o1, object[] o2, object[] o3)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 })]
    public void FourObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 })]
    public void FiveObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 })]
    public void SixObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 })]
    public void SevenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 })]
    public void EightObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 })]
    public void NineObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 })]
    public void TenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 })]
    public void ElevenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 })]
    public void TwelveObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11, object[] o12)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 },
        new object[] { 13 })]
    public void ThirteenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11, object[] o12, object[] o13)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 },
        new object[] { 13 }, new object[] { 14 })]
    public void FourteenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11, object[] o12, object[] o13, object[] o14)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 },
        new object[] { 13 }, new object[] { 14 }, new object[] { 15 })]
    public void FifteenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11, object[] o12, object[] o13, object[] o14, object[] o15)
    {
    }

    [TestMethod]
    [DataRow(new object[] { 1 }, new object[] { 2 }, new object[] { 3 }, new object[] { 4 },
        new object[] { 5 }, new object[] { 6 }, new object[] { 7 }, new object[] { 8 },
        new object[] { 9 }, new object[] { 10 }, new object[] { 11 }, new object[] { 12 },
        new object[] { 13 }, new object[] { 14 }, new object[] { 15 }, new object[] { 16 })]
    public void SixteenObjectArrays(object[] o1, object[] o2, object[] o3, object[] o4,
        object[] o5, object[] o6, object[] o7, object[] o8, object[] o9, object[] o10,
        object[] o11, object[] o12, object[] o13, object[] o14, object[] o15, object[] o16)
    {
    }

    [TestMethod]
    [DataRow(1, 2, 3, 4, 5)]
    public void MultipleIntegersWrappedWithParams(params int[] integers) => Assert.AreEqual(5, integers.Length);
}
