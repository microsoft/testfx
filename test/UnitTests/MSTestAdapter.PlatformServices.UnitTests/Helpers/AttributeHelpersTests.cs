// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public class AttributeHelpersTests : TestContainer
{
    // A controllable ConditionBaseAttribute for testing purposes.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    private sealed class TestConditionAttribute : ConditionBaseAttribute
    {
        private readonly bool _isConditionMet;

        public TestConditionAttribute(bool isConditionMet, string groupName = "TestGroup")
            : base(ConditionMode.Include)
        {
            _isConditionMet = isConditionMet;
            GroupName = groupName;
        }

        public override bool IsConditionMet => _isConditionMet;

        public override string GroupName { get; }
    }

    // ── Dummy methods decorated with attribute combinations ──────────────────

    private static void NoConditionMethod() { }

    [Ignore("unsatisfied reason")]
    private static void SingleUnsatisfiedConditionMethod() { }

    [TestCondition(true)]
    private static void SingleSatisfiedConditionMethod() { }

    // Same group: one unsatisfied followed by one satisfied → group is satisfied.
    [TestCondition(false, IgnoreMessage = "first")]
    [TestCondition(true)]
    private static void SameGroupOneSatisfiedMethod() { }

    // Same group: both unsatisfied with messages → first non-null message is returned.
    [TestCondition(false, IgnoreMessage = "first reason")]
    [TestCondition(false, IgnoreMessage = "second reason")]
    private static void SameGroupBothUnsatisfiedWithMessagesMethod() { }

    // Same group: both unsatisfied, first has null message, second has a message.
    [TestCondition(false)]
    [TestCondition(false, IgnoreMessage = "actual reason")]
    private static void SameGroupFirstMessageNullMethod() { }

    // Different groups: group A unsatisfied, group B satisfied → test is ignored (group A fails).
    [TestCondition(false, "GroupA", IgnoreMessage = "group A reason")]
    [TestCondition(true, "GroupB")]
    private static void DifferentGroupsOneUnsatisfiedMethod() { }

    // Different groups: both satisfied → test is not ignored.
    [TestCondition(true, "GroupA")]
    [TestCondition(true, "GroupB")]
    private static void DifferentGroupsBothSatisfiedMethod() { }

    // ── Tests ────────────────────────────────────────────────────────────────

    public void IsIgnored_NoConditionAttribute_ReturnsFalse()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(NoConditionMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeFalse();
        message.Should().BeNull();
    }

    public void IsIgnored_SingleUnsatisfiedCondition_ReturnsTrue()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(SingleUnsatisfiedConditionMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeTrue();
        message.Should().Be("unsatisfied reason");
    }

    public void IsIgnored_SingleSatisfiedCondition_ReturnsFalse()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(SingleSatisfiedConditionMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeFalse();
        message.Should().BeNull();
    }

    public void IsIgnored_SameGroupOneSatisfied_ReturnsFalse()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(SameGroupOneSatisfiedMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeFalse();
        message.Should().BeNull();
    }

    public void IsIgnored_SameGroupBothUnsatisfied_ReturnsTrueWithFirstMessage()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(SameGroupBothUnsatisfiedWithMessagesMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeTrue();
        message.Should().Be("first reason");
    }

    public void IsIgnored_SameGroupFirstMessageNull_ReturnsFirstNonNullMessage()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(SameGroupFirstMessageNullMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeTrue();
        message.Should().Be("actual reason");
    }

    public void IsIgnored_DifferentGroupsOneUnsatisfied_ReturnsTrue()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(DifferentGroupsOneUnsatisfiedMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeTrue();
        message.Should().Be("group A reason");
    }

    public void IsIgnored_DifferentGroupsBothSatisfied_ReturnsFalse()
    {
        MethodInfo method = typeof(AttributeHelpersTests).GetMethod(nameof(DifferentGroupsBothSatisfiedMethod), BindingFlags.NonPublic | BindingFlags.Static)!;
        bool result = method.IsIgnored(out string? message);
        result.Should().BeFalse();
        message.Should().BeNull();
    }
}
