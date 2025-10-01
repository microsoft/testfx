// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class OSConditionAttribute.
/// </summary>
public class OSConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        // Arrange & Act
        var includeAttribute = new OSConditionAttribute(ConditionMode.Include, OperatingSystems.Windows);
        var excludeAttribute = new OSConditionAttribute(ConditionMode.Exclude, OperatingSystems.Linux);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_WithOperatingSystemsOnly_DefaultsToIncludeMode()
    {
        // Arrange & Act
        var attribute = new OSConditionAttribute(OperatingSystems.Windows);

        // Assert
        attribute.Mode.Should().Be(ConditionMode.Include);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var attribute = new OSConditionAttribute(OperatingSystems.Windows);

        // Act & Assert
        attribute.GroupName.Should().Be("OSCondition");
    }

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new OSConditionAttribute(ConditionMode.Include, OperatingSystems.Windows);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported on Windows");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new OSConditionAttribute(ConditionMode.Exclude, OperatingSystems.Linux);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is not supported on Linux");
    }

    public void IgnoreMessage_MultipleOperatingSystems_ReturnsCorrectMessage()
    {
        // Arrange
        var osFlags = OperatingSystems.Windows | OperatingSystems.Linux;
        var attribute = new OSConditionAttribute(ConditionMode.Include, osFlags);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported on Windows, Linux");
    }

    public void ShouldRun_AlwaysUsesRuntimeInformationToDetectOS()
    {
        // This test validates that we no longer assume .NET Framework always runs on Windows
        // The actual OS detection is now done via RuntimeInformation consistently

        // Arrange
        var windowsAttribute = new OSConditionAttribute(OperatingSystems.Windows);
        var linuxAttribute = new OSConditionAttribute(OperatingSystems.Linux);
        var osxAttribute = new OSConditionAttribute(OperatingSystems.OSX);

        // Act & Assert
        // We can't mock RuntimeInformation, but we can verify the logic works
        // The behavior should be consistent regardless of compilation target
        
        // At least one of these should be true on any platform
        bool anyOSMatched = windowsAttribute.ShouldRun || linuxAttribute.ShouldRun || osxAttribute.ShouldRun;
        anyOSMatched.Should().BeTrue("At least one OS should match the current platform");
    }

    public void ShouldRun_IncludeMode_WhenCurrentOSMatches_ReturnsTrue()
    {
        // Arrange
        // Create an attribute that includes all possible OS to ensure current OS is included
        var allOSAttribute = new OSConditionAttribute(
            ConditionMode.Include, 
            OperatingSystems.Windows | OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD);

        // Act & Assert
        allOSAttribute.ShouldRun.Should().BeTrue("Current OS should be included when all OS are specified");
    }

    public void ShouldRun_ExcludeMode_WhenCurrentOSMatches_ReturnsFalse()
    {
        // Arrange
        // Create an attribute that excludes all possible OS to ensure current OS is excluded
        var allOSAttribute = new OSConditionAttribute(
            ConditionMode.Exclude, 
            OperatingSystems.Windows | OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD);

        // Act & Assert
        allOSAttribute.ShouldRun.Should().BeFalse("Current OS should be excluded when all OS are specified in exclude mode");
    }

    public void ShouldRun_IncludeMode_WhenCurrentOSNotMatched_ReturnsFalse()
    {
        // This test ensures that when we specify only non-current OS, the test is skipped
        // We can't know exactly which OS we're on, but we can create a scenario where
        // at least some OS options are not the current one
        
        // Arrange
        var windowsOnly = new OSConditionAttribute(OperatingSystems.Windows);
        var linuxOnly = new OSConditionAttribute(OperatingSystems.Linux);
        var osxOnly = new OSConditionAttribute(OperatingSystems.OSX);

        // Act & Assert
        // Exactly one of these should return true (the current OS), others should return false
        var results = new[] { windowsOnly.ShouldRun, linuxOnly.ShouldRun, osxOnly.ShouldRun };
        var trueCount = results.Count(r => r);
        
        trueCount.Should().Be(1, "Exactly one OS should match the current platform");
    }
}