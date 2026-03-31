// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class CIConditionAttribute.
/// </summary>
/// <remarks>
/// The CI detection logic is tested in <see cref="CIEnvironmentDetectorTests"/>.
/// </remarks>
public class CIConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        // Act
        var includeAttribute = new CIConditionAttribute(ConditionMode.Include);
        var excludeAttribute = new CIConditionAttribute(ConditionMode.Exclude);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        // Act & Assert
        attribute.GroupName.Should().Be(nameof(CIConditionAttribute));
    }

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported in CI environments");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var attribute = new CIConditionAttribute(ConditionMode.Exclude);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is not supported in CI environments");
    }
}
