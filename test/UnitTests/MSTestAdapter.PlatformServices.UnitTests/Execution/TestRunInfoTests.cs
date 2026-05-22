// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestRunInfoTests : TestContainer
{
    public void CreateFromEmptyListReturnsInfoWithEmptyPlannedTests()
    {
        var info = TestRunInfo.CreateFrom([]);

        info.PlannedTests.Should().BeEmpty();
    }

    public void CreateFromSingleElementPopulatesAllRequiredFields()
    {
        var testMethod = new TestMethod(
            managedMethodName: "MyMethod",
            hierarchyValues: null,
            name: "MyMethod",
            fullClassName: "Some.Namespace.MyClass",
            assemblyName: @"c:\repo\bin\MyTests.dll",
            displayName: "My Display Name",
            parameterTypes: null);

        var element = new UnitTestElement(testMethod);

        var info = TestRunInfo.CreateFrom([element]);

        info.PlannedTests.Should().HaveCount(1);
        PlannedTest planned = info.PlannedTests.Single();
        planned.FullyQualifiedTestClassName.Should().Be("Some.Namespace.MyClass");
        planned.TestName.Should().Be("MyMethod");
        planned.TestDisplayName.Should().Be("My Display Name");
        planned.AssemblyPath.Should().Be(@"c:\repo\bin\MyTests.dll");
        planned.ManagedMethodName.Should().Be("MyMethod");
        planned.ManagedTypeName.Should().Be("Some.Namespace.MyClass");
        planned.TestCategories.Should().BeEmpty();
        planned.TestProperties.Should().BeEmpty();
    }

    public void CreateFromPopulatesCategoriesAndProperties()
    {
        var testMethod = new TestMethod("Run", "Tests.MyClass", "MyTests.dll", null);
        var element = new UnitTestElement(testMethod)
        {
            TestCategory = ["Smoke", "Compatibility"],
            Traits = [new Trait("Owner", "alice"), new Trait("Owner", "bob"), new Trait("Priority", "1")],
        };

        var info = TestRunInfo.CreateFrom([element]);

        PlannedTest planned = info.PlannedTests.Single();
        planned.TestCategories.Should().BeEquivalentTo(["Smoke", "Compatibility"]);
        planned.TestProperties.Should().BeEquivalentTo(new[]
        {
            new KeyValuePair<string, string>("Owner", "alice"),
            new KeyValuePair<string, string>("Owner", "bob"),
            new KeyValuePair<string, string>("Priority", "1"),
        });
    }

    public void TestRunCurrentIsNeverNull()
        => TestRun.Current.Should().NotBeNull();

    public void SetCurrentNullResetsToEmpty()
    {
        var testMethod = new TestMethod("M", "T", "A.dll", null);
        TestRun.SetCurrent(TestRunInfo.CreateFrom([new UnitTestElement(testMethod)]));
        TestRun.Current.PlannedTests.Should().HaveCount(1);

        TestRun.SetCurrent(null);

        TestRun.Current.Should().NotBeNull();
        TestRun.Current.PlannedTests.Should().BeEmpty();
    }

    public void CreateFromMapsDefaultDisplayNameToNull()
    {
        // TestMethod.DisplayName defaults to TestMethod.Name when no display name is explicitly provided.
        // PlannedTest.TestDisplayName should be null in that case so consumers can distinguish
        // "no explicit display name" from "display name set".
        var testMethod = new TestMethod("MyMethod", "Tests.MyClass", "MyTests.dll", displayName: null);
        var element = new UnitTestElement(testMethod);

        var info = TestRunInfo.CreateFrom([element]);

        PlannedTest planned = info.PlannedTests.Single();
        planned.TestName.Should().Be("MyMethod");
        planned.TestDisplayName.Should().BeNull();
    }

    public void PlannedTestCopiesInputCollections()
    {
        string[] categories = ["Smoke"];
        KeyValuePair<string, string>[] properties = [new("Owner", "alice")];

        var plannedTest = new PlannedTest(
            fullyQualifiedTestClassName: "Tests.MyClass",
            testName: "Run",
            testDisplayName: null,
            assemblyPath: "MyTests.dll",
            managedTypeName: "Tests.MyClass",
            managedMethodName: "Run",
            declaringFilePath: "Tests.cs",
            declaringLineNumber: 42,
            testCategories: categories,
            testProperties: properties);

        categories[0] = "Changed";
        properties[0] = new KeyValuePair<string, string>("Owner", "bob");

        plannedTest.TestCategories.Should().Equal("Smoke");
        plannedTest.TestProperties.Should().Equal([new KeyValuePair<string, string>("Owner", "alice")]);
    }
}
