// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG && !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;

public sealed class DeploymentItemMutationTests : TestContainer
{
    public void ConstructorFailsTheDiagnosticAssertionForAnEmptySourcePath()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        var item = new DeploymentItem(string.Empty, "out");

        assertions.Should().Equal("sourcePath");
        item.SourcePath.Should().BeEmpty();
        item.RelativeOutputDirectory.Should().Be("out");
    }

    public void ConstructorFailsTheDiagnosticAssertionForANullRelativeOutputDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        var item = new DeploymentItem("source", null!);

        assertions.Should().Equal("relativeOutputDirectory");
        item.SourcePath.Should().Be("source");
        item.RelativeOutputDirectory.Should().BeNull();
    }

    public void ConstructorDoesNotFailTheDiagnosticAssertionsForValidArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        var item = new DeploymentItem("source" + Path.DirectorySeparatorChar, string.Empty);

        assertions.Should().BeEmpty();
        item.SourcePath.Should().Be("source");
    }

    public void EqualsFailsTheDiagnosticAssertionForANullSourcePath()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", "out");
        SetSourcePath(item, null);
        var other = new DeploymentItem("other", "out");

        Action equals = () => _ = item.Equals(other);

        equals.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("SourcePath");
    }

    public void EqualsFailsTheDiagnosticAssertionForANullRelativeOutputDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", null!);
        assertions.Clear();
        var other = new DeploymentItem("other", "out");

        bool result = item.Equals(other);

        result.Should().BeFalse();
        assertions.Should().Equal("RelativeOutputDirectory");
    }

    public void EqualsDoesNotFailTheDiagnosticAssertionsForValidItemsOrForOtherObjects()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", "out");

        item.Equals(new DeploymentItem("source", "out")).Should().BeTrue();
        item.Equals("source").Should().BeFalse();

        assertions.Should().BeEmpty();
    }

    public void GetHashCodeFailsTheDiagnosticAssertionForANullSourcePath()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", "out");
        SetSourcePath(item, null);

        Action getHashCode = () => _ = item.GetHashCode();

        getHashCode.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("SourcePath");
    }

    public void GetHashCodeFailsTheDiagnosticAssertionForANullRelativeOutputDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", null!);
        assertions.Clear();

        Action getHashCode = () => _ = item.GetHashCode();

        getHashCode.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("RelativeOutputDirectory");
    }

    public void GetHashCodeDoesNotFailTheDiagnosticAssertionsForAValidItem()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", "out");

        item.GetHashCode().Should().Be("source".GetHashCode() + "out".GetHashCode());

        assertions.Should().BeEmpty();
    }

    public void ToStringFailsTheDiagnosticAssertionForANullSourcePath()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", string.Empty);
        SetSourcePath(item, null);

        string text = item.ToString();

        assertions.Should().Equal("SourcePath");
        text.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItem, string.Empty));
    }

    public void ToStringFailsTheDiagnosticAssertionForANullRelativeOutputDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", null!);
        assertions.Clear();

        string text = item.ToString();

        assertions.Should().Equal("RelativeOutputDirectory");
        text.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItem, "source"));
    }

    public void ToStringDoesNotFailTheDiagnosticAssertionsForAValidItem()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var item = new DeploymentItem("source", "out");

        string text = item.ToString();

        assertions.Should().BeEmpty();
        text.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemWithOutputDirectory, "source", "out"));
    }

    private static void SetSourcePath(DeploymentItem item, string? value)
        => typeof(DeploymentItem)
            .GetProperty(nameof(DeploymentItem.SourcePath), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(item, value);
}
#endif
