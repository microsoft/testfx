// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Deployment;

using System;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

public class TestRunDirectoriesTests : TestContainer
{
    private readonly TestRunDirectories _testRunDirectories = new(@"C:\temp");

    public void InMachineNameDirectoryShouldReturnMachineSpecificDeploymentDirectory()
    {
        Verify(
            Path.Combine(@"C:\temp\In", Environment.MachineName)
            == _testRunDirectories.InMachineNameDirectory);
    }
}
