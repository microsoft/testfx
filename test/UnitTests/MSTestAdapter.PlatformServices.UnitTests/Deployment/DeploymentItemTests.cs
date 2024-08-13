// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Deployment;

public class DeploymentItemTests : TestContainer
{
    public void EqualsShouldReturnFalseIfOtherItemIsNull()
    {
        DeploymentItem item = new("e:\\temp\\temp1.dll");

        Verify(!item.Equals(null));
    }

    public void EqualsShouldReturnFalseIfOtherItemIsNotDeploymentItem()
    {
        DeploymentItem item = new("e:\\temp\\temp1.dll");

        Verify(!item.Equals(new DeploymentItemTests()));
    }

    public void EqualsShouldReturnFalseIfSourcePathIsDifferent()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll");
        DeploymentItem item2 = new("e:\\temp\\temp2.dll");

        Verify(!item1.Equals(item2));
    }

    public void EqualsShouldReturnFalseIfRelativeOutputDirectoryIsDifferent()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "foo2");

        Verify(!item1.Equals(item2));
    }

    public void EqualsShouldReturnTrueIfSourcePathDiffersByCase()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll");
        DeploymentItem item2 = new("e:\\temp\\Temp1.dll");

        Verify(item1.Equals(item2));
    }

    public void EqualsShouldReturnTrueIfRelativeOutputDirectoryDiffersByCase()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "Foo1");

        Verify(item1.Equals(item2));
    }

    public void EqualsShouldReturnTrueIfSourceAndRelativeOutputDirectoryAreSame()
    {
        DeploymentItem item1 = new("e:\\temp\\temp1.dll", "foo1");
        DeploymentItem item2 = new("e:\\temp\\temp1.dll", "foo1");

        Verify(item1.Equals(item2));
    }

    public void GetHashCodeShouldConsiderSourcePathAndRelativeOutputDirectory()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        string relativeOutputDirectory = "foo1";
        DeploymentItem item = new(sourcePath, relativeOutputDirectory);

        Verify(sourcePath.GetHashCode() + relativeOutputDirectory.GetHashCode() == item.GetHashCode());
    }

    public void ToStringShouldReturnDeploymentItemIfRelativeOutputDirectoryIsNotSpecified()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        DeploymentItem item = new(sourcePath);

        Verify(string.Format(CultureInfo.InvariantCulture, Resource.DeploymentItem, sourcePath) == item.ToString());
    }

    public void ToStringShouldReturnDeploymentItemAndRelativeOutputDirectory()
    {
        string sourcePath = "e:\\temp\\temp1.dll";
        string relativeOutputDirectory = "foo1";
        DeploymentItem item = new(sourcePath, relativeOutputDirectory);

        Verify(string.Format(CultureInfo.InvariantCulture, Resource.DeploymentItemWithOutputDirectory, sourcePath, relativeOutputDirectory) == item.ToString());
    }
}
