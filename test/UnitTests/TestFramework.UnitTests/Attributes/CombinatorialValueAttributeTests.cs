// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class CombinatorialValueAttributeTests : TestContainer
{
    public void ValuesAttributeTreatsNullParamsArrayAsOneNullValue()
        => new CombinatorialValuesAttribute(null).Values.Should().Equal([null]);

    public void SignedRangeSupportsCountsAndStepsInBothDirections()
    {
        new CombinatorialRangeAttribute(3, 3).Values.Should().Equal([3, 4, 5]);
        new CombinatorialRangeAttribute(0, 7, 2).Values.Should().Equal([0, 2, 4, 6]);
        new CombinatorialRangeAttribute(7, 0, -2).Values.Should().Equal([7, 5, 3, 1]);
    }

    public void UnsignedRangeSupportsStepsInBothDirections()
    {
        new CombinatorialRangeAttribute(0u, 4u).Values.Should().Equal([0u, 1u, 2u, 3u]);
        new CombinatorialRangeAttribute(0u, 7u, 2u).Values.Should().Equal([0u, 2u, 4u, 6u]);
        new CombinatorialRangeAttribute(7u, 0u, 2u).Values.Should().Equal([7u, 5u, 3u, 1u]);
    }

    public void RangeRejectsInvalidCountsAndSteps()
    {
        Action signedCount = () => _ = new CombinatorialRangeAttribute(0, 0);
        Action signedOverflow = () => _ = new CombinatorialRangeAttribute(int.MaxValue, 2);
        Action signedStep = () => _ = new CombinatorialRangeAttribute(0, 1, 0);
        Action unsignedCount = () => _ = new CombinatorialRangeAttribute(0u, 0u);
        Action unsignedOverflow = () => _ = new CombinatorialRangeAttribute(uint.MaxValue, 2u);
        Action unsignedStep = () => _ = new CombinatorialRangeAttribute(0u, 1u, 0u);

        signedCount.Should().Throw<ArgumentOutOfRangeException>();
        signedOverflow.Should().Throw<ArgumentOutOfRangeException>();
        signedStep.Should().Throw<ArgumentOutOfRangeException>();
        unsignedCount.Should().Throw<ArgumentOutOfRangeException>();
        unsignedOverflow.Should().Throw<ArgumentOutOfRangeException>();
        unsignedStep.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void RangeHandlesBoundaryArithmeticWithoutWrapping()
    {
        new CombinatorialRangeAttribute(int.MinValue, int.MaxValue, int.MaxValue)
            .Values.Should().Equal([int.MinValue, -1, int.MaxValue - 1]);
        new CombinatorialRangeAttribute(0u, uint.MaxValue, uint.MaxValue)
            .Values.Should().Equal([0u, uint.MaxValue]);
        new CombinatorialRangeAttribute(uint.MaxValue - 1, uint.MaxValue, 2u)
            .Values.Should().Equal([uint.MaxValue - 1]);
    }

    public void RandomDataIsUniqueBoundedSeededAndCached()
    {
        var attribute = new CombinatorialRandomDataAttribute
        {
            Count = 5,
            Minimum = 10,
            Maximum = 20,
            Seed = 42,
        };

        object[] values = attribute.Values;

        values.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        values.Cast<int>().Should().OnlyContain(value => value >= 10 && value <= 20);
        attribute.GetValues(null!).Should().BeSameAs(values);
        new CombinatorialRandomDataAttribute
        {
            Count = 5,
            Minimum = 10,
            Maximum = 20,
            Seed = 42,
        }.Values.Should().Equal(values);
    }

    public void RandomDataRejectsInvalidConfiguration()
    {
        Action nonPositiveCount = () => _ = new CombinatorialRandomDataAttribute { Count = 0 }.Values;
        Action reversedRange = () => _ = new CombinatorialRandomDataAttribute { Minimum = 2, Maximum = 1 }.Values;
        Action excessiveCount = () => _ = new CombinatorialRandomDataAttribute { Count = 3, Minimum = 1, Maximum = 2 }.Values;

        nonPositiveCount.Should().Throw<InvalidOperationException>();
        reversedRange.Should().Throw<InvalidOperationException>();
        excessiveCount.Should().Throw<InvalidOperationException>();
    }

    public void RandomDataSupportsDenseAndFullIntegerRanges()
    {
        object[] denseValues = new CombinatorialRandomDataAttribute
        {
            Count = 1001,
            Minimum = 0,
            Maximum = 1000,
            Seed = 42,
        }.Values;
        object[] fullRangeValues = new CombinatorialRandomDataAttribute
        {
            Count = 10,
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            Seed = 42,
        }.Values;
        object[] maximumValue = new CombinatorialRandomDataAttribute
        {
            Count = 1,
            Minimum = int.MaxValue,
            Maximum = int.MaxValue,
            Seed = 42,
        }.Values;

        denseValues.Should().HaveCount(1001).And.OnlyHaveUniqueItems();
        fullRangeValues.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        maximumValue.Should().Equal([int.MaxValue]);
    }
}
