// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Deployment;

public class TestRunDirectoriesTests : TestContainer
{
    private readonly TestRunDirectories _testRunDirectories = new(@"C:\temp");

    public void InDirectoryShouldReturnCorrectDirectory()
    {
        Verify(_testRunDirectories.InDirectory == @"C:\temp\In");
    }

    public void OutDirectoryShouldReturnCorrectDirectory()
    {
        Verify(_testRunDirectories.OutDirectory == @"C:\temp\Out");
    }

    public void InMachineNameDirectoryShouldReturnMachineSpecificDeploymentDirectory()
    {
        Verify(Path.Combine(@"C:\temp\In", Environment.MachineName) == _testRunDirectories.InMachineNameDirectory);
    }
}
