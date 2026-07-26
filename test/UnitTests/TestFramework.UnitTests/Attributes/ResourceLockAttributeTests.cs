// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

/// <summary>
/// Tests for <see cref="ResourceLockAttribute"/> and <see cref="WellKnownResources"/>.
/// </summary>
public class ResourceLockAttributeTests : TestContainer
{
    public void Constructor_SetsResource_AndDefaultsToReadWrite()
    {
        var attribute = new ResourceLockAttribute("my-resource");

        attribute.Resource.Should().Be("my-resource");
        attribute.Mode.Should().Be(ResourceAccessMode.ReadWrite);
    }

    public void Mode_CanBeSetToRead()
    {
        var attribute = new ResourceLockAttribute("my-resource") { Mode = ResourceAccessMode.Read };

        attribute.Mode.Should().Be(ResourceAccessMode.Read);
    }

    public void WellKnownResources_AreDistinctNonEmptyKeys()
    {
        string[] keys =
        [
            WellKnownResources.CurrentDirectory,
            WellKnownResources.EnvironmentVariables,
            WellKnownResources.Console,
        ];

        keys.Should().OnlyHaveUniqueItems();
        keys.Should().NotContain(k => string.IsNullOrEmpty(k));
    }

    public void WellKnownResources_HaveTheirExactPublishedValues()
    {
        // These strings are permanent public API: conflict detection is plain string equality, so changing a
        // value after shipping would silently stop matching every user key that spells the old one, and two
        // tests intending to share a resource would quietly stop being serialized. Pin the exact values so a
        // rename or a swap between constants fails here rather than in someone's flaky suite.
        WellKnownResources.CurrentDirectory.Should().Be("System.Environment.CurrentDirectory");
        WellKnownResources.EnvironmentVariables.Should().Be("System.Environment.Variables");
        WellKnownResources.Console.Should().Be("System.Console");
    }

    public void Constructor_WhenResourceIsNull_Throws()
    {
        Action act = static () => _ = new ResourceLockAttribute(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resource");
    }

    public void Constructor_WhenResourceIsEmptyOrWhitespace_Throws()
    {
        // An empty key is not merely useless: because conflict detection is plain string equality it would
        // become a shared key that silently serializes every other test that also got an empty key.
        Action empty = static () => _ = new ResourceLockAttribute(string.Empty);
        empty.Should().Throw<ArgumentException>().WithParameterName("resource");

        Action whitespace = static () => _ = new ResourceLockAttribute("   ");
        whitespace.Should().Throw<ArgumentException>().WithParameterName("resource");
    }

    public void Attribute_IsInherited_MatchingDoNotParallelize()
    {
        // Inheriting fails closed (a derived class keeps a lock it may need); not inheriting would fail open.
        AttributeUsageAttribute usage = typeof(ResourceLockAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.Inherited.Should().BeTrue();
        usage.AllowMultiple.Should().BeTrue();
        usage.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Method);
    }

    public void DefaultAccessMode_IsReadWrite_SoUnspecifiedValuesFailClosed()
        => default(ResourceAccessMode).Should().Be(ResourceAccessMode.ReadWrite);
}
