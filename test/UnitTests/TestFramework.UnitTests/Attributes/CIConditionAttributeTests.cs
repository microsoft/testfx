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

    public void IsConditionMet_UsesLiveCIEnvironmentWithoutInvertingExcludeMode()
    {
        string[] variables =
        [
            "TF_BUILD", "GITHUB_ACTIONS", "APPVEYOR", "CI", "TRAVIS", "CIRCLECI",
            "CODEBUILD_BUILD_ID", "AWS_REGION", "BUILD_ID", "BUILD_URL", "PROJECT_ID",
            "TEAMCITY_VERSION", "JB_SPACE_API_URL",
        ];
        Dictionary<string, string?> original = variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            var include = new CIConditionAttribute(ConditionMode.Include);
            var exclude = new CIConditionAttribute(ConditionMode.Exclude);
            include.IsConditionMet.Should().BeFalse();
            exclude.IsConditionMet.Should().BeFalse();

            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
            include.IsConditionMet.Should().BeTrue();
            exclude.IsConditionMet.Should().BeTrue();
            include.Mode.Should().Be(ConditionMode.Include);
            exclude.Mode.Should().Be(ConditionMode.Exclude);
            include.GroupName.Should().Be("CIConditionAttribute");

            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "false");
            include.IsConditionMet.Should().BeFalse();
            exclude.IsConditionMet.Should().BeFalse();
        }
        finally
        {
            foreach (KeyValuePair<string, string?> entry in original)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
