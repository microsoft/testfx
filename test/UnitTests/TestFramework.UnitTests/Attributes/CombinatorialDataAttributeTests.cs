// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

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

    public void GetDataRejectsExclusionValuesOutsideCandidateSet()
    {
        Action action = () => GetData(nameof(IneffectiveExclusion));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*2*parameter position 0*");
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

    public void MemberDataProviderIsEnumeratedOnce()
    {
        MemberEnumerationCount = 0;

        GetData(nameof(CountingMemberData)).Should().HaveCount(4);

        MemberEnumerationCount.Should().Be(1);
    }

    public void ClassDataProviderIsCreatedAndEnumeratedOnce()
    {
        CountingClassDataSource.ConstructionCount = 0;
        CountingClassDataSource.EnumerationCount = 0;

        GetData(nameof(CountingClassData)).Should().HaveCount(4);

        CountingClassDataSource.ConstructionCount.Should().Be(1);
        CountingClassDataSource.EnumerationCount.Should().Be(1);
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

    [ExcludeTestCase(2)]
    private static void IneffectiveExclusion(bool value)
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

    private static void CountingMemberData(
        [CombinatorialMemberData(nameof(GetCountingMemberValues))] int value,
        bool flag)
    {
    }

    private static void CountingClassData(
        [CombinatorialClassData(typeof(CountingClassDataSource))] int value,
        bool flag)
    {
    }

    private static void CountingProviderData([CountingValues] int value, bool flag)
    {
    }

    public static int MemberEnumerationCount { get; set; }

    public static IEnumerable<int> GetCountingMemberValues()
    {
        MemberEnumerationCount++;
        return [1, 2];
    }

    public sealed class CountingClassDataSource : IEnumerable<object[]>
    {
        public CountingClassDataSource() => ConstructionCount++;

        public static int ConstructionCount { get; set; }

        public static int EnumerationCount { get; set; }

        public IEnumerator<object[]> GetEnumerator()
            => GetRows().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<object[]> GetRows()
        {
            EnumerationCount++;
            yield return [1];
            yield return [2];
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class CountingValuesAttribute : Attribute, ICombinatorialValuesProvider
    {
        public CountingValuesAttribute() => ConstructionCount++;

        public static int ConstructionCount { get; set; }

        public object?[] GetValues(ParameterInfo parameter) => [1, 2];
    }
}
