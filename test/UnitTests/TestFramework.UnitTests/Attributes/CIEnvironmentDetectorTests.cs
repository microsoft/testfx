// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Moq;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for <see cref="CIEnvironmentDetector"/>.
/// </summary>
public class CIEnvironmentDetectorTests : TestContainer
{
    public void IsCIEnvironment_WhenNotInCI_ReturnsFalse()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeFalse();
    }

    public void IsCIEnvironment_WhenInCI_GitHub_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_AzurePipelines_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_AppVeyor_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("APPVEYOR")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_Travis_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TRAVIS")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_CircleCI_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CIRCLECI")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_Generic_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("true");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_TeamCity_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("TEAMCITY_VERSION")).Returns("2023.11");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_Jenkins_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("123");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_URL")).Returns("http://jenkins.example.com/job/test/123/");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_AWSCodeBuild_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CODEBUILD_BUILD_ID")).Returns("codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("AWS_REGION")).Returns("us-east-1");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_GoogleCloudBuild_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("abc-123-def-456");
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("PROJECT_ID")).Returns("my-project");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_WhenInCI_JetBrainsSpace_ReturnsTrue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("JB_SPACE_API_URL")).Returns("https://mycompany.jetbrains.space");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert
        detector.IsCIEnvironment().Should().BeTrue();
    }

    public void IsCIEnvironment_Jenkins_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("123");
        // BUILD_URL not set - should return null by default
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        detector.IsCIEnvironment().Should().BeFalse();
    }

    public void IsCIEnvironment_AWSCodeBuild_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CODEBUILD_BUILD_ID")).Returns("codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        // AWS_REGION not set - should return null by default
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        detector.IsCIEnvironment().Should().BeFalse();
    }

    public void IsCIEnvironment_GoogleCloudBuild_RequiresBothVariables()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("BUILD_ID")).Returns("abc-123-def-456");
        // PROJECT_ID not set - should return null by default
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since both variables are required
        detector.IsCIEnvironment().Should().BeFalse();
    }

    public void IsCIEnvironment_BooleanVariable_RequiresTrueValue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("false");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since value is false
        detector.IsCIEnvironment().Should().BeFalse();
    }

    public void IsCIEnvironment_BooleanVariable_RequiresValidBooleanValue()
    {
        // Arrange
        var mockEnvironment = new Mock<IEnvironment>();
        mockEnvironment.Setup(e => e.GetEnvironmentVariable("CI")).Returns("invalid");
        var detector = new CIEnvironmentDetector(mockEnvironment.Object);

        // Act & Assert - Should not detect as CI since value is not a valid boolean
        detector.IsCIEnvironment().Should().BeFalse();
    }
}
