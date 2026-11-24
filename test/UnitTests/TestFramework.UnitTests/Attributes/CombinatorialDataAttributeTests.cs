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

    public void GetDataExcludesTrailingCancellationTokenFromCombinations()
    {
        object?[][] rows = GetData(nameof(BooleanAndIntegerWithToken));

        AssertRows(
            rows,
            [
                [true, 3],
                [true, 5],
                [false, 3],
                [false, 5],
            ]);
    }

    public void GetDataWithOnlyCancellationTokenProducesOneEmptyRow()
    {
        foreach (object?[][] rows in new[] { nameof(TokenOnly), nameof(OptionalTokenOnly) }.Select(GetData))
        {
            rows.Should().ContainSingle();
            rows[0].Should().BeEmpty();
        }
    }

    public void GetDataForParameterlessMethodRemainsEmpty()
        => GetData(nameof(Parameterless)).Should().BeEmpty();

    public void GetDataPreservesExplicitCancellationTokenValueProvider()
    {
        int previousConstructionCount = TokenValuesAttribute.ConstructionCount;
        try
        {
            object?[][] rows = GetData(nameof(BooleanWithExplicitTokens));

            AssertRows(
                rows,
                [
                    [true, CancellationToken.None],
                    [true, new CancellationToken(canceled: true)],
                    [false, CancellationToken.None],
                    [false, new CancellationToken(canceled: true)],
                ]);
            TokenValuesAttribute.ConstructionCount.Should().Be(previousConstructionCount + 1);
        }
        finally
        {
            TokenValuesAttribute.ConstructionCount = previousConstructionCount;
        }
    }

    public void GetDataDoesNotExcludeUnsupportedCancellationTokenShapes()
    {
        string[] methodNames = [nameof(NonTrailingToken), nameof(NullableToken), nameof(TokenSource), nameof(ByRefToken)];
        foreach (string methodName in methodNames)
        {
            Action action = () => GetData(methodName);

            action.Should().Throw<NotSupportedException>("unsupported shape {0} is still a data parameter", methodName);
        }
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

    public void CombinatorialAttributesAreSealedAndDataAttributeIsNotInherited()
    {
        Type[] attributeTypes =
        [
            typeof(CombinatorialDataAttribute),
            typeof(CombinatorialRandomDataAttribute),
            typeof(CombinatorialRangeAttribute),
            typeof(CombinatorialValuesAttribute),
        ];
        AttributeUsageAttribute usage = typeof(CombinatorialDataAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        attributeTypes.Should().OnlyContain(static type => type.IsSealed);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
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

    private static void BooleanAndIntegerWithToken(bool flag, [CombinatorialValues(3, 5)] int number, CancellationToken token)
    {
    }

    private static void TokenOnly(CancellationToken token)
    {
    }

    private static void OptionalTokenOnly(CancellationToken token = default)
    {
    }

    private static void Parameterless()
    {
    }

    private static void BooleanWithExplicitTokens(bool flag, [TokenValues] CancellationToken token)
    {
    }

    private static void NonTrailingToken(CancellationToken token, bool flag)
    {
    }

    private static void NullableToken(CancellationToken? token)
    {
    }

    private static void TokenSource(CancellationTokenSource source)
    {
    }

    private static void ByRefToken(ref CancellationToken token)
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

    private static void UnsupportedParameter(Guid value)
    {
    }

    private static void ConflictingValueProviders(
        [CombinatorialValues(1)]
        [CombinatorialRange(1, 2)]
        int value)
    {
    }

    private static void CountingProviderData([CountingValues] int value, bool flag)
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class TokenValuesAttribute : Attribute, ICombinatorialValuesProvider
    {
        public TokenValuesAttribute() => ConstructionCount++;

        public static int ConstructionCount { get; set; }

        public object?[] GetValues(ParameterInfo parameter) => [CancellationToken.None, new CancellationToken(canceled: true)];
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class CountingValuesAttribute : Attribute, ICombinatorialValuesProvider
    {
        public CountingValuesAttribute() => ConstructionCount++;

        public static int ConstructionCount { get; set; }

        public object?[] GetValues(ParameterInfo parameter) => [1, 2];
    }
}
