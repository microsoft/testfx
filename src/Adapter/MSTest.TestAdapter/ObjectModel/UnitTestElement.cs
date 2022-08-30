// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

/// <summary>
/// The unit test element.
/// </summary>
[Serializable]
[DebuggerDisplay("{GetDisplayName()} ({TestMethod.ManagedTypeName})")]
internal class UnitTestElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestElement"/> class.
    /// </summary>
    /// <param name="testMethod"> The test method. </param>
    /// <exception cref="ArgumentNullException"> Thrown when method is null. </exception>
    public UnitTestElement(TestMethod testMethod)
    {
        if (testMethod == null)
        {
            throw new ArgumentNullException(nameof(testMethod));
        }

        Debug.Assert(testMethod.FullClassName != null, "Full className cannot be empty");
        TestMethod = testMethod;
    }

    /// <summary>
    /// Gets the test method which should be executed as part of this test case
    /// </summary>
    public TestMethod TestMethod { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the unit test should be ignored at run-time
    /// </summary>
    public bool Ignored { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is a async test
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Gets or sets the test categories for test method.
    /// </summary>
    public string[] TestCategory { get; set; }

    /// <summary>
    /// Gets or sets the traits for test method.
    /// </summary>
    public Trait[] Traits { get; set; }

    /// <summary>
    /// Gets or sets the priority of the test method, if any.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this test method should not execute in parallel.
    /// </summary>
    public bool DoNotParallelize { get; set; }

    /// <summary>
    /// Gets or sets the deployment items for the test method.
    /// </summary>
    public KeyValuePair<string, string>[] DeploymentItems { get; set; }

    /// <summary>
    /// Gets or sets the DisplayName
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the compiler generated type name for async test method.
    /// </summary>
    internal string AsyncTypeName { get; set; }

    /// <summary>
    /// Gets or sets the Css Iteration for the test method.
    /// </summary>
    internal string CssIteration { get; set; }

    /// <summary>
    /// Gets or sets the Css Project Structure for the test method.
    /// </summary>
    internal string CssProjectStructure { get; set; }

    /// <summary>
    /// Gets or sets the Description for the test method.
    /// </summary>
    internal string Description { get; set; }

    /// <summary>
    /// Gets or sets the Work Item Ids for the test method.
    /// </summary>
    internal string[] WorkItemIds { get; set; }

    internal UnitTestElement Clone()
    {
        var clone = MemberwiseClone() as UnitTestElement;
        if (TestMethod != null)
        {
            clone.TestMethod = TestMethod.Clone();
        }

        return clone;
    }

    /// <summary>
    /// Convert the UnitTestElement instance to an Object Model testCase instance.
    /// </summary>
    /// <returns> An instance of <see cref="TestCase"/>. </returns>
    internal TestCase ToTestCase()
    {
        // This causes compatibility problems with older runners.
        // string fullName = this.TestMethod.HasManagedMethodAndTypeProperties
        //                 ? string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.TestMethod.ManagedTypeName, this.TestMethod.ManagedMethodName)
        //                 : string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.TestMethod.FullClassName, this.TestMethod.Name);
        var fullName = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", TestMethod.FullClassName, TestMethod.Name);

        TestCase testCase = new(fullName, TestAdapter.Constants.ExecutorUri, TestMethod.AssemblyName)
        {
            DisplayName = GetDisplayName()
        };

        if (TestMethod.HasManagedMethodAndTypeProperties)
        {
            testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, TestMethod.ManagedTypeName);
            testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, TestMethod.ManagedMethodName);
            testCase.SetPropertyValue(TestAdapter.Constants.TestClassNameProperty, TestMethod.ManagedTypeName);
        }
        else
        {
            testCase.SetPropertyValue(TestAdapter.Constants.TestClassNameProperty, TestMethod.FullClassName);
        }

        var hierarchy = TestMethod.Hierarchy;
        if (hierarchy != null && hierarchy.Count > 0)
        {
            testCase.SetHierarchy(hierarchy.ToArray());
        }

        // Set declaring type if present so the correct method info can be retrieved
        if (TestMethod.DeclaringClassFullName != null)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.DeclaringClassNameProperty, TestMethod.DeclaringClassFullName);
        }

        // Many of the tests will not be async, so there is no point in sending extra data
        if (IsAsync)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.AsyncTestProperty, IsAsync);
        }

        // Set only if some test category is present
        if (TestCategory != null && TestCategory.Length > 0)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.TestCategoryProperty, TestCategory);
        }

        // Set priority if present
        if (Priority != null)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.PriorityProperty, Priority.Value);
        }

        if (Traits != null)
        {
            testCase.Traits.AddRange(Traits);
        }

        if (!string.IsNullOrEmpty(CssIteration))
        {
            testCase.SetPropertyValue(TestAdapter.Constants.CssIterationProperty, CssIteration);
        }

        if (!string.IsNullOrEmpty(CssProjectStructure))
        {
            testCase.SetPropertyValue(TestAdapter.Constants.CssProjectStructureProperty, CssProjectStructure);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            testCase.SetPropertyValue(TestAdapter.Constants.DescriptionProperty, Description);
        }

        if (WorkItemIds != null)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.WorkItemIdsProperty, WorkItemIds);
        }

        // The list of items to deploy before running this test.
        if (DeploymentItems != null && DeploymentItems.Length > 0)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.DeploymentItemsProperty, DeploymentItems);
        }

        // Set the Do not parallelize state if present
        if (DoNotParallelize)
        {
            testCase.SetPropertyValue(TestAdapter.Constants.DoNotParallelizeProperty, DoNotParallelize);
        }

        // Store resolved data if any
        if (TestMethod.DataType != DynamicDataType.None)
        {
            var data = TestMethod.SerializedData;

            testCase.SetPropertyValue(TestAdapter.Constants.TestDynamicDataTypeProperty, (int)TestMethod.DataType);
            testCase.SetPropertyValue(TestAdapter.Constants.TestDynamicDataProperty, data);
        }

        string fileName = testCase.Source;
        try
        {
            fileName = Path.GetFileName(fileName);
        }
        catch
        {
        }

        var idProvider = new TestIdProvider();
        idProvider.AppendString(testCase.ExecutorUri?.ToString());
        idProvider.AppendString(fileName);
        if (TestMethod.HasManagedMethodAndTypeProperties)
        {
            idProvider.AppendString(TestMethod.ManagedTypeName);
            idProvider.AppendString(TestMethod.ManagedMethodName);
        }
        else
        {
            idProvider.AppendString(testCase.FullyQualifiedName);
        }

        if (TestMethod.DataType != DynamicDataType.None)
        {
            idProvider.AppendString(testCase.DisplayName);
        }

        testCase.Id = idProvider.GetId();

        return testCase;
    }

    private string GetDisplayName()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            return TestMethod.Name;

            // This causes compatibility problems with older runners.
            // return string.IsNullOrWhiteSpace(this.TestMethod.ManagedMethodName)
            //      ? this.TestMethod.Name
            //      : this.TestMethod.ManagedMethodName;
        }
        else
        {
            return DisplayName;
        }
    }
}
