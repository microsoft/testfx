// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerContextMutationTests : TestContainer
{
#if NET
    private const int CapacityProbeCount = 8;
#endif

    public void TraitsAloneAreAddedToTheTestContextProperties()
    {
        UnitTestElement element = CreateElement(traits: [new TestTrait("Owner", "me")]);

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties: null, new Dictionary<string, object>(), element);

        result.Should().Equal(new Dictionary<string, object?> { ["Owner"] = "me" });
    }

    public void TestCategoriesAloneAreAddedToTheTestContextPropertiesWithAnEmptyValue()
    {
        UnitTestElement element = CreateElement(categories: ["Category"]);

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties: null, new Dictionary<string, object>(), element);

        result.Should().Equal(new Dictionary<string, object?> { ["Category"] = string.Empty });
    }

    public void TestContextPropertiesMergeEverySourceAndOnlyTheSourceLevelParametersOverrideTcmProperties()
    {
        var tcmProperties = new Dictionary<string, object?> { ["fromTcm"] = 1, ["shared"] = "tcm" };
        var sourceLevelParameters = new Dictionary<string, object> { ["shared"] = "source", ["fromSource"] = 2 };
        UnitTestElement element = CreateElement(
            traits: [new TestTrait("trait", "traitValue"), new TestTrait("shared", "ignored"), new TestTrait(string.Empty, "unnamed")],
            categories: ["category", "fromTcm", string.Empty]);

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties, sourceLevelParameters, element);

        result.Should().Equal(new Dictionary<string, object?>
        {
            ["fromTcm"] = 1,
            ["shared"] = "source",
            ["fromSource"] = 2,
            ["trait"] = "traitValue",
            ["category"] = string.Empty,
        });
    }

#if NET
    public void TcmPropertiesAreCountedWhenSizingTheTestContextProperties()
    {
        var tcmProperties = new Dictionary<string, object?>();
        for (int i = 0; i < CapacityProbeCount; i++)
        {
            tcmProperties["tcm" + i] = i;
        }

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties, new Dictionary<string, object>(), CreateElement());

        AssertSizedOnceForAllEntries(result);
    }

    public void TraitsAreCountedWhenSizingTheTestContextProperties()
    {
        TestTrait[] traits = Enumerable.Range(0, CapacityProbeCount).Select(i => new TestTrait("trait" + i, "value")).ToArray();

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties: null, new Dictionary<string, object>(), CreateElement(traits: traits));

        AssertSizedOnceForAllEntries(result);
    }

    public void TestCategoriesAreCountedWhenSizingTheTestContextProperties()
    {
        string[] categories = Enumerable.Range(0, CapacityProbeCount).Select(i => "category" + i).ToArray();

        Dictionary<string, object?> result = GetTestContextProperties(tcmProperties: null, new Dictionary<string, object>(), CreateElement(categories: categories));

        AssertSizedOnceForAllEntries(result);
    }

    private static void AssertSizedOnceForAllEntries(Dictionary<string, object?> result)
    {
        result.Count.Should().Be(CapacityProbeCount);

        // EnsureCapacity(0) reports the current capacity: a dictionary that had to grow while it was filled ends up
        // with a different capacity than the one created with the final count up front.
        int expectedCapacity = new Dictionary<string, object?>(capacity: CapacityProbeCount).EnsureCapacity(0);
        result.EnsureCapacity(0).Should().Be(expectedCapacity);
    }
#endif

    private static UnitTestElement CreateElement(TestTrait[]? traits = null, string[]? categories = null)
        => new(new TestMethod("TestMethod", "Ns.TestClass", "TestAssembly", displayName: null))
        {
            Traits = traits,
            TestCategory = categories,
        };

    private static Dictionary<string, object?> GetTestContextProperties(
        IReadOnlyDictionary<string, object?>? tcmProperties,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestElement unitTestElement)
        => (Dictionary<string, object?>)typeof(TestExecutionManager)
            .GetMethod("GetTestContextProperties", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [tcmProperties, sourceLevelParameters, unitTestElement])!;
}
