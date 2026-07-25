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

    public void DefaultAccessMode_IsReadWrite_SoUnspecifiedValuesFailClosed()
        => default(ResourceAccessMode).Should().Be(ResourceAccessMode.ReadWrite);
}
