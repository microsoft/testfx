// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// The unit test element.
/// </summary>
[Serializable]
[DebuggerDisplay("{GetDisplayName()} ({TestMethod.ManagedTypeName})")]
internal sealed class UnitTestElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestElement"/> class.
    /// </summary>
    /// <param name="testMethod"> The test method. </param>
    /// <exception cref="ArgumentNullException"> Thrown when method is null. </exception>
    public UnitTestElement(TestMethod testMethod)
    {
        Guard.NotNull(testMethod);

        DebugEx.Assert(testMethod.FullClassName != null, "Full className cannot be empty");
        TestMethod = testMethod;
    }

    /// <summary>
    /// Gets the test method which should be executed as part of this test case.
    /// </summary>
    public TestMethod TestMethod { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the unit test should be ignored at run-time.
    /// </summary>
    public bool Ignored { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is a async test.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Gets or sets the test categories for test method.
    /// </summary>
    public string[]? TestCategory { get; set; }

    /// <summary>
    /// Gets or sets the traits for test method.
    /// </summary>
    public Trait[]? Traits { get; set; }

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
    public KeyValuePair<string, string>[]? DeploymentItems { get; set; }

    /// <summary>
    /// Gets or sets the DisplayName.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the compiler generated type name for async test method.
    /// </summary>
    internal string? AsyncTypeName { get; set; }

    /// <summary>
    /// Gets or sets the Css Iteration for the test method.
    /// </summary>
    internal string? CssIteration { get; set; }

    /// <summary>
    /// Gets or sets the Css Project Structure for the test method.
    /// </summary>
    internal string? CssProjectStructure { get; set; }

    /// <summary>
    /// Gets or sets the Description for the test method.
    /// </summary>
    internal string? Description { get; set; }

    /// <summary>
    /// Gets or sets the Work Item Ids for the test method.
    /// </summary>
    internal string[]? WorkItemIds { get; set; }

    internal UnitTestElement Clone()
    {
        var clone = (UnitTestElement)MemberwiseClone();
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
        // string testFullName = this.TestMethod.HasManagedMethodAndTypeProperties
        //                 ? string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.TestMethod.ManagedTypeName, this.TestMethod.ManagedMethodName)
        //                 : string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.TestMethod.FullClassName, this.TestMethod.Name);
        string testFullName = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", TestMethod.FullClassName, TestMethod.Name);

        TestCase testCase = new(testFullName, Constants.ExecutorUri, TestMethod.AssemblyName)
        {
            DisplayName = GetDisplayName(),
        };

        if (TestMethod.HasManagedMethodAndTypeProperties)
        {
            testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, TestMethod.ManagedTypeName);
            testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, TestMethod.ManagedMethodName);
            testCase.SetPropertyValue(Constants.TestClassNameProperty, TestMethod.ManagedTypeName);
        }
        else
        {
            testCase.SetPropertyValue(Constants.TestClassNameProperty, TestMethod.FullClassName);
        }

        IReadOnlyCollection<string?> hierarchy = TestMethod.Hierarchy;
        if (hierarchy is { Count: > 0 })
        {
            testCase.SetHierarchy(hierarchy.ToArray());
        }

        // Set declaring type if present so the correct method info can be retrieved
        if (TestMethod.DeclaringClassFullName != null)
        {
            testCase.SetPropertyValue(Constants.DeclaringClassNameProperty, TestMethod.DeclaringClassFullName);
        }

        // Many of the tests will not be async, so there is no point in sending extra data
        if (IsAsync)
        {
            testCase.SetPropertyValue(Constants.AsyncTestProperty, IsAsync);
        }

        // Set only if some test category is present
        if (TestCategory is { Length: > 0 })
        {
            testCase.SetPropertyValue(Constants.TestCategoryProperty, TestCategory);
        }

        // Set priority if present
        if (Priority != null)
        {
            testCase.SetPropertyValue(Constants.PriorityProperty, Priority.Value);
        }

        if (Traits is { Length: > 0 })
        {
            testCase.Traits.AddRange(Traits);
        }

        if (!StringEx.IsNullOrEmpty(CssIteration))
        {
            testCase.SetPropertyValue(Constants.CssIterationProperty, CssIteration);
        }

        if (!StringEx.IsNullOrEmpty(CssProjectStructure))
        {
            testCase.SetPropertyValue(Constants.CssProjectStructureProperty, CssProjectStructure);
        }

        if (!StringEx.IsNullOrEmpty(Description))
        {
            testCase.SetPropertyValue(Constants.DescriptionProperty, Description);
        }

        if (WorkItemIds != null)
        {
            testCase.SetPropertyValue(Constants.WorkItemIdsProperty, WorkItemIds);
        }

        // The list of items to deploy before running this test.
        if (DeploymentItems is { Length: > 0 })
        {
            testCase.SetPropertyValue(Constants.DeploymentItemsProperty, DeploymentItems);
        }

        // Set the Do not parallelize state if present
        if (DoNotParallelize)
        {
            testCase.SetPropertyValue(Constants.DoNotParallelizeProperty, DoNotParallelize);
        }

        // Store resolved data if any
        if (TestMethod.DataType != DynamicDataType.None)
        {
            string?[]? data = TestMethod.SerializedData;

            testCase.SetPropertyValue(Constants.TestDynamicDataTypeProperty, (int)TestMethod.DataType);
            testCase.SetPropertyValue(Constants.TestDynamicDataProperty, data);
        }

        SetTestCaseId(testCase, testFullName);

        return testCase;
    }

    private void SetTestCaseId(TestCase testCase, string testFullName)
    {
        testCase.SetPropertyValue(Constants.TestIdGenerationStrategyProperty, (int)TestMethod.TestIdGenerationStrategy);

        switch (TestMethod.TestIdGenerationStrategy)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            case TestIdGenerationStrategy.Legacy:
                // Legacy Id generation is to rely on default ID generation of TestCase from TestPlatform.
                break;

            case TestIdGenerationStrategy.DisplayName:
                testCase.Id = GenerateDisplayNameStrategyTestId(testCase);
                break;
#pragma warning restore CS0618 // Type or member is obsolete

            case TestIdGenerationStrategy.FullyQualified:
                testCase.Id = GenerateSerializedDataStrategyTestId(testFullName);
                break;

            default:
                throw new NotSupportedException($"Requested test ID generation strategy '{TestMethod.TestIdGenerationStrategy}' is not supported.");
        }
    }

    private Guid GenerateDisplayNameStrategyTestId(TestCase testCase)
    {
        var idProvider = new TestIdProvider();
        idProvider.AppendString(testCase.ExecutorUri.ToString());

        // Below comment is copied over from Test Platform.
        // If source is a file name then just use the filename for the identifier since the file might have moved between
        // discovery and execution (in appx mode for example). This is not elegant because the Source contents should be
        // a black box to the framework.
        // For example in the database adapter case this is not a file path.
        // As discussed with team, we found no scenario for netcore, & fullclr where the Source is not present where ID
        // is generated, which means we would always use FileName to generate ID. In cases where somehow Source Path
        // contained garbage character the API Path.GetFileName() we are simply returning original input.
        // For UWP where source during discovery, & during execution can be on different machine, in such case we should
        // always use Path.GetFileName().
        string filePath = testCase.Source;
        try
        {
            filePath = Path.GetFileName(filePath);
        }
        catch (ArgumentException)
        {
            // In case path contains invalid characters.
        }

        idProvider.AppendString(filePath);

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

        return idProvider.GetId();
    }

    private Guid GenerateSerializedDataStrategyTestId(string testFullName)
    {
        var idProvider = new TestIdProvider();

        idProvider.AppendString(Constants.ExecutorUriString);

        // Below comment is copied over from Test Platform.
        // If source is a file name then just use the filename for the identifier since the file might have moved between
        // discovery and execution (in appx mode for example). This is not elegant because the Source contents should be
        // a black box to the framework.
        // For example in the database adapter case this is not a file path.
        // As discussed with team, we found no scenario for netcore, & fullclr where the Source is not present where ID
        // is generated, which means we would always use FileName to generate ID. In cases where somehow Source Path
        // contained garbage character the API Path.GetFileName() we are simply returning original input.
        // For UWP where source during discovery, & during execution can be on different machine, in such case we should
        // always use Path.GetFileName().
        string fileNameOrFilePath = TestMethod.AssemblyName;
        try
        {
            fileNameOrFilePath = Path.GetFileName(fileNameOrFilePath);
        }
        catch (ArgumentException)
        {
            // In case path contains invalid characters.
        }

        idProvider.AppendString(fileNameOrFilePath);
        idProvider.AppendString(testFullName);

        if (TestMethod.SerializedData != null)
        {
            foreach (string? item in TestMethod.SerializedData)
            {
                idProvider.AppendString(item ?? "null");
            }
        }

        return idProvider.GetId();
    }

    private string GetDisplayName() => StringEx.IsNullOrWhiteSpace(DisplayName) ? TestMethod.Name : DisplayName;
}
