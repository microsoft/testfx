// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProjectWithNativeAOT;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    // MSTest.SourceGeneration materializes the [DataRow] values into a generated attribute array at
    // compile time (instead of discovering them via GetCustomAttributes at run time), and invokes this
    // method through a generated delegate instead of MethodInfo.Invoke.
    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(2, 2, 4)]
    [DataRow(-1, 1, 0)]
    public void Add_ReturnsExpectedSum(int a, int b, int expected)
        => Assert.AreEqual(expected, a + b);

    // [DynamicData] sources are still evaluated at run time (the values aren't known at compile time),
    // but the source generator still avoids reflecting over the test method itself to invoke it.
    [TestMethod]
    [DynamicData(nameof(DivisionCases))]
    public void Divide_ReturnsExpectedQuotient(int dividend, int divisor, int expected)
        => Assert.AreEqual(expected, dividend / divisor);

    public static IEnumerable<object[]> DivisionCases { get; } =
    [
        [10, 2, 5],
        [9, 3, 3],
        [-6, 2, -3],
    ];
}

// A base class that is *not* itself annotated with [TestClass] is a supported pattern: the derived
// class below applies [TestClass] directly, so the source generator can see and root it. Only
// *implicitly* becoming a test class by inheriting [TestClass] from a base class is unsupported -
// see NotSourceGenerated.cs for that case.
public abstract class CalculatorFixtureBase
{
    public TestContext TestContext { get; set; } = null!;

    protected static int Square(int value) => value * value;
}

[TestClass]
[TestCategory("Arithmetic")]
public class DerivedCalculatorTests : CalculatorFixtureBase
{
    [TestMethod]
    [DataRow(3, 9)]
    [DataRow(-4, 16)]
    public void Square_ReturnsExpectedValue(int value, int expected)
        => Assert.AreEqual(expected, Square(value));
}