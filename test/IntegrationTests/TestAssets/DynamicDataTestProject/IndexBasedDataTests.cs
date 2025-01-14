// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Runtime.Serialization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
public class IndexBasedDataTests
{
    #region https://github.com/microsoft/testfx/issues/908

    [TestMethod]
    [DynamicData(nameof(AddTestCases), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void Add_ShouldAddTheExpectedValues(Collection<string> systemUnderTest, IEnumerable<string> itemsToAdd, Collection<string> expected)
    {
        // The actual tested method is irrelevant. Executing this empty tests provokes the error
    }

    public static IEnumerable<object[]> AddTestCases
    {
        get
        {
            var sut = new Collection<string>();
            var expected = new Collection<string>();
            yield return new object[] { sut, Enumerable.Empty<string>(), expected };

            sut = new Collection<string>() { "1" };
            expected = new Collection<string>() { "1" };
            yield return new object[] { sut, Enumerable.Empty<string>(), expected };

            sut = new Collection<string>();
            expected = new Collection<string>() { "1" };
            yield return new object[] { sut, new[] { "1" }, expected };

            sut = new Collection<string>() { "1", "a", "b" };
            expected = new Collection<string>() { "1", "a", "b", "z", "j" };
            yield return new object[] { sut, new[] { "z", "j" }, expected };
        }
    }

    #endregion

    #region https://github.com/microsoft/testfx/issues/1022

    [TestMethod]
    [DynamicData(nameof(UnlimitedNaturalData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void TestUnlimitedNatural(UnlimitedNatural testObject, UnlimitedNatural other, bool expected)
    {
        bool actual = testObject.Equals(other);
        Assert.AreEqual(expected, actual);
    }

    public static IEnumerable<object[]> UnlimitedNaturalData
    {
        get
        {
            yield return new object[] { UnlimitedNatural.Zero, UnlimitedNatural.Infinite, false };
            yield return new object[] { UnlimitedNatural.Zero, UnlimitedNatural.Zero, true };
        }
    }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals
    public readonly struct UnlimitedNatural : IEquatable<UnlimitedNatural>
#pragma warning restore CA2231 // Overload operator equals on overriding value type Equals
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public static readonly UnlimitedNatural Infinite;
        public static readonly UnlimitedNatural One = new(1);
        public static readonly UnlimitedNatural Zero = new(0);

        public UnlimitedNatural(uint? value)
            => Value = value;

        public uint? Value { get; }

        public override string ToString()
            => Value?.ToString(CultureInfo.InvariantCulture) ?? "*";

        public override bool Equals(object obj)
            => obj is UnlimitedNatural other && Equals(other);

        public bool Equals(UnlimitedNatural other)
            => Value == other.Value;
    }

    #endregion

    #region https://github.com/microsoft/testfx/issues/1037

    [DynamicData(nameof(TestData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    [TestMethod]
    public void ValidateExMessage(Exception ex)
        => Assert.AreEqual(ex?.Message, "Test exception message");

    private static IEnumerable<object[]> TestData()
        => new List<object[]>()
        {
            new object[] { new InvalidUpdateException("Test exception message") },
        };

    [Serializable]
    public class InvalidUpdateException : Exception
    {
        public InvalidUpdateException(string message)
            : base(message)
        {
        }

        public InvalidUpdateException()
        {
        }

        [ExcludeFromCodeCoverage]
        protected InvalidUpdateException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
        }
    }

    #endregion

    #region https://github.com/microsoft/testfx/issues/1094

    [TestMethod]
    [DynamicData(nameof(Create_test_cases_for_multiplication_of_vector_and_real), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void Vector2D_op_Multiplication_Vector2D_double___valid_args___scaled_vector(Vector2D v, double d, Vector2D expected)
    {
        Vector2D actual = v * d;
        double delta = 0.00001;

        Assert.AreEqual(expected.U, actual.U, delta, "The U-component of the vector hasn't been computed correctly.");
        Assert.AreEqual(expected.V, actual.V, delta, "The V-component of the vector hasn't been computed correctly.");
    }

    [TestMethod]
    [DynamicData(nameof(Create_test_cases_for_multiplication_of_real_and_vector), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void Vector2D_op_Multiplication_double_Vector2D___valid_args___scaled_vector(double d, Vector2D v, Vector2D expected)
    {
        Vector2D actual = d * v;
        double delta = 0.00001;

        Assert.AreEqual(expected.U, actual.U, delta, "The U-component of the vector hasn't been computed correctly.");
        Assert.AreEqual(expected.V, actual.V, delta, "The V-component of the vector hasn't been computed correctly.");
    }

    [TestMethod]
    [DynamicData(nameof(Create_test_cases_for_scalar_product_of_two_vectors), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void Vector2D_op_Multiplication_Vector2D_Vector2D___valid_Vector2D___double_scalar_product(Vector2D v, Vector2D w, double expected)
    {
        double actual = v * w;
        double delta = 0.00001;

        Assert.AreEqual(expected, actual, delta, "The scalar product hasn't been computed correctly.");
    }

    private static IEnumerable<object[]> Create_test_cases_for_multiplication_of_vector_and_real()
        => new[]
        {
            new object[] { new Vector2D(0.0, 0.0), 0.0, new Vector2D(0.0, 0.0) },
            new object[] { new Vector2D(0.0, 0.0), -3.5, new Vector2D(0.0, 0.0) },
            new object[] { new Vector2D(2.0, 3.0), 2.1, new Vector2D(4.2, 6.3) },
            new object[] { new Vector2D(1.0, 2.0), -0.73, new Vector2D(-0.73, -1.46) },
            new object[] { new Vector2D(-3.4, 2.75), 22.415, new Vector2D(-76.211, 61.64125) },
            new object[] { new Vector2D(12.43, -2.754), 1023.56, new Vector2D(12722.8508, -2818.88424) },
            new object[] { new Vector2D(4.23, 6.81187), -13.25, new Vector2D(-56.0475, -90.2572775) },
            new object[] { new Vector2D(-17.4327, -8.1956), -0.45, new Vector2D(7.844715, 3.68802) },
        };

    private static IEnumerable<object[]> Create_test_cases_for_multiplication_of_real_and_vector()
        => new[]
        {
            new object[] { 0.0, new Vector2D(0.0, 0.0), new Vector2D(0.0, 0.0) },
            new object[] { -3.5, new Vector2D(0.0, 0.0), new Vector2D(0.0, 0.0) },
            new object[] { 2.1, new Vector2D(2.0, 3.0), new Vector2D(4.2, 6.3) },
            new object[] { -0.73, new Vector2D(1.0, 2.0), new Vector2D(-0.73, -1.46) },
            new object[] { 22.415, new Vector2D(-3.4, 2.75), new Vector2D(-76.211, 61.64125) },
            new object[] { 1023.56, new Vector2D(12.43, -2.754), new Vector2D(12722.8508, -2818.88424) },
            new object[] { -13.25, new Vector2D(4.23, 6.81187), new Vector2D(-56.0475, -90.2572775) },
            new object[] { -0.45, new Vector2D(-17.4327, -8.1956), new Vector2D(7.844715, 3.68802) },
        };

    private static IEnumerable<object[]> Create_test_cases_for_scalar_product_of_two_vectors()
        => new[]
        {
            new object[] { new Vector2D(0.0, 0.0), new Vector2D(0.0, 0.0), 0.0 },
            new object[] { new Vector2D(1.0, 2.0), new Vector2D(0.0, 0.0), 0.0 },
            new object[] { new Vector2D(0.0, 0.0), new Vector2D(2.0, 3.0), 0.0 },
            new object[] { new Vector2D(1.0, 3.0), new Vector2D(1.0, 2.0), 7.0 },
            new object[] { new Vector2D(-1.0, -2.0), new Vector2D(-3.4, 2.75), -2.1 },
            new object[] { new Vector2D(3.355, -2.211), new Vector2D(12.43, -2.754), 47.791744 },
            new object[] { new Vector2D(-0.15, 2.03), new Vector2D(4.23, 6.81187), 13.1935961 },
            new object[] { new Vector2D(-22.7231, -78.2976), new Vector2D(-17.4327, -8.1956), 1037.82079593 },
        };

    public readonly struct Vector2D
    {
        public Vector2D(double u, double v)
        {
            U = u;
            V = v;
        }

        public double U { get; }

        public double V { get; }

        public override string ToString()
            => FormattableString.Invariant($"Vector2D: {U:F3} / {V:F3}");

        public static Vector2D operator *(Vector2D v, double d)
            => new(v.U * d, v.V * d);

        public static Vector2D operator *(double d, Vector2D v)
            => new(d * v.U, d * v.V);

        public static double operator *(Vector2D v, Vector2D w)
            => (v.U * w.U) + (v.V * w.V);
    }

    #endregion

    #region https://github.com/microsoft/testfx/issues/1588

    [TestMethod]
    [DynamicData(nameof(GetTestData), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void TestReadonlyCollectionData(string someString, MyData foo)
    {
    }

    private static IEnumerable<object[]> GetTestData()
    {
        yield return new object[]
        {
            string.Empty, new MyData(),
        };
    }

    public class MyData
    {
        private readonly SortedSet<int> _values;

        public MyData() => _values = new SortedSet<int>();

        public IEnumerable<int> Values => _values;
    }

    #endregion
}
