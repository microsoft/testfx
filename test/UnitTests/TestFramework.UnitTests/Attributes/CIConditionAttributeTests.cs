// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Moq;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class CIConditionAttribute.
/// </summary>
public class CIConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);

        // Act
        var includeAttribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);
        var excludeAttribute = new CIConditionAttribute(ConditionMode.Exclude, mockEnvironment.Object);

        // Assert
        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.GroupName.Should().Be(nameof(CIConditionAttribute));
    }

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is only supported in CI environments");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        var attribute = new CIConditionAttribute(ConditionMode.Exclude, mockEnvironment.Object);

        // Act & Assert
        attribute.IgnoreMessage.Should().Be("Test is not supported in CI environments");
    }

    public void ShouldRun_IncludeMode_WhenNotInCI_ReturnsFalse()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_ExcludeMode_WhenNotInCI_ReturnsFalse()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        var attribute = new CIConditionAttribute(ConditionMode.Exclude, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_IncludeMode_WhenInCI_GitHub_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_ExcludeMode_WhenInCI_GitHub_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Exclude, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_AzurePipelines_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_AppVeyor_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("APPVEYOR")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_Travis_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TRAVIS")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_CircleCI_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CIRCLECI")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_Generic_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("true");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_TeamCity_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TEAMCITY_VERSION")).Returns("2023.11");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_Jenkins_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("123");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_URL")).Returns("http://jenkins.example.com/job/test/123/");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_AWSCodeBuild_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CODEBUILD_BUILD_ID")).Returns("codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("AWS_REGION")).Returns("us-east-1");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_GoogleCloudBuild_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("abc-123-def-456");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("PROJECT_ID")).Returns("my-project");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_IncludeMode_WhenInCI_JetBrainsSpace_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("JB_SPACE_API_URL")).Returns("https://mycompany.jetbrains.space");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert
        attribute.ShouldRun.Should().BeTrue();
    }

    public void ShouldRun_Jenkins_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("123");
        // BUILD_URL not set - should return null by default
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_AWSCodeBuild_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CODEBUILD_BUILD_ID")).Returns("codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        // AWS_REGION not set - should return null by default
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_GoogleCloudBuild_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("abc-123-def-456");
        // PROJECT_ID not set - should return null by default
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_BooleanVariable_RequiresTrueValue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("false");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since value is false
        attribute.ShouldRun.Should().BeFalse();
    }

    public void ShouldRun_BooleanVariable_RequiresValidBooleanValue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("invalid");
        var attribute = new CIConditionAttribute(ConditionMode.Include, mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since value is not a valid boolean
        attribute.ShouldRun.Should().BeFalse();
    }
}
