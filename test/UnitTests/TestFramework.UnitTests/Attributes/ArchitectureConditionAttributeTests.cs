// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ArchitectureConditionAttribute.
/// </summary>
public class ArchitectureConditionAttributeTests : TestContainer
{
    private const Architectures AllArchitectures =
        Architectures.X86 | Architectures.X64 | Architectures.Arm | Architectures.Arm64 | Architectures.Wasm
        | Architectures.S390x | Architectures.LoongArch64 | Architectures.Armv6 | Architectures.Ppc64le | Architectures.RiscV64;

    public void Constructor_SetsCorrectMode()
    {
        // Act
        var includeAttribute = new ArchitectureConditionAttribute(ConditionMode.Include, Architectures.X64);
        var excludeAttribute = new ArchitectureConditionAttribute(ConditionMode.Exclude, Architectures.X64);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_SingleArgument_DefaultsToIncludeMode()
    {
        // Act
        var attribute = new ArchitectureConditionAttribute(Architectures.X64);

        // Assert
        attribute.Mode.Should().Be(ConditionMode.Include);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(Architectures.X64);

        // Act & Assert
        attribute.GroupName.Should().Be("ArchitectureCondition");
    }

    // A different GroupName is what makes MSTest combine the two conditions with a logical AND,
    // so an architecture requirement can be composed with an OS requirement.
    public void GroupName_DiffersFromOSCondition()
        => new ArchitectureConditionAttribute(Architectures.X64).GroupName
            .Should().NotBe(new OSConditionAttribute(OperatingSystems.Windows).GroupName);

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(ConditionMode.Include, Architectures.X64);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be($"Test is only supported on {Architectures.X64}");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new ArchitectureConditionAttribute(ConditionMode.Exclude, Architectures.X64);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be($"Test is not supported on {Architectures.X64}");
    }

    public void IsConditionMet_WhenAllArchitecturesIncluded_ReturnsTrue()
    {
        // The current process architecture is necessarily one of the known architectures.
        var attribute = new ArchitectureConditionAttribute(AllArchitectures);

        attribute.IsConditionMet.Should().BeTrue();
    }

    public void IsConditionMet_WhenNoArchitecturesIncluded_ReturnsFalse()
    {
        var attribute = new ArchitectureConditionAttribute(default);

        attribute.IsConditionMet.Should().BeFalse();
    }

#if !NETFRAMEWORK
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

    private static Architectures GetCurrentArchitecture()
        => (int)System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            0 => Architectures.X86,
            1 => Architectures.X64,
            2 => Architectures.Arm,
            3 => Architectures.Arm64,
            4 => Architectures.Wasm,
            5 => Architectures.S390x,
            6 => Architectures.LoongArch64,
            7 => Architectures.Armv6,
            8 => Architectures.Ppc64le,
            9 => Architectures.RiscV64,
            _ => default,
        };
#endif
}
