// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ArchitectureConditionAttribute.
/// </summary>
public class ArchitectureConditionAttributeTests : TestContainer
{
    private const TestArchitectures AllArchitectures =
        TestArchitectures.X86 | TestArchitectures.X64 | TestArchitectures.Arm | TestArchitectures.Arm64 | TestArchitectures.Wasm
        | TestArchitectures.S390x | TestArchitectures.LoongArch64 | TestArchitectures.Armv6 | TestArchitectures.Ppc64le | TestArchitectures.RiscV64;

    public void Constructor_SetsCorrectMode()
    {
        // Act
        var includeAttribute = new ArchitectureConditionAttribute(ConditionMode.Include, TestArchitectures.X64);
        var excludeAttribute = new ArchitectureConditionAttribute(ConditionMode.Exclude, TestArchitectures.X64);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_SingleArgument_DefaultsToIncludeMode()
    {
        // Act
        var attribute = new ArchitectureConditionAttribute(TestArchitectures.X64);

        // Assert
        attribute.Mode.Should().Be(ConditionMode.Include);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(TestArchitectures.X64);

        // Act & Assert
        attribute.GroupName.Should().Be("ArchitectureCondition");
    }

    // A different GroupName is what makes MSTest combine the two conditions with a logical AND,
    // so an architecture requirement can be composed with an OS requirement.
    public void GroupName_DiffersFromOSCondition()
        => new ArchitectureConditionAttribute(TestArchitectures.X64).GroupName
            .Should().NotBe(new OSConditionAttribute(OperatingSystems.Windows).GroupName);

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(ConditionMode.Include, TestArchitectures.X64);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be($"Test is only supported on {TestArchitectures.X64}");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(ConditionMode.Exclude, TestArchitectures.X64);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be($"Test is not supported on {TestArchitectures.X64}");
    }

    public void IsConditionMet_WhenAllArchitecturesIncluded_ReturnsTrue()
    {
        // The current process architecture is necessarily one of the known TestArchitectures.
        var attribute = new ArchitectureConditionAttribute(AllArchitectures);

        attribute.IsConditionMet.Should().BeTrue();
    }

    public void IsConditionMet_WhenNoArchitecturesIncluded_ReturnsFalse()
    {
        var attribute = new ArchitectureConditionAttribute(default);

        attribute.IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_WhenCurrentArchitectureMatches_ReturnsTrue()
    {
        var attribute = new ArchitectureConditionAttribute(GetCurrentArchitecture());

        attribute.IsConditionMet.Should().BeTrue();
    }

    public void IsConditionMet_WhenCurrentArchitectureExcludedFromSet_ReturnsFalse()
    {
        var attribute = new ArchitectureConditionAttribute(AllArchitectures & ~GetCurrentArchitecture());

        attribute.IsConditionMet.Should().BeFalse();
    }

    private static TestArchitectures GetCurrentArchitecture()
        => (int)System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            0 => TestArchitectures.X86,
            1 => TestArchitectures.X64,
            2 => TestArchitectures.Arm,
            3 => TestArchitectures.Arm64,
            4 => TestArchitectures.Wasm,
            5 => TestArchitectures.S390x,
            6 => TestArchitectures.LoongArch64,
            7 => TestArchitectures.Armv6,
            8 => TestArchitectures.Ppc64le,
            9 => TestArchitectures.RiscV64,
            _ => default,
        };
}
#endif
