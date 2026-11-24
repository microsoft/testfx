// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;

public sealed class TestRunDirectoriesMutationTests : TestContainer
{
    public void OutDirectoryIsTheDirectoryOfTheFirstTestSourceWhenAppDomainCreationIsDisabled()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-trd-root");
        string sourceDirectory = Path.Combine(Path.GetTempPath(), "testfx-trd-tests", "bin");
        string source = Path.Combine(sourceDirectory, "MyTests.dll");

        var directories = new TestRunDirectories(root, source, isAppDomainCreationDisabled: true);

        directories.OutDirectory.Should().Be(sourceDirectory);
        directories.RootDeploymentDirectory.Should().Be(root);
        directories.InDirectory.Should().Be(Path.Combine(root, "In"));
        directories.InMachineNameDirectory.Should().Be(Path.Combine(root, "In", Environment.MachineName));
    }

    public void OutDirectoryStaysUnderTheRootWhenAppDomainCreationIsEnabled()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-trd-root");
        string source = Path.Combine(Path.GetTempPath(), "testfx-trd-tests", "bin", "MyTests.dll");

        var directories = new TestRunDirectories(root, source, isAppDomainCreationDisabled: false);

        directories.OutDirectory.Should().Be(Path.Combine(root, "Out"));
    }

    public void OutDirectoryStaysUnderTheRootWhenTheTestSourceHasNoDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-trd-root");
        string expected = Path.Combine(root, "Out");

        new TestRunDirectories(root, firstTestSource: null, isAppDomainCreationDisabled: true).OutDirectory.Should().Be(expected);
        new TestRunDirectories(root, firstTestSource: string.Empty, isAppDomainCreationDisabled: true).OutDirectory.Should().Be(expected);
        new TestRunDirectories(root, firstTestSource: "MyTests.dll", isAppDomainCreationDisabled: true).OutDirectory.Should().Be(expected);
    }

#if DEBUG && !NETFRAMEWORK
    public void EmptyRootDirectoryFailsTheDiagnosticAssertionWithTheArgumentName()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        var directories = new TestRunDirectories(string.Empty, firstTestSource: null, isAppDomainCreationDisabled: false);

        assertions.Should().Equal("rootDirectory");
        directories.RootDeploymentDirectory.Should().BeEmpty();
        directories.InDirectory.Should().Be("In");
        directories.OutDirectory.Should().Be("Out");
    }

    public void ValidRootDirectoryDoesNotFailTheDiagnosticAssertion()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new TestRunDirectories(Path.Combine(Path.GetTempPath(), "testfx-trd-root"), firstTestSource: null, isAppDomainCreationDisabled: false);

        assertions.Should().BeEmpty();
    }
#endif
}
