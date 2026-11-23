// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class DeploymentItemUtilityMutationTests : TestContainer
{
    public void HasDeploymentItemsRequiresAtLeastOneDeploymentItem()
    {
        var withoutItems = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null)) { DeploymentItems = null };
        var withEmptyItems = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null)) { DeploymentItems = [] };
        var withOneItem = new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))
        {
            DeploymentItems = [new KeyValuePair<string, string>("data.csv", string.Empty)],
        };

        DeploymentItemUtility.HasDeploymentItems(withoutItems).Should().BeFalse();
        DeploymentItemUtility.HasDeploymentItems(withEmptyItems).Should().BeFalse();
        DeploymentItemUtility.HasDeploymentItems(withOneItem).Should().BeTrue();
    }

    public void InvalidPathCharactersInTheSourceDirectoryAreReported()
    {
        // The invalid character is in the directory part only, so the file name check cannot be the one to report it.
        const string sourcePath = "dir\0name/file.txt";

        bool isValid = DeploymentItemUtility.IsValidDeploymentItem(sourcePath, string.Empty, out string? warning);

        isValid.Should().BeFalse();
        warning.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemContainsInvalidCharacters, sourcePath, string.Empty));
    }

    public void InvalidPathCharactersInTheOutputDirectoryAreReported()
    {
        const string outputDirectory = "out\0dir/sub";

        bool isValid = DeploymentItemUtility.IsValidDeploymentItem("file.txt", outputDirectory, out string? warning);

        isValid.Should().BeFalse();
        warning.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemContainsInvalidCharacters, "file.txt", outputDirectory));
    }

    public void PathsWithoutInvalidCharactersAreAccepted()
    {
        bool isValid = DeploymentItemUtility.IsValidDeploymentItem("dir/name/file.txt", "out/sub", out string? warning);

        isValid.Should().BeTrue();
        warning.Should().BeNull();
    }

#if DEBUG && !NETFRAMEWORK
    public void AddDeploymentItemFailsTheDiagnosticAssertionForANullList()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action add = () => DeploymentItemUtility.AddDeploymentItem(null!, new DeploymentItem("source"));

        add.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("DeploymentItem list cannot be null");
    }

    public void AddDeploymentItemFailsTheDiagnosticAssertionForANullItem()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var items = new List<DeploymentItem>();

        DeploymentItemUtility.AddDeploymentItem(items, null!);

        assertions.Should().Equal("DeploymentItem  cannot be null");
        items.Count.Should().Be(1);
        items[0].Should().BeNull();
    }

    public void AddDeploymentItemDoesNotFailTheDiagnosticAssertionsForValidArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var items = new List<DeploymentItem>();

        DeploymentItemUtility.AddDeploymentItem(items, new DeploymentItem("source", "out"));
        DeploymentItemUtility.AddDeploymentItem(items, new DeploymentItem("SOURCE", "OUT"));

        assertions.Should().BeEmpty();
        items.Count.Should().Be(1);
    }
#endif
}
#endif
