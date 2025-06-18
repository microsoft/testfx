// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class CIConditionAttribute.
/// </summary>
public class CIConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        var includeAttribute = new CIConditionAttribute(ConditionMode.Include);
        var excludeAttribute = new CIConditionAttribute(ConditionMode.Exclude);

        Verify(includeAttribute.Mode == ConditionMode.Include);
        Verify(excludeAttribute.Mode == ConditionMode.Exclude);
    }

    public void GroupName_ReturnsCorrectValue()
    {
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        Verify(attribute.GroupName == nameof(CIConditionAttribute));
    }

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
    {
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        Verify(attribute.IgnoreMessage == "Test is only supported in CI environments");
    }

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
    {
        var attribute = new CIConditionAttribute(ConditionMode.Exclude);

        Verify(attribute.IgnoreMessage == "Test is not supported in CI environments");
    }

    public void ShouldRun_IncludeMode_WhenNotInCI_ReturnsFalse()
    {
        // Arrange - Clear all CI environment variables
        ClearCIEnvironmentVariables();
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        // Act & Assert
        Verify(!attribute.ShouldRun);
    }

    public void ShouldRun_ExcludeMode_WhenNotInCI_ReturnsTrue()
    {
        // Arrange - Clear all CI environment variables
        ClearCIEnvironmentVariables();
        var attribute = new CIConditionAttribute(ConditionMode.Exclude);

        // Act & Assert
        Verify(attribute.ShouldRun);
    }

    public void ShouldRun_IncludeMode_WhenInCI_GitHub_ReturnsTrue()
    {
        // Arrange - Set GitHub Actions environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
        }
    }

    public void ShouldRun_ExcludeMode_WhenInCI_GitHub_ReturnsFalse()
    {
        // Arrange - Set GitHub Actions environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Exclude);

        try
        {
            // Act & Assert
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_AzurePipelines_ReturnsTrue()
    {
        // Arrange - Set Azure Pipelines environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("TF_BUILD", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TF_BUILD", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_AppVeyor_ReturnsTrue()
    {
        // Arrange - Set AppVeyor environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("APPVEYOR", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPVEYOR", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_Travis_ReturnsTrue()
    {
        // Arrange - Set Travis CI environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("TRAVIS", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TRAVIS", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_CircleCI_ReturnsTrue()
    {
        // Arrange - Set CircleCI environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CIRCLECI", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CIRCLECI", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_Generic_ReturnsTrue()
    {
        // Arrange - Set generic CI environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CI", "true");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_TeamCity_ReturnsTrue()
    {
        // Arrange - Set TeamCity environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("TEAMCITY_VERSION", "2023.11");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEAMCITY_VERSION", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_Jenkins_ReturnsTrue()
    {
        // Arrange - Set Jenkins environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("BUILD_ID", "123");
        Environment.SetEnvironmentVariable("BUILD_URL", "http://jenkins.example.com/job/test/123/");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_ID", null);
            Environment.SetEnvironmentVariable("BUILD_URL", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_AWSCodeBuild_ReturnsTrue()
    {
        // Arrange - Set AWS CodeBuild environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CODEBUILD_BUILD_ID", "codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEBUILD_BUILD_ID", null);
            Environment.SetEnvironmentVariable("AWS_REGION", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_GoogleCloudBuild_ReturnsTrue()
    {
        // Arrange - Set Google Cloud Build environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("BUILD_ID", "abc-123-def-456");
        Environment.SetEnvironmentVariable("PROJECT_ID", "my-project");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_ID", null);
            Environment.SetEnvironmentVariable("PROJECT_ID", null);
        }
    }

    public void ShouldRun_IncludeMode_WhenInCI_JetBrainsSpace_ReturnsTrue()
    {
        // Arrange - Set JetBrains Space environment
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("JB_SPACE_API_URL", "https://mycompany.jetbrains.space");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert
            Verify(attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JB_SPACE_API_URL", null);
        }
    }

    public void ShouldRun_Jenkins_RequiresBothVariables()
    {
        // Arrange - Set only one Jenkins variable
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("BUILD_ID", "123");
        // BUILD_URL not set
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert - Should not detect as CI since both variables are required
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_ID", null);
        }
    }

    public void ShouldRun_AWSCodeBuild_RequiresBothVariables()
    {
        // Arrange - Set only one AWS CodeBuild variable
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CODEBUILD_BUILD_ID", "codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE");
        // AWS_REGION not set
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert - Should not detect as CI since both variables are required
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEBUILD_BUILD_ID", null);
        }
    }

    public void ShouldRun_GoogleCloudBuild_RequiresBothVariables()
    {
        // Arrange - Set only one Google Cloud Build variable
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("BUILD_ID", "abc-123-def-456");
        // PROJECT_ID not set
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert - Should not detect as CI since both variables are required
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_ID", null);
        }
    }

    public void ShouldRun_BooleanVariable_RequiresTrueValue()
    {
        // Arrange - Set CI variable to false
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CI", "false");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert - Should not detect as CI since value is false
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", null);
        }
    }

    public void ShouldRun_BooleanVariable_RequiresValidBooleanValue()
    {
        // Arrange - Set CI variable to invalid boolean
        ClearCIEnvironmentVariables();
        Environment.SetEnvironmentVariable("CI", "invalid");
        var attribute = new CIConditionAttribute(ConditionMode.Include);

        try
        {
            // Act & Assert - Should not detect as CI since value is not a valid boolean
            Verify(!attribute.ShouldRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", null);
        }
    }

    /// <summary>
    /// Helper method to clear all known CI environment variables to ensure clean test state.
    /// </summary>
    private static void ClearCIEnvironmentVariables()
    {
        // Boolean variables
        Environment.SetEnvironmentVariable("TF_BUILD", null);
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
        Environment.SetEnvironmentVariable("APPVEYOR", null);
        Environment.SetEnvironmentVariable("CI", null);
        Environment.SetEnvironmentVariable("TRAVIS", null);
        Environment.SetEnvironmentVariable("CIRCLECI", null);

        // If-non-null variables
        Environment.SetEnvironmentVariable("TEAMCITY_VERSION", null);
        Environment.SetEnvironmentVariable("JB_SPACE_API_URL", null);

        // All-not-null variables
        Environment.SetEnvironmentVariable("CODEBUILD_BUILD_ID", null);
        Environment.SetEnvironmentVariable("AWS_REGION", null);
        Environment.SetEnvironmentVariable("BUILD_ID", null);
        Environment.SetEnvironmentVariable("BUILD_URL", null);
        Environment.SetEnvironmentVariable("PROJECT_ID", null);
    }
}
