// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
public class DataRowTests_DerivedClass : DataRowTests_BaseClass
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Test type")]
    public enum TestEnum
    {
        Alfa,
        Beta,
        Gamma,
        Delta,
        Epsilon,
    }

    [TestCategory("DataRowSimple")]
    [TestMethod]
    [DataRow("DerivedString1")]
    [DataRow("DerivedString2")]
    public override void DataRowTestMethod(string a) => Assert.IsTrue(true);

    [TestCategory("DataRowSomeOptional")]
    [TestMethod]
    [DataRow(123)]
    [DataRow(123, "DerivedOptionalString1")]
    [DataRow(123, "DerivedOptionalString2", "DerivedOptionalString3")]
    public void DataRowTestMethodWithSomeOptionalParameters(int i, string s1 = null, string s2 = null) => Assert.IsTrue(true);

    [TestCategory("DataRowAllOptional")]
    [TestMethod]
    [DataRow]
    [DataRow(123)]
    [DataRow(123, "DerivedOptionalString4")]
    [DataRow(123, "DerivedOptionalString5", "DerivedOptionalString6")]
    public void DataRowTestMethodWithAllOptionalParameters(int i = 0, string s1 = null, string s2 = null) => Assert.IsTrue(true);

    [TestCategory("DataRowParamsArgument")]
    [TestMethod]
    [DataRow(2)]
    [DataRow(2, "DerivedSingleParamsArg")]
    [DataRow(2, "DerivedParamsArg1", "DerivedParamsArg2")]
    [DataRow(2, "DerivedParamsArg1", "DerivedParamsArg2", "DerivedParamsArg3")]
    public void DataRowTestMethodWithParamsParameters(int i, params string[] args) => Assert.IsTrue(true);

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
    [DataRow((byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    public void DataRowTestMixed(byte b, int i, uint u, long l, ulong ul, float f, double d, string s)
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
    [DataRow(null)]
    [DataRow(TestEnum.Alfa)]
    [DataRow(TestEnum.Beta)]
    [DataRow(TestEnum.Gamma)]
    public void DataRowEnums(TestEnum? testEnum) => Assert.IsTrue(true);

    [TestMethod]
    [DataRow(typeof(string))]
    [DataRow(typeof(int))]
    [DataRow(typeof(DataRowTests_DerivedClass))]
    public void DataRowNonSerializable(Type type) => Assert.IsTrue(true);
}
