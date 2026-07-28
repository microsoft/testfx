// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

/// <summary>
/// Tests for <see cref="DependsOnAttribute"/>.
/// </summary>
public class DependsOnAttributeTests : TestContainer
{
    public void Constructor_WithMethodName_TargetsAMethodOfTheSameClass()
    {
        var attribute = new DependsOnAttribute("Setup");

        attribute.TestMethodName.Should().Be("Setup");
        attribute.TestClass.Should().BeNull();
        attribute.ProceedOnFailure.Should().BeFalse();
    }

    public void Constructor_WithType_TargetsEveryTestOfThatClass()
    {
        var attribute = new DependsOnAttribute(typeof(DependsOnAttributeTests));

        attribute.TestClass.Should().Be<DependsOnAttributeTests>();

        // A null method name is what distinguishes "every test of the class" from "this one test".
        attribute.TestMethodName.Should().BeNull();
    }

    public void Constructor_WithTypeAndMethodName_TargetsThatMethodOfThatClass()
    {
        var attribute = new DependsOnAttribute(typeof(DependsOnAttributeTests), "Setup");

        attribute.TestClass.Should().Be<DependsOnAttributeTests>();
        attribute.TestMethodName.Should().Be("Setup");
    }

    public void ProceedOnFailure_CanBeSet()
    {
        // The default is asserted here too, not only in the constructor tests: without it, a property that
        // ignored its setter and always returned true would still satisfy this test.
        new DependsOnAttribute("Setup").ProceedOnFailure.Should().BeFalse();

        var attribute = new DependsOnAttribute("Setup") { ProceedOnFailure = true };

        attribute.ProceedOnFailure.Should().BeTrue();
    }

    public void Constructor_WhenMethodNameIsNull_Throws()
    {
        Action nameOnly = () => _ = new DependsOnAttribute((string)null!);
        nameOnly.Should().Throw<ArgumentNullException>();

        Action withType = () => _ = new DependsOnAttribute(typeof(DependsOnAttributeTests), null!);
        withType.Should().Throw<ArgumentNullException>();
    }

    public void Constructor_WhenMethodNameIsEmptyOrWhitespace_Throws()
    {
        // An empty target is never intentional and would otherwise become an edge that silently matches
        // nothing, so it is rejected at the source rather than warned about at run time.
        Action empty = () => _ = new DependsOnAttribute(string.Empty);
        empty.Should().Throw<ArgumentException>();

        Action whitespace = () => _ = new DependsOnAttribute("   ");
        whitespace.Should().Throw<ArgumentException>();
    }

    public void Constructor_WhenTypeIsNull_Throws()
    {
        Action typeOnly = () => _ = new DependsOnAttribute((Type)null!);
        typeOnly.Should().Throw<ArgumentNullException>();

        Action withMethod = () => _ = new DependsOnAttribute(null!, "Setup");
        withMethod.Should().Throw<ArgumentNullException>();
    }

    public void Attribute_AllowsMultiple_SoATestCanDeclareSeveralPrerequisites()
    {
        AttributeUsageAttribute usage = typeof(DependsOnAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Fan-in is expressed by repeating the attribute, so AllowMultiple is part of the contract.
        usage.AllowMultiple.Should().BeTrue();

        // Deliberately not inherited: a dependency states one concrete test's prerequisites, and
        // re-pointing it at every derived class would invent edges nobody declared.
        usage.Inherited.Should().BeFalse();
        usage.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Method);
    }
}
