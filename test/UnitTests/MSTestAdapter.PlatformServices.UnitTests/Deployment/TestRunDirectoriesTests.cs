// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace MSTestAdapter.PlatformServices.Tests.Deployment;

public class TestRunDirectoriesTests : TestContainer
{
    private readonly TestRunDirectories _testRunDirectories = new(@"C:\temp");

    public void InDirectoryShouldReturnCorrectDirectory() => _testRunDirectories.InDirectory.Should().Be(@"C:\temp\In");

    public void OutDirectoryShouldReturnCorrectDirectory() => _testRunDirectories.OutDirectory.Should().Be(@"C:\temp\Out");

    public void InMachineNameDirectoryShouldReturnMachineSpecificDeploymentDirectory() => Path.Combine(@"C:\temp\In", Environment.MachineName).Should().Be(_testRunDirectories.InMachineNameDirectory);
}
