// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;

public class TestRunDirectoriesTests : TestContainer
{
    private static readonly string TestRoot = Path.GetFullPath("run");
    private readonly TestRunDirectories _testRunDirectories = new(TestRoot, Path.Combine(TestRoot, "asm.dll"), isAppDomainCreationDisabled: false);

    public void InDirectoryShouldReturnCorrectDirectory()
        => _testRunDirectories.InDirectory.Should().Be(Path.Combine(TestRoot, "In"));

    public void OutDirectoryShouldReturnCorrectDirectory()
        => _testRunDirectories.OutDirectory.Should().Be(Path.Combine(TestRoot, "Out"));

    public void InMachineNameDirectoryShouldReturnMachineSpecificDeploymentDirectory()
        => _testRunDirectories.InMachineNameDirectory.Should().Be(Path.Combine(TestRoot, "In", Environment.MachineName));

    public void OutDirectoryShouldFallBackWhenFirstTestSourceIsEmpty()
    {
        // Simulates Android CoreCLR where Assembly.Location returns "" (before synthetic path).
        var directories = new TestRunDirectories(TestRoot, firstTestSource: string.Empty, isAppDomainCreationDisabled: true);
        directories.OutDirectory.Should().Be(Path.Combine(TestRoot, "Out"));
    }

    public void OutDirectoryShouldFallBackWhenFirstTestSourceIsRelativePath()
    {
        // Simulates Android MonoVM / CoreCLR after PR #7772 where Assembly.Location
        // is a relative filename like "MyTests.dll" with no directory component.
        var directories = new TestRunDirectories(TestRoot, firstTestSource: "MyTests.dll", isAppDomainCreationDisabled: true);
        directories.OutDirectory.Should().Be(Path.Combine(TestRoot, "Out"));
    }
}
