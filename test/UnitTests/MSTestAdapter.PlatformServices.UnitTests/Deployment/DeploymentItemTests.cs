// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;

public class DeploymentItemTests : TestContainer
{
    public void EqualsShouldReturnFalseIfOtherItemIsNull()
    {
        DeploymentItem item = new("e:\\temp\\temp1.dll");

        item.Equals(null).Should().BeFalse();
    }

    public void EqualsShouldReturnFalseIfOtherItemIsNotDeploymentItem()
    {
        DeploymentItem item = new("e:\\temp\\temp1.dll");

        item.Equals(new DeploymentItemTests()).Should().BeFalse();
    }

    public void EqualsShouldReturnFalseIfSourcePathIsDifferent()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll");
        DeploymentItem item2 = new("e:\\temp\\temp2.dll");

        item1.Equals(item2).Should().BeFalse();
    }

    public void EqualsShouldReturnFalseIfRelativeOutputDirectoryIsDifferent()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "foo2");

        item1.Equals(item2).Should().BeFalse();
    }

    public void EqualsShouldReturnTrueIfSourcePathDiffersByCase()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll");
        DeploymentItem item2 = new("e:\\temp\\Temp1.dll");

        item1.Equals(item2).Should().BeTrue();
    }

    public void EqualsShouldReturnTrueIfRelativeOutputDirectoryDiffersByCase()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "Foo1");

        item1.Equals(item2).Should().BeTrue();
    }

    public void EqualsShouldReturnTrueIfSourceAndRelativeOutputDirectoryAreSame()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "foo1");

        item1.Equals(item2).Should().BeTrue();
    }

    public void GetHashCodeShouldConsiderSourcePathAndRelativeOutputDirectory()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        string relativeOutputDirectory = "foo1";
        DeploymentItem item = new(sourcePath, relativeOutputDirectory);

        item.GetHashCode().Should().Be(sourcePath.GetHashCode() + relativeOutputDirectory.GetHashCode());
    }

    public void ToStringShouldReturnDeploymentItemIfRelativeOutputDirectoryIsNotSpecified()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        DeploymentItem item = new(sourcePath);

        item.ToString().Should().Be(string.Format(CultureInfo.InvariantCulture, Resource.DeploymentItem, sourcePath));
    }

    public void ToStringShouldReturnDeploymentItemAndRelativeOutputDirectory()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        string relativeOutputDirectory = "foo1";
        DeploymentItem item = new(sourcePath, relativeOutputDirectory);

        item.ToString().Should().Be(string.Format(CultureInfo.InvariantCulture, Resource.DeploymentItemWithOutputDirectory, sourcePath, relativeOutputDirectory));
    }
}
