// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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
    private static readonly byte[] OpenParen = [40, 0]; // Encoding.Unicode.GetBytes("(");
    private static readonly byte[] CloseParen = [41, 0]; // Encoding.Unicode.GetBytes(")");
    private static readonly byte[] OpenBracket = [91, 0]; // Encoding.Unicode.GetBytes("[");
    private static readonly byte[] CloseBracket = [93, 0]; // Encoding.Unicode.GetBytes("]");

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

    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; set; } = TestDataSourceUnfoldingStrategy.Auto;

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

    internal string? DeclaringFilePath { get; set; }

    internal int? DeclaringLineNumber { get; set; }

    /// <summary>
    /// Gets or sets the compiler generated type name for async test method.
    /// </summary>
    internal string? AsyncTypeName { get; set; }

    /// <summary>
    /// Gets or sets the Work Item Ids for the test method.
    /// </summary>
    internal string[]? WorkItemIds { get; set; }

    internal UnitTestElement Clone()
    {
        var clone = (UnitTestElement)MemberwiseClone();
        clone.TestMethod = TestMethod.Clone();
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

        TestCase testCase = new(testFullName, EngineConstants.ExecutorUri, TestMethod.AssemblyName)
        {
            DisplayName = GetDisplayName(),
        };

        if (TestMethod.HasManagedMethodAndTypeProperties)
        {
            testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, TestMethod.ManagedTypeName);
            testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, TestMethod.ManagedMethodName);
            testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, TestMethod.ManagedTypeName);
        }
        else
        {
            testCase.SetPropertyValue(EngineConstants.TestClassNameProperty, TestMethod.FullClassName);
        }

        if (TestMethod.ParameterTypes is not null)
        {
            testCase.SetPropertyValue(EngineConstants.ParameterTypesProperty, TestMethod.ParameterTypes);
        }

        IReadOnlyCollection<string?> hierarchy = TestMethod.Hierarchy;
        if (hierarchy is { Count: > 0 })
        {
            testCase.SetHierarchy([.. hierarchy]);
        }

        // Set declaring type if present so the correct method info can be retrieved
        if (TestMethod.DeclaringClassFullName != null)
        {
            testCase.SetPropertyValue(EngineConstants.DeclaringClassNameProperty, TestMethod.DeclaringClassFullName);
        }

        // Set only if some test category is present
        if (TestCategory is { Length: > 0 })
        {
            testCase.SetPropertyValue(EngineConstants.TestCategoryProperty, TestCategory);
        }

        // Set priority if present
        if (Priority != null)
        {
            testCase.SetPropertyValue(EngineConstants.PriorityProperty, Priority.Value);
        }

        if (Traits is { Length: > 0 })
        {
            testCase.Traits.AddRange(Traits);
        }

        if (WorkItemIds != null)
        {
            testCase.SetPropertyValue(EngineConstants.WorkItemIdsProperty, WorkItemIds);
        }

        // The list of items to deploy before running this test.
        if (DeploymentItems is { Length: > 0 })
        {
            testCase.SetPropertyValue(EngineConstants.DeploymentItemsProperty, DeploymentItems);
        }

        // Set the Do not parallelize state if present
        if (DoNotParallelize)
        {
            testCase.SetPropertyValue(EngineConstants.DoNotParallelizeProperty, DoNotParallelize);
        }

        if (UnfoldingStrategy != TestDataSourceUnfoldingStrategy.Auto)
        {
            testCase.SetPropertyValue(EngineConstants.UnfoldingStrategy, (int)UnfoldingStrategy);
        }

        // Store resolved data if any
        if (TestMethod.DataType != DynamicDataType.None)
        {
            string?[]? data = TestMethod.SerializedData;

            testCase.SetPropertyValue(EngineConstants.TestDynamicDataTypeProperty, (int)TestMethod.DataType);
            testCase.SetPropertyValue(EngineConstants.TestDynamicDataProperty, data);
            testCase.SetPropertyValue(EngineConstants.TestCaseIndexProperty, TestMethod.TestCaseIndex);
            // VSTest serialization doesn't handle null so instead don't set the property so that it's deserialized as null
            if (TestMethod.TestDataSourceIgnoreMessage is not null)
            {
                testCase.SetPropertyValue(EngineConstants.TestDataSourceIgnoreMessageProperty, TestMethod.TestDataSourceIgnoreMessage);
            }
        }

        testCase.LineNumber = DeclaringLineNumber ?? -1;
        testCase.CodeFilePath = DeclaringFilePath;

        SetTestCaseId(testCase, testFullName);

        return testCase;
    }

    private void SetTestCaseId(TestCase testCase, string testFullName)
        => testCase.Id = GenerateSerializedDataStrategyTestId(testFullName);

    private Guid GenerateSerializedDataStrategyTestId(string testFullName)
    {
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

        byte hashVersion = 1; // Increment when changing the hashing algorithm.
        var hash = new TestFx.Hashing.XxHash128();
        hash.Append(Encoding.Unicode.GetBytes(fileNameOrFilePath));
        hash.Append(Encoding.Unicode.GetBytes(testFullName));
        if (TestMethod.ParameterTypes is not null)
        {
            hash.Append(OpenParen);
            hash.Append(Encoding.Unicode.GetBytes(TestMethod.ParameterTypes));
            hash.Append(CloseParen);
        }

        if (TestMethod.SerializedData != null)
        {
            hash.Append(OpenBracket);
            hash.Append(Encoding.Unicode.GetBytes(TestMethod.TestCaseIndex.ToString(CultureInfo.InvariantCulture)));
            hash.Append(CloseBracket);
        }

        byte[] hashBytes = hash.GetCurrentHash();

        return VersionedGuidFromHash(hashBytes, hashVersion);
    }

    internal /* for testing */ static Guid VersionedGuidFromHash(byte[] hashBytes, byte hashVersion)
    {
        const int firstByte = 0;
        const int versionByte = 6;

        // We set first 4 bits to 0001, which is our version. Increase this when we change the hashing algorithm.
        //
        // Note: The logic below is operating on int32, because bitwise operators are not defined for byte in C#. But casting
        // does not affect endianness, so we can safely assume the byte data are at the end of the int.
        // We also don't care to specify the whole expansion of the int in the bit masks, which effectively ignores all the data in the int
        // before the last byte data, but that is okay, they will always be zero.
        hashBytes[firstByte] = (byte)((hashBytes[firstByte] & 0b0000_1111) | (hashVersion << 4));
        // We set first 4bits 7th byte to 8 to indicate that this is a version 8 UUID. https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-8
        hashBytes[versionByte] = (byte)((hashBytes[versionByte] & 0b0000_1111) | 0b1000_0000);

        var guid = new Guid(hashBytes);
#if NET9_0_OR_GREATER
        // Version property is only available on .NET 9 and later.
        Debug.Assert(guid.Version == 8, "Expected Guid version to be 8");
#endif
        return guid;
    }

    private string GetDisplayName() => StringEx.IsNullOrWhiteSpace(DisplayName) ? TestMethod.Name : DisplayName;
}
