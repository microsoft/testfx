// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class FrameworkConditionAttribute.
/// </summary>
public class FrameworkConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        // Arrange & Act
        var includeAttribute = new FrameworkConditionAttribute(ConditionMode.Include, Frameworks.NetFramework);
        var excludeAttribute = new FrameworkConditionAttribute(ConditionMode.Exclude, Frameworks.NetCore);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_WithFrameworkOnly_DefaultsToIncludeMode()
    {
        // Arrange & Act
        var attribute = new FrameworkConditionAttribute(Frameworks.Net);

        // Assert
        attribute.Mode.Should().Be(ConditionMode.Include);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, Frameworks.Net);

        // Act & Assert
        attribute.GroupName.Should().Be(nameof(FrameworkConditionAttribute));
    }

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, Frameworks.NetFramework);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported on NetFramework");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new FrameworkConditionAttribute(ConditionMode.Exclude, Frameworks.NetCore);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is not supported on NetCore");
    }

    public void IgnoreMessage_MultipleFrameworks_ReturnsCorrectMessage()
    {
        // Arrange
        var frameworks = Frameworks.NetFramework | Frameworks.NetCore;
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, frameworks);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported on NetFramework, NetCore");
    }

    public void ShouldRun_IncludeMode_CurrentFrameworkMatches_ReturnsTrue()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, currentFramework);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_ExcludeMode_CurrentFrameworkMatches_ReturnsTrue()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var attribute = new FrameworkConditionAttribute(ConditionMode.Exclude, currentFramework);

        // Act & Assert
        // ShouldRun returns true when the condition is detected (current framework matches)
        // The framework handles include/exclude logic separately
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_CurrentFrameworkDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var differentFramework = GetDifferentFramework(currentFramework);
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, differentFramework);

        // Act & Assert
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_ExcludeMode_CurrentFrameworkDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var differentFramework = GetDifferentFramework(currentFramework);
        var attribute = new FrameworkConditionAttribute(ConditionMode.Exclude, differentFramework);

        // Act & Assert
        // ShouldRun returns false when the condition is NOT detected
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_Net8OrGreater_OnNet8_ReturnsTrue()
    {
        // Arrange
        var attribute = new FrameworkConditionAttribute(Frameworks.Net8OrGreater);

        // Act & Assert
        // This test assumes we're running on .NET 8+ in the test environment
        if (Environment.Version.Major >= 8)
        {
            attribute.ShouldRun.Should().BeTrue();
        }
        else
        {
            attribute.ShouldRun.Should().BeFalse();
        }
    }

    public void ShouldRun_MultipleFrameworks_IncludesCurrent_ReturnsTrue()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var multipleFrameworks = currentFramework | Frameworks.NetFramework; // Include current plus another
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, multipleFrameworks);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_MultipleFrameworks_ExcludesCurrent_ReturnsFalse()
    {
        // Arrange
        var currentFramework = GetCurrentFrameworkEnum();
        var differentFramework = GetDifferentFramework(currentFramework);
        var attribute = new FrameworkConditionAttribute(ConditionMode.Include, differentFramework);

        // Act & Assert
        attribute.ShouldRun.Should().BeFalse();
    }

    private static Frameworks GetCurrentFrameworkEnum()
    {
        string frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        Version version = Environment.Version;

        if (frameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
        {
            return Frameworks.NetFramework;
        }

        if (frameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
        {
            return Frameworks.NetCore;
        }

        // .NET 5+
        Frameworks framework = Frameworks.Net;
        if (version.Major >= 6)
        {
            framework |= Frameworks.Net6OrGreater;
        }
        if (version.Major >= 7)
        {
            framework |= Frameworks.Net7OrGreater;
        }
        if (version.Major >= 8)
        {
            framework |= Frameworks.Net8OrGreater;
        }
        if (version.Major >= 9)
        {
            framework |= Frameworks.Net9OrGreater;
        }

        return framework;
    }

    private static Frameworks GetDifferentFramework(Frameworks current)
    {
        // Return a framework that's different from the current one
        if ((current & Frameworks.NetFramework) != 0)
        {
            return Frameworks.NetCore;
        }
        if ((current & Frameworks.NetCore) != 0)
        {
            return Frameworks.NetFramework;
        }
        if ((current & Frameworks.Net) != 0)
        {
            return Frameworks.NetFramework;
        }
        return Frameworks.NetCore;
    }
}