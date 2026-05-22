// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Builds <see cref="ITestRunInfo"/> snapshots from internal MSTest discovery data so that user
/// code (typically <c>[AssemblyInitialize]</c> or fixtures) can query <see cref="TestRun.Current"/>.
/// </summary>
internal sealed class TestRunInfo : ITestRunInfo
{
    private static readonly KeyValuePair<string, string>[] EmptyProperties = [];
    private static readonly string[] EmptyCategories = [];

    public TestRunInfo(IReadOnlyCollection<PlannedTest> plannedTests)
        => PlannedTests = plannedTests;

    public IReadOnlyCollection<PlannedTest> PlannedTests { get; }

    public static TestRunInfo CreateFrom(IReadOnlyList<UnitTestElement> testsToRun)
    {
        if (testsToRun.Count == 0)
        {
            return new TestRunInfo([]);
        }

        var planned = new PlannedTest[testsToRun.Count];
        for (int i = 0; i < testsToRun.Count; i++)
        {
            planned[i] = ToPlannedTest(testsToRun[i]);
        }

        return new TestRunInfo(planned);
    }

    private static PlannedTest ToPlannedTest(UnitTestElement element)
    {
        Trait[]? traits = element.Traits;
        KeyValuePair<string, string>[] testProperties;
        if (traits is { Length: > 0 })
        {
            testProperties = new KeyValuePair<string, string>[traits.Length];
            for (int i = 0; i < traits.Length; i++)
            {
                testProperties[i] = new KeyValuePair<string, string>(traits[i].Name, traits[i].Value);
            }
        }
        else
        {
            testProperties = EmptyProperties;
        }

        string[] categories = element.TestCategory is { Length: > 0 }
            ? element.TestCategory
            : EmptyCategories;

        // TestMethod.DisplayName defaults to TestMethod.Name when no display name was explicitly set;
        // surface that distinction by passing null to PlannedTest so consumers can tell them apart.
        string testName = element.TestMethod.Name;
        string adapterDisplayName = element.TestMethod.DisplayName;
        string? testDisplayName = string.Equals(adapterDisplayName, testName, StringComparison.Ordinal)
            ? null
            : adapterDisplayName;

        return PlannedTest.CreateFromOwnedArrays(
            fullyQualifiedTestClassName: element.TestMethod.FullClassName,
            testName: testName,
            testDisplayName: testDisplayName,
            assemblyPath: element.TestMethod.AssemblyName,
            managedTypeName: element.TestMethod.ManagedTypeName,
            managedMethodName: element.TestMethod.ManagedMethodName,
            declaringFilePath: element.DeclaringFilePath,
            declaringLineNumber: element.DeclaringLineNumber,
            testCategories: categories,
            testProperties: testProperties);
    }
}
