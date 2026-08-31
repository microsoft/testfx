// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class CombinatorialMemberDataAttributeTests : TestContainer
{
    private static readonly ParameterInfo IntParameter = GetParameter(nameof(IntParameterStub));
    private static readonly ParameterInfo StringParameter = GetParameter(nameof(StringParameterStub));

    public void ReadsValuesFromPropertyFieldAndMethod()
    {
        new CombinatorialMemberDataAttribute(nameof(IntProperty)).GetValues(IntParameter).Should().Equal([1, 2]);
        new CombinatorialMemberDataAttribute(nameof(IntField)).GetValues(IntParameter).Should().Equal([3, 4]);
        new CombinatorialMemberDataAttribute(nameof(GetInts), 5, 2).GetValues(IntParameter).Should().Equal([5, 7]);
    }

    public void ReadsValuesFromExplicitMemberType()
    {
        var attribute = new CombinatorialMemberDataAttribute(nameof(ExternalValues.Strings))
        {
            MemberType = typeof(ExternalValues),
        };

        attribute.GetValues(StringParameter).Should().Equal(["a", "b"]);
    }

    public void SelectsPublicStaticMethodWhenInstanceOverloadAlsoMatches()
    {
        var attribute = new CombinatorialMemberDataAttribute(nameof(OverloadedValues.GetValues), "value")
        {
            MemberType = typeof(OverloadedValues),
        };

        attribute.GetValues(IntParameter).Should().Equal([1, 2]);
    }

    public void SelectsMostSpecificCompatibleMethod()
    {
        var attribute = new CombinatorialMemberDataAttribute(nameof(SpecificOverloads.GetValues), "value")
        {
            MemberType = typeof(SpecificOverloads),
        };

        attribute.GetValues(IntParameter).Should().Equal([2]);
    }

    public void AllowsNullForNullableMethodParameter()
    {
        var attribute = new CombinatorialMemberDataAttribute(nameof(GetValuesForNullable), [null]);

        attribute.GetValues(IntParameter).Should().Equal([1]);
    }

    public void FindsEligibleInheritedMembersHiddenByInstanceMembers()
    {
        new CombinatorialMemberDataAttribute(nameof(HiddenMembers.IntProperty))
        {
            MemberType = typeof(HiddenMembers),
        }.GetValues(IntParameter).Should().Equal([1, 2]);
        new CombinatorialMemberDataAttribute(nameof(HiddenMembers.IntField))
        {
            MemberType = typeof(HiddenMembers),
        }.GetValues(IntParameter).Should().Equal([3, 4]);
    }

    public void FlattensObjectArrayRows()
        => new CombinatorialMemberDataAttribute(nameof(Rows)).GetValues(IntParameter).Should().Equal([1, 2, 3]);

    public void RejectsMissingNonGenericNestedAndIncompatibleMembers()
    {
        Action missing = () => new CombinatorialMemberDataAttribute("Missing").GetValues(IntParameter);
        Action nonGeneric = () => new CombinatorialMemberDataAttribute(nameof(NonGenericValues)).GetValues(IntParameter);
        Action nested = () => new CombinatorialMemberDataAttribute(nameof(NestedValues)).GetValues(IntParameter);
        Action incompatible = () => new CombinatorialMemberDataAttribute(nameof(StringProperty)).GetValues(IntParameter);

        missing.Should().Throw<ArgumentException>().WithMessage("*Could not find*");
        nonGeneric.Should().Throw<ArgumentException>().WithMessage("*IEnumerable<T>*");
        nested.Should().Throw<ArgumentException>().WithMessage("*not supported*");
        incompatible.Should().Throw<ArgumentException>().WithMessage("*not compatible*");
    }

    public void ClassDataCreatesSourceWithArgumentsAndFlattensRows()
    {
        var attribute = new CombinatorialClassDataAttribute(typeof(IntegerRows), 3);

        attribute.GetValues(IntParameter).Should().Equal([0, 1, 2]);
    }

    public void ClassDataRejectsInvalidTypesAndConstructorArguments()
    {
        Action invalidType = () => _ = new CombinatorialClassDataAttribute(typeof(object));
        Action invalidArguments = () => _ = new CombinatorialClassDataAttribute(typeof(IntegerRows), "wrong");

        invalidType.Should().Throw<InvalidOperationException>().WithMessage("*IEnumerable*");
        invalidArguments.Should().Throw<InvalidOperationException>().WithMessage("*Failed to create*");
    }

    public void ClassDataDoesNotReportEnumerationFailuresAsConstructorFailures()
    {
        Action action = () => _ = new CombinatorialClassDataAttribute(typeof(ThrowingRows));

        action.Should().Throw<NotSupportedException>().WithMessage("Enumeration failed.");
    }

    public void DataProviderAttributesOnlyApplyToParametersAndDisallowDuplicates()
    {
        AttributeUsageAttribute memberUsage = typeof(CombinatorialMemberDataAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        AttributeUsageAttribute classUsage = typeof(CombinatorialClassDataAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        memberUsage.ValidOn.Should().Be(AttributeTargets.Parameter);
        memberUsage.AllowMultiple.Should().BeFalse();
        classUsage.ValidOn.Should().Be(AttributeTargets.Parameter);
        classUsage.AllowMultiple.Should().BeFalse();
    }

    public static IEnumerable<int> IntProperty => [1, 2];

    public static readonly IEnumerable<int> IntField = [3, 4];

    public static IEnumerable<string> StringProperty => ["x"];

    public static IEnumerable<object[]> Rows => [[1], [2, 3]];

    public static IEnumerable<int> GetInts(int start, int step) => new[] { start, start + step };

    public static IEnumerable<int> GetValuesForNullable(int? value) => [value ?? 1];

    public static IEnumerable NonGenericValues => new ArrayList { 1, 2 };

    public static IEnumerable<IEnumerable<int>> NestedValues => [[1], [2]];

    private static ParameterInfo GetParameter(string methodName)
        => typeof(CombinatorialMemberDataAttributeTests)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .GetParameters()[0];

    private static void IntParameterStub(int value)
    {
    }

    private static void StringParameterStub(string value)
    {
    }

    public static class ExternalValues
    {
        public static IEnumerable<string> Strings => ["a", "b"];
    }

    public sealed class OverloadedValues
    {
        public IEnumerable<int> GetValues(object value) => [0];

        public static IEnumerable<int> GetValues(string value) => [1, 2];
    }

    public static class SpecificOverloads
    {
        public static IEnumerable<int> GetValues(object value) => [1];

        public static IEnumerable<int> GetValues(string value) => [2];
    }

    public class BaseMembers
    {
        public static IEnumerable<int> IntProperty => [1, 2];

        public static readonly IEnumerable<int> IntField = [3, 4];
    }

#pragma warning disable CS0108, SA1401 // Intentionally hide eligible base members with ineligible members.
    public sealed class HiddenMembers : BaseMembers
    {
        public IEnumerable<int> IntProperty => [5, 6];

        public readonly IEnumerable<int> IntField = [7, 8];
    }
#pragma warning restore CS0108, SA1401

    public sealed class IntegerRows : IEnumerable<object[]>
    {
        private readonly int _count;

        public IntegerRows(int count) => _count = count;

        public IEnumerator<object[]> GetEnumerator()
            => Enumerable.Range(0, _count).Select(value => new object[] { value }).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class ThrowingRows : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => throw new NotSupportedException("Enumeration failed.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
