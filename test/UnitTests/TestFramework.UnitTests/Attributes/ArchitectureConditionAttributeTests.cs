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
        | TestArchitectures.S390x | TestArchitectures.LoongArch64 | TestArchitectures.Armv6 | TestArchitectures.Ppc64le
#if NET9_0_OR_GREATER
        | TestArchitectures.RiscV64
#endif
        ;

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

    // Derive the expected flag from the *name* of the current architecture rather than re-implementing the
    // integer→flag mapping, so this test doesn't silently mirror (and therefore can't validate) the production logic.
    // Enum.GetName returns null (rather than a numeric string) for a value missing from this TFM's Architecture enum,
    // so an unknown architecture fails fast instead of Enum.TryParse accepting a numeric string as a bogus flag.
    private static TestArchitectures GetCurrentArchitecture()
        => Enum.GetName(typeof(Architecture), RuntimeInformation.ProcessArchitecture) is { } name
            && Enum.TryParse(name, out TestArchitectures architecture)
            ? architecture
            : throw new InvalidOperationException($"Unknown process architecture: {RuntimeInformation.ProcessArchitecture}");

    public void ArchitectureMapping_AllSupportedValuesHaveDistinctDocumentedFlags()
    {
        Func<Architecture, TestArchitectures> map = typeof(ArchitectureConditionAttribute).GetMethod("MapArchitecture", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<Architecture, TestArchitectures>>();
        (Architecture Input, TestArchitectures Expected)[] cases =
        [
            (Architecture.X86, TestArchitectures.X86),
            (Architecture.X64, TestArchitectures.X64),
            (Architecture.Arm, TestArchitectures.Arm),
            (Architecture.Arm64, TestArchitectures.Arm64),
            (Architecture.Wasm, TestArchitectures.Wasm),
            (Architecture.S390x, TestArchitectures.S390x),
            (Architecture.LoongArch64, TestArchitectures.LoongArch64),
            (Architecture.Armv6, TestArchitectures.Armv6),
            (Architecture.Ppc64le, TestArchitectures.Ppc64le),
#if NET9_0_OR_GREATER
            (Architecture.RiscV64, TestArchitectures.RiscV64),
#endif
        ];
        foreach ((Architecture input, TestArchitectures expected) in cases)
        {
            map(input).Should().Be(expected);
        }

        cases.Select(pair => map(pair.Input)).Distinct().Should().HaveCount(cases.Length);
    }

    public void ArchitectureMapping_UnknownValuesRejectTheirOriginalValue()
    {
        Func<Architecture, TestArchitectures> map = typeof(ArchitectureConditionAttribute).GetMethod("MapArchitecture", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<Architecture, TestArchitectures>>();
        foreach (Architecture architecture in new[] { (Architecture)(-1), (Architecture)int.MaxValue })
        {
            Action action = () => map(architecture);
            ArgumentOutOfRangeException error = action.Should().ThrowExactly<ArgumentOutOfRangeException>().Which;
            error.ParamName.Should().Be("architecture");
            error.ActualValue.Should().Be(architecture);
        }
    }
}
#endif
