// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

public class MemberConditionAttributeTests : TestContainer
{
    #region Test helpers (condition members)

    private sealed class Conditions
    {
        public static bool TruePropertyValue => true;

        public static bool FalsePropertyValue => false;

        public static readonly bool TrueField = true;

#pragma warning disable CA1805 // explicit init illustrates intent in the test fixture
        public static readonly bool FalseField = false;
#pragma warning restore CA1805

        public static bool TrueMethod() => true;

        public static bool FalseMethod() => false;

        public static int NotABool => 42;

        public static int NotABoolMethod() => 42;

        public static bool WithParam(int _) => true;

        public bool InstanceProp => true;

        internal static bool InternalTrueProperty => true;
    }

    #endregion

    public void Constructor_DefaultMode_IsInclude()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.Mode.Should().Be(ConditionMode.Include);
        attribute.ConditionType.Should().Be(typeof(Conditions));
        attribute.ConditionMemberNames.Should().BeEquivalentTo([nameof(Conditions.TruePropertyValue)]);
    }

    public void Constructor_ExplicitMode_IsHonored()
    {
        var attribute = new MemberConditionAttribute(ConditionMode.Exclude, typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_NullType_Throws()
        => ((Action)(() => _ = new MemberConditionAttribute(null!, "Foo")))
            .Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("conditionType");

    public void Constructor_NullMemberName_Throws()
        => ((Action)(() => _ = new MemberConditionAttribute(typeof(Conditions), null!)))
            .Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("conditionMemberName");

    public void Constructor_NullAdditionalMemberNames_DoesNotThrow()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue), null!);
        attribute.ConditionMemberNames.Should().BeEquivalentTo([nameof(Conditions.TruePropertyValue)]);
    }

    public void Constructor_EmptyAdditionalMemberNames_Ok()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue), []);
        attribute.ConditionMemberNames.Should().BeEquivalentTo([nameof(Conditions.TruePropertyValue)]);
    }

    public void Constructor_WhitespaceMemberName_Throws()
        => ((Action)(() => _ = new MemberConditionAttribute(typeof(Conditions), "   ")))
            .Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("conditionMemberName");

    public void Constructor_WhitespaceAdditionalMemberName_Throws()
        => ((Action)(() => _ = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue), "   ")))
            .Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("additionalConditionMemberNames");

    public void IgnoreMessage_Include_HasExpectedText()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.IgnoreMessage.Should().Contain("only supported")
            .And.Contain(nameof(Conditions.TruePropertyValue))
            .And.Contain(typeof(Conditions).FullName!);
    }

    public void IgnoreMessage_Exclude_HasExpectedText()
    {
        var attribute = new MemberConditionAttribute(ConditionMode.Exclude, typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.IgnoreMessage.Should().Contain("not supported")
            .And.Contain(nameof(Conditions.TruePropertyValue))
            .And.Contain(typeof(Conditions).FullName!);
    }

    public void IgnoreMessage_MultipleMembers_ListsAllWithAnd()
    {
        var attribute = new MemberConditionAttribute(
            typeof(Conditions),
            nameof(Conditions.TruePropertyValue),
            nameof(Conditions.TrueField));

        attribute.IgnoreMessage.Should().Contain(nameof(Conditions.TruePropertyValue))
            .And.Contain(nameof(Conditions.TrueField))
            .And.Contain(" AND ");
    }

    public void IsConditionMet_StaticPublicProperty_ReturnsValue()
    {
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue)).IsConditionMet.Should().BeTrue();
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.FalsePropertyValue)).IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_StaticPublicField_ReturnsValue()
    {
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TrueField)).IsConditionMet.Should().BeTrue();
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.FalseField)).IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_StaticParameterlessMethod_ReturnsValue()
    {
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TrueMethod)).IsConditionMet.Should().BeTrue();
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.FalseMethod)).IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_NonPublicStaticProperty_ThrowsInvalidOperation()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), "InternalTrueProperty").IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*InternalTrueProperty*");

    public void IsConditionMet_MultipleMembers_AndsValues()
    {
        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue), nameof(Conditions.TrueField), nameof(Conditions.TrueMethod))
            .IsConditionMet.Should().BeTrue();

        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue), nameof(Conditions.FalseField))
            .IsConditionMet.Should().BeFalse();

        new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.FalsePropertyValue), nameof(Conditions.TrueField))
            .IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_MissingMember_ThrowsInvalidOperation()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), "DoesNotExist").IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DoesNotExist*");

    public void IsConditionMet_NonBoolProperty_ThrowsInvalidOperation()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.NotABool)).IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*static bool*");

    public void IsConditionMet_MethodWithParameters_FallsThroughAndThrows()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.WithParam)).IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*WithParam*");

    public void IsConditionMet_ParameterlessMethodWithNonBoolReturn_ThrowsInvalidOperation()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.NotABoolMethod)).IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*static parameterless bool method*");

    public void IsConditionMet_InstanceProperty_NotFoundForStaticLookup()
        => ((Func<bool>)(() => new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.InstanceProp)).IsConditionMet))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*InstanceProp*");

    public void GroupName_EncodesTypeAndMembers()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.GroupName.Should().Contain(nameof(MemberConditionAttribute))
            .And.Contain(typeof(Conditions).FullName!)
            .And.Contain(nameof(Conditions.TruePropertyValue));
    }

    public void GroupName_DifferentMembers_AreDifferentGroups()
    {
        var a = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));
        var b = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.FalsePropertyValue));

        a.GroupName.Should().NotBe(b.GroupName);
    }

    public void GroupName_SameTypeAndMembers_AreSameGroup()
    {
        var a = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));
        var b = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));

        a.GroupName.Should().Be(b.GroupName);
    }

    public void GroupName_DifferentMode_AreDifferentGroups()
    {
        var include = new MemberConditionAttribute(ConditionMode.Include, typeof(Conditions), nameof(Conditions.TruePropertyValue));
        var exclude = new MemberConditionAttribute(ConditionMode.Exclude, typeof(Conditions), nameof(Conditions.TruePropertyValue));

        include.GroupName.Should().NotBe(exclude.GroupName);
    }

    public void ConditionMemberNames_CannotBeDowncastToMutableArray()
    {
        var attribute = new MemberConditionAttribute(typeof(Conditions), nameof(Conditions.TruePropertyValue));

        attribute.ConditionMemberNames.Should().NotBeAssignableTo<string[]>();
    }
}
