// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class CombinatorialDataAttributeTests : TestContainer
{
    public void GetDataReturnsEveryBooleanCombinationInStableOrder()
    {
        object?[][] rows = GetData(nameof(BooleanParameters));

        AssertRows(
            rows,
            [
                [true, true],
                [true, false],
                [false, true],
                [false, false],
            ]);
    }

    public void GetDataInfersIntegersEnumsAndNullableValues()
    {
        object?[][] rows = GetData(nameof(InferredParameters));

        AssertRows(
            rows,
            [
                [0, DateTimeKind.Unspecified, null],
                [0, DateTimeKind.Unspecified, true],
                [0, DateTimeKind.Unspecified, false],
                [0, DateTimeKind.Utc, null],
                [0, DateTimeKind.Utc, true],
                [0, DateTimeKind.Utc, false],
                [0, DateTimeKind.Local, null],
                [0, DateTimeKind.Local, true],
                [0, DateTimeKind.Local, false],
                [1, DateTimeKind.Unspecified, null],
                [1, DateTimeKind.Unspecified, true],
                [1, DateTimeKind.Unspecified, false],
                [1, DateTimeKind.Utc, null],
                [1, DateTimeKind.Utc, true],
                [1, DateTimeKind.Utc, false],
                [1, DateTimeKind.Local, null],
                [1, DateTimeKind.Local, true],
                [1, DateTimeKind.Local, false],
            ]);
    }

    public void GetDataUsesExplicitAndRangeValues()
    {
        object?[][] rows = GetData(nameof(ExplicitParameters));

        AssertRows(
            rows,
            [
                ["a", 2],
                ["a", 4],
                ["a", 6],
                ["b", 2],
                ["b", 4],
                ["b", 6],
            ]);
    }

    public void GetDataHonorsExactAndWildcardExclusions()
    {
        AssertRows(
            GetData(nameof(ExactExclusion)),
            [
                [true, true],
                [false, true],
                [false, false],
            ]);
        AssertRows(
            GetData(nameof(WildcardExclusion)),
            [
                [true, true],
                [false, true],
            ]);
        AssertRows(GetData(nameof(NullExclusion)), [["value"]]);
    }

    public void GetDataValidatesExclusionWidth()
    {
        Action action = () => GetData(nameof(InvalidExclusion));

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*{nameof(ExcludeTestCaseAttribute)}*number of test method parameters*");
    }

    public void GetDataRejectsUnsupportedTypes()
    {
        Action action = () => GetData(nameof(UnsupportedParameter));

        action.Should().Throw<NotSupportedException>()
            .WithMessage($"*{nameof(ICombinatorialValuesProvider)}*");
    }

    public void GetDataReportsConflictingValueProviders()
    {
        Action action = () => GetData(nameof(ConflictingValueProviders));

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*'value'*multiple combinatorial value providers*{nameof(CombinatorialValuesAttribute)}*{nameof(CombinatorialRangeAttribute)}*{nameof(ICombinatorialValuesProvider)}*");
    }

    public void MemberDataValuesAreUniquePerTestCase()
    {
        object?[][] rows = GetData(nameof(MutableMemberData));
        MutableValue[] values = rows.Select(row => (MutableValue)row[0]!).ToArray();

        values.Should().HaveCount(4);
        values.Select(value => value.Value).Should().BeEquivalentTo([1, 1, 2, 2]);
        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                values[i].Should().NotBeSameAs(values[j]);
            }
        }
    }

    public void ClassDataValuesAreUniquePerTestCase()
    {
        object?[][] rows = GetData(nameof(MutableClassData));
        MutableValue[] values = rows.Select(row => (MutableValue)row[0]!).ToArray();

        values.Should().HaveCount(4);
        values.Select(value => value.Value).Should().BeEquivalentTo([1, 1, 2, 2]);
        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                values[i].Should().NotBeSameAs(values[j]);
            }
        }
    }

    public void ValueProviderAttributeIsResolvedOnce()
    {
        CountingValuesAttribute.ConstructionCount = 0;

        object?[][] rows = GetData(nameof(CountingProviderData));

        rows.Should().HaveCount(4);
        CountingValuesAttribute.ConstructionCount.Should().Be(1);
    }

    public void GetDisplayNameUsesMSTestFormatting()
    {
        MethodInfo method = GetMethod(nameof(ExplicitParameters));

        string? displayName = new CombinatorialDataAttribute().GetDisplayName(method, ["a", 2]);

        displayName.Should().Be("ExplicitParameters (\"a\",2)");
    }

    private static object?[][] GetData(string methodName)
        => new CombinatorialDataAttribute().GetData(GetMethod(methodName)).ToArray();

    private static void AssertRows<T>(T[][] actual, T[][] expected)
    {
        actual.Should().HaveCount(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Should().Equal(expected[i]);
        }
    }

    private static MethodInfo GetMethod(string methodName)
        => typeof(CombinatorialDataAttributeTests).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;

    private static void BooleanParameters(bool first, bool second)
    {
    }

    private static void InferredParameters(int number, DateTimeKind kind, bool? flag)
    {
    }

    private static void ExplicitParameters(
        [CombinatorialValues("a", "b")] string text,
        [CombinatorialRange(2, 6, 2)] int number)
    {
    }

    [ExcludeTestCase(true, false)]
    private static void ExactExclusion(bool first, bool second)
    {
    }

    [ExcludeTestCase(typeof(AnyDataValue), false)]
    private static void WildcardExclusion(bool first, bool second)
    {
    }

    [ExcludeTestCase(true)]
    private static void InvalidExclusion(bool first, bool second)
    {
    }

    [ExcludeTestCase(null)]
    private static void NullExclusion([CombinatorialValues(null, "value")] string? value)
    {
    }

    private static void UnsupportedParameter(Guid value)
    {
    }

    private static void ConflictingValueProviders(
        [CombinatorialValues(1)]
        [CombinatorialRange(1, 2)]
        int value)
    {
    }

    private static void MutableMemberData(
        [CombinatorialMemberData(nameof(GetMutableValues))] MutableValue value,
        bool flag)
    {
    }

    private static void MutableClassData(
        [CombinatorialClassData(typeof(MutableClassDataSource))] MutableValue value,
        bool flag)
    {
    }

    private static void CountingProviderData([CountingValues] int value, bool flag)
    {
    }

    public static IEnumerable<MutableValue> GetMutableValues()
    {
        yield return new MutableValue(1);
        yield return new MutableValue(2);
    }

    public sealed class MutableValue(int value)
    {
        public int Value { get; } = value;
    }

    public sealed class MutableClassDataSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return [new MutableValue(1)];
            yield return [new MutableValue(2)];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class CountingValuesAttribute : Attribute, ICombinatorialValuesProvider
    {
        public CountingValuesAttribute() => ConstructionCount++;

        public static int ConstructionCount { get; set; }

        public object?[] GetValues(ParameterInfo parameter) => [1, 2];
    }
}
