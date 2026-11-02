// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Tests exercise the experimental shipping snapshot contract.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class ExtensionsContractTests : TestContainer
{
    public void PlannedTestCopiesMetadataAndCollections()
    {
        string[] categories = ["fast", "database"];
        KeyValuePair<string, string>[] properties = [new("owner", "one"), new("owner", "two")];
        var test = new PlannedTest(
            "Example.Tests", "Calculate", "Calculation", "example.dll",
            "Example.Tests", "Calculate(System.Int32)", "example.cs", 37, categories, properties);
        categories[0] = "changed";
        properties[0] = new("changed", "changed");

        CheckMetadata(test);
        test.TestCategories.Should().Equal("fast", "database");
        test.TestProperties.Select(p => (p.Key, p.Value)).Should().Equal(("owner", "one"), ("owner", "two"));
    }

    public void PlannedTestRejectsNullRequiredArguments()
    {
        Action[] actions =
        [
            () => Create(null!, "Calculate", "example.dll", [], []),
            () => Create("Example.Tests", null!, "example.dll", [], []),
            () => Create("Example.Tests", "Calculate", null!, [], []),
            () => Create("Example.Tests", "Calculate", "example.dll", null!, []),
            () => Create("Example.Tests", "Calculate", "example.dll", [], null!),
        ];
        string[] names = ["fullyQualifiedTestClassName", "testName", "assemblyPath", "testCategories", "testProperties"];
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be(names[i]);
        }

        PlannedTest empty = Create(string.Empty, string.Empty, string.Empty, [], []);
        empty.TestDisplayName.Should().BeNull();
        empty.ManagedTypeName.Should().BeNull();
        empty.ManagedMethodName.Should().BeNull();
        empty.DeclaringFilePath.Should().BeNull();
        empty.DeclaringLineNumber.Should().BeNull();
        empty.TestCategories.Should().BeEmpty();
        empty.TestProperties.Should().BeEmpty();
    }

    public void PlannedTestOwnedArraysPreserveMetadata()
    {
        MethodInfo factory = typeof(PlannedTest).GetMethod("CreateFromOwnedArrays", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] categories = ["fast", "database"];
        KeyValuePair<string, string>[] properties = [new("owner", "one"), new("owner", "two")];
        var test = (PlannedTest)factory.Invoke(
            null,
            ["Example.Tests", "Calculate", "Calculation", "example.dll", "Example.Tests", "Calculate(System.Int32)", "example.cs", 37, categories, properties])!;

        CheckMetadata(test);
        test.TestCategories.Should().BeSameAs(categories);
        test.TestProperties.Should().BeSameAs(properties);
    }

    public void TestRunSetAndResetPreserveSnapshot()
    {
        ITestRunInfo previous = TestRun.Current;
        MethodInfo setter = typeof(TestRun).GetMethod("SetCurrent", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            var info = new RunInfo([Create("Example.Tests", "Calculate", "example.dll", [], [])]);
            setter.Invoke(null, [info]);
            TestRun.Current.Should().BeSameAs(info);
            TestRun.Current.PlannedTests.Single().TestName.Should().Be("Calculate");
            setter.Invoke(null, [null]);
            TestRun.Current.PlannedTests.Should().BeEmpty();
        }
        finally
        {
            setter.Invoke(null, [previous]);
        }
    }

#if !WIN_UI
    public void DeploymentItemPreservesPathsWithoutPerformingDeployment()
    {
        var single = new DeploymentItemAttribute("input data.txt");
        single.Path.Should().Be("input data.txt");
        single.OutputDirectory.Should().BeEmpty();
        var directed = new DeploymentItemAttribute("source", "destination");
        directed.Path.Should().Be("source");
        directed.OutputDirectory.Should().Be("destination");
    }
#endif

    private static PlannedTest Create(
        string className, string name, string assembly,
        IReadOnlyCollection<string> categories, IReadOnlyCollection<KeyValuePair<string, string>> properties)
        => new(className, name, null, assembly, null, null, null, null, categories, properties);

    private static void CheckMetadata(PlannedTest test)
    {
        test.FullyQualifiedTestClassName.Should().Be("Example.Tests");
        test.TestName.Should().Be("Calculate");
        test.TestDisplayName.Should().Be("Calculation");
        test.AssemblyPath.Should().Be("example.dll");
        test.ManagedTypeName.Should().Be("Example.Tests");
        test.ManagedMethodName.Should().Be("Calculate(System.Int32)");
        test.DeclaringFilePath.Should().Be("example.cs");
        test.DeclaringLineNumber.Should().Be(37);
    }

    private sealed class RunInfo(IReadOnlyCollection<PlannedTest> tests) : ITestRunInfo
    {
        public IReadOnlyCollection<PlannedTest> PlannedTests { get; } = tests;
    }
}
