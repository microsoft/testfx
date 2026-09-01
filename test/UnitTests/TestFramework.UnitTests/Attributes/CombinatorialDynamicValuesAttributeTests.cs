// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class CombinatorialDynamicValuesAttributeTests : TestContainer
{
    private static readonly ParameterInfo IntParameter = GetParameter(nameof(IntParameterStub));
    private static readonly ParameterInfo NullableIntParameter = GetParameter(nameof(NullableIntParameterStub));
    private static readonly ParameterInfo ObjectArrayParameter = GetParameter(nameof(ObjectArrayParameterStub));
    private static readonly ParameterInfo StringParameter = GetParameter(nameof(StringParameterStub));

    public void ReadsValuesFromPropertyFieldAndMethod()
    {
        new CombinatorialDynamicValuesAttribute(nameof(IntProperty)).GetValues(IntParameter).Should().Equal([1, 2]);
        new CombinatorialDynamicValuesAttribute(nameof(IntField)).GetValues(IntParameter).Should().Equal([3, 4]);
        new CombinatorialDynamicValuesAttribute(nameof(GetInts), 5, 2).GetValues(IntParameter).Should().Equal([5, 7]);
    }

    public void ReadsValuesFromExplicitMemberType()
    {
        var attribute = new CombinatorialDynamicValuesAttribute(nameof(ExternalValues.Strings))
        {
            MemberType = typeof(ExternalValues),
        };

        attribute.GetValues(StringParameter).Should().Equal(["a", "b"]);
    }

    public void SelectsPublicStaticMethodWhenInstanceOverloadAlsoMatches()
    {
        var attribute = new CombinatorialDynamicValuesAttribute(nameof(OverloadedValues.GetValues), "value")
        {
            MemberType = typeof(OverloadedValues),
        };

        attribute.GetValues(IntParameter).Should().Equal([1, 2]);
    }

    public void SelectsMostSpecificCompatibleMethod()
    {
        var stringAttribute = new CombinatorialDynamicValuesAttribute(nameof(SpecificOverloads.GetValues), "value")
        {
            MemberType = typeof(SpecificOverloads),
        };
        var intAttribute = new CombinatorialDynamicValuesAttribute(nameof(SpecificOverloads.GetValues), 1)
        {
            MemberType = typeof(SpecificOverloads),
        };

        stringAttribute.GetValues(IntParameter).Should().Equal([2]);
        intAttribute.GetValues(IntParameter).Should().Equal([3]);
    }

    public void IgnoresOpenGenericMethodsAndFindsInheritedSource()
    {
        var attribute = new CombinatorialDynamicValuesAttribute(nameof(GenericMethodValues.GetValues))
        {
            MemberType = typeof(GenericMethodValues),
        };

        attribute.GetValues(IntParameter).Should().Equal([3, 4]);
    }

    public void AllowsNullForNullableMethodParameter()
    {
        var attribute = new CombinatorialDynamicValuesAttribute(nameof(GetValuesForNullable), [null]);

        attribute.GetValues(IntParameter).Should().Equal([1]);
    }

    public void TreatsExplicitNullParamsArrayAsSingleMemberArgument()
    {
        ParameterInfo parameter = GetParameter(nameof(ExplicitNullMemberArgument));
        CombinatorialDynamicValuesAttribute attribute = parameter.GetCustomAttribute<CombinatorialDynamicValuesAttribute>()!;

        attribute.Arguments.Should().Equal([null]);
        attribute.GetValues(parameter).Should().Equal([1]);
    }

    public void AllowsNonNullForNullableMethodParameter()
    {
        var attribute = new CombinatorialDynamicValuesAttribute(nameof(GetValuesForNullable), 2);

        attribute.GetValues(IntParameter).Should().Equal([2]);
    }

    public void AllowsNonNullableMemberValuesForNullableParameter()
        => new CombinatorialDynamicValuesAttribute(nameof(IntProperty))
            .GetValues(NullableIntParameter)
            .Should().Equal([1, 2]);

    public void FindsEligibleInheritedMembersHiddenByInstanceMembers()
    {
        new CombinatorialDynamicValuesAttribute(nameof(HiddenMembers.IntProperty))
        {
            MemberType = typeof(HiddenMembers),
        }.GetValues(IntParameter).Should().Equal([1, 2]);
        new CombinatorialDynamicValuesAttribute(nameof(HiddenMembers.IntField))
        {
            MemberType = typeof(HiddenMembers),
        }.GetValues(IntParameter).Should().Equal([3, 4]);
    }

    public void TreatsEachObjectArrayRowAsOneCandidate()
    {
        object?[] values = new CombinatorialDynamicValuesAttribute(nameof(Rows)).GetValues(ObjectArrayParameter);

        values.Should().HaveCount(2);
        ((object[])values[0]!).Should().Equal([1]);
        ((object[])values[1]!).Should().Equal([2, 3]);
    }

    public void RejectsMissingNonGenericNestedAndIncompatibleMembers()
    {
        Action missing = () => new CombinatorialDynamicValuesAttribute("Missing").GetValues(IntParameter);
        Action nonGeneric = () => new CombinatorialDynamicValuesAttribute(nameof(NonGenericValues)).GetValues(IntParameter);
        Action nested = () => new CombinatorialDynamicValuesAttribute(nameof(NestedValues)).GetValues(IntParameter);
        Action incompatible = () => new CombinatorialDynamicValuesAttribute(nameof(StringProperty)).GetValues(IntParameter);

        missing.Should().Throw<ArgumentException>().WithMessage("*Could not find*");
        nonGeneric.Should().Throw<ArgumentException>().WithMessage("*IEnumerable<T>*");
        nested.Should().Throw<ArgumentException>().WithMessage("*not supported*");
        incompatible.Should().Throw<ArgumentException>().WithMessage("*not compatible*");
    }

    public void DataProviderAttributeOnlyAppliesToParametersAndDisallowsDuplicates()
    {
        AttributeUsageAttribute usage = typeof(CombinatorialDynamicValuesAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.Should().Be(AttributeTargets.Parameter);
        usage.AllowMultiple.Should().BeFalse();
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
        => typeof(CombinatorialDynamicValuesAttributeTests)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .GetParameters()[0];

    private static void IntParameterStub(int value)
    {
    }

    private static void NullableIntParameterStub(int? value)
    {
    }

    private static void ObjectArrayParameterStub(object[] value)
    {
    }

    private static void ExplicitNullMemberArgument(
        [CombinatorialDynamicValues(nameof(GetValuesForNullable), null)] int value)
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

        public static IEnumerable<int> GetValues(int value) => [3];

        public static IEnumerable<int> GetValues(int? value) => [4];
    }

    public class BaseMethodValues
    {
        public static IEnumerable<int> GetValues() => [3, 4];
    }

    public sealed class GenericMethodValues : BaseMethodValues
    {
        public static IEnumerable<int> GetValues<T>() => [5, 6];
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

}
