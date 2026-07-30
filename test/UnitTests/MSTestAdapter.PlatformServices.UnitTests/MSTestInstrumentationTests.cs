// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

/// <summary>
/// Guards the engine-side tracing seam: it must be completely inert until a host installs a factory (which is the
/// case under VSTest and whenever the OpenTelemetry extension is not registered), and it must not build any tag
/// payload in that state.
/// </summary>
public sealed class MSTestInstrumentationTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        MSTestInstrumentation.SetActivityFactory(null);
        base.Dispose(disposing);
    }

    public void IsEnabledIsFalseAndStartActivityReturnsNullWhenNoFactoryIsInstalled()
    {
        MSTestInstrumentation.SetActivityFactory(null);

        MSTestInstrumentation.IsEnabled.Should().BeFalse();
        MSTestInstrumentation.StartActivity("name").Should().BeNull();
        MSTestInstrumentation.StartFixtureActivity("name", "kind", "Type", "Assembly").Should().BeNull();
    }

    public void StartFixtureActivityDoesNotBuildTagsWhenNoFactoryIsInstalled()
    {
        MSTestInstrumentation.SetActivityFactory(null);

        // A throwing argument expression would surface if the seam evaluated its inputs; the point of the guard at
        // the call sites is that it does not even get that far.
        MSTestInstrumentation.StartFixtureActivity(
            MSTestInstrumentation.ActivityNames.ClassInitialize,
            "class_initialize",
            owningType: null,
            assemblyName: null).Should().BeNull();
    }

    public void StartFixtureActivityForwardsTheConventionalTags()
    {
        List<KeyValuePair<string, object?>>? capturedTags = null;
        string? capturedName = null;
        MSTestInstrumentation.SetActivityFactory((name, tags) =>
        {
            capturedName = name;
            capturedTags = tags is null ? null : [.. tags];
            return new FakeActivity();
        });

        using IMSTestActivity? activity = MSTestInstrumentation.StartFixtureActivity(
            MSTestInstrumentation.ActivityNames.AssemblyInitialize,
            "assembly_initialize",
            "My.Namespace.MyClass",
            "MyAssembly");

        activity.Should().NotBeNull();
        MSTestInstrumentation.IsEnabled.Should().BeTrue();
        capturedName.Should().Be("MSTest.AssemblyInitialize");
        capturedTags.Should().Contain(new KeyValuePair<string, object?>("test.fixture.kind", "assembly_initialize"));
        capturedTags.Should().Contain(new KeyValuePair<string, object?>("test.suite.name", "My.Namespace.MyClass"));
        capturedTags.Should().Contain(new KeyValuePair<string, object?>("test.assembly.name", "MyAssembly"));
    }

    public void SetActivityFactoryWithNullDisablesAPreviouslyInstalledFactory()
    {
        MSTestInstrumentation.SetActivityFactory((_, _) => new FakeActivity());
        MSTestInstrumentation.IsEnabled.Should().BeTrue();

        MSTestInstrumentation.SetActivityFactory(null);

        MSTestInstrumentation.IsEnabled.Should().BeFalse();
        MSTestInstrumentation.StartActivity("name").Should().BeNull();
    }

    private sealed class FakeActivity : IMSTestActivity
    {
        public void SetTag(string key, object? value)
        {
        }

        public void RecordException(Exception exception)
        {
        }

        public void Dispose()
        {
        }
    }
}
