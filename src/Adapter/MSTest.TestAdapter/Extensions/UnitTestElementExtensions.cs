// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Materializes a VSTest <see cref="TestCase"/> from a platform-agnostic <see cref="UnitTestElement"/>. This is the
/// adapter-boundary translation from the neutral execution model to the VSTest object model; the platform services
/// engine never performs it and only threads the resulting test case (as an opaque handle) to the recorder and the
/// deployment boundary.
/// </summary>
internal static class UnitTestElementExtensions
{
    private static readonly byte[] OpenParen = [40, 0]; // Encoding.Unicode.GetBytes("(");
    private static readonly byte[] CloseParen = [41, 0]; // Encoding.Unicode.GetBytes(")");
    private static readonly byte[] OpenBracket = [91, 0]; // Encoding.Unicode.GetBytes("[");
    private static readonly byte[] CloseBracket = [93, 0]; // Encoding.Unicode.GetBytes("]");

    /// <summary>
    /// Returns the host test case for this test, reusing the host-provided one when present and otherwise
    /// materializing a single VSTest test case on demand and caching it (so deployment, test-start and every
    /// reported result share one instance, matching the historical "one test case per discovered test").
    /// </summary>
    internal static TestCase GetOrCreateHostTestCase(this UnitTestElement element)
    {
        if (element.HostRecordingHandle is not TestCase testCase)
        {
            testCase = element.ToTestCase();
            element.HostRecordingHandle = testCase;
        }

        return testCase;
    }

    /// <summary>
    /// Convert the UnitTestElement instance to an Object Model testCase instance.
    /// </summary>
    /// <param name="element">The unit test element to convert.</param>
    /// <returns> An instance of <see cref="TestCase"/>. </returns>
    internal static TestCase ToTestCase(this UnitTestElement element)
    {
        TestMethod testMethod = element.TestMethod;

        // This causes compatibility problems with older runners.
        // string testFullName = this.TestMethod.HasManagedMethodAndTypeProperties
        //     ? $"{TestMethod.ManagedTypeName}.{TestMethod.ManagedMethodName}"
        //     : $"{TestMethod.FullClassName}.{TestMethod.Name}";
        string testFullName = $"{testMethod.FullClassName}.{testMethod.Name}";

        TestCase testCase = new(testFullName, EngineConstants.ExecutorUri, testMethod.AssemblyName)
        {
            DisplayName = testMethod.DisplayName,
            LocalExtensionData = element,
        };

        if (testMethod.HasManagedMethodAndTypeProperties)
        {
            testCase.SetPropertyValue(TestCaseExtensions.ManagedTypeProperty, testMethod.ManagedTypeName);
            testCase.SetPropertyValue(TestCaseExtensions.ManagedMethodProperty, testMethod.ManagedMethodName);
        }

        testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, testMethod.FullClassName);

        if (testMethod.ParameterTypes is not null)
        {
            testCase.SetPropertyValue(AdapterTestProperties.ParameterTypesProperty, testMethod.ParameterTypes);
        }

        IReadOnlyCollection<string?>? hierarchy = testMethod.Hierarchy;
        if (hierarchy is { Count: > 0 })
        {
            testCase.SetHierarchy([.. hierarchy]);
        }

        // Set only if some test category is present
        if (element.TestCategory is { Length: > 0 })
        {
            testCase.SetPropertyValue(AdapterTestProperties.TestCategoryProperty, element.TestCategory);
        }

        // Set priority if present
        if (element.Priority is not null)
        {
            testCase.SetPropertyValue(AdapterTestProperties.PriorityProperty, element.Priority.Value);
        }

        if (element.Traits is { Length: > 0 })
        {
            foreach (TestTrait trait in element.Traits)
            {
                testCase.Traits.Add(new Trait(trait.Name, trait.Value));
            }
        }

        if (element.WorkItemIds is not null)
        {
            testCase.SetPropertyValue(AdapterTestProperties.WorkItemIdsProperty, element.WorkItemIds);
        }

#if !WINDOWS_UWP && !WIN_UI
        // The list of items to deploy before running this test.
        if (element.DeploymentItems is { Length: > 0 })
        {
            testCase.SetPropertyValue(AdapterTestProperties.DeploymentItemsProperty, element.DeploymentItems);
        }
#endif

        // Set the Do not parallelize state if present
        if (element.DoNotParallelize)
        {
            testCase.SetPropertyValue(AdapterTestProperties.DoNotParallelizeProperty, element.DoNotParallelize);
        }

        if (element.UnfoldingStrategy != TestDataSourceUnfoldingStrategy.Auto)
        {
            testCase.SetPropertyValue(AdapterTestProperties.UnfoldingStrategy, (int)element.UnfoldingStrategy);
        }

        // Store resolved data if any
        if (testMethod.DataType != DynamicDataType.None)
        {
            testCase.SetPropertyValue(AdapterTestProperties.TestDynamicDataTypeProperty, (int)testMethod.DataType);
            testCase.SetPropertyValue(AdapterTestProperties.TestDynamicDataProperty, testMethod.SerializedData);
            testCase.SetPropertyValue(AdapterTestProperties.TestCaseIndexProperty, testMethod.TestCaseIndex);
            // VSTest serialization doesn't handle null so instead don't set the property so that it's deserialized as null
            if (testMethod.TestDataSourceIgnoreMessage is not null)
            {
                testCase.SetPropertyValue(AdapterTestProperties.TestDataSourceIgnoreMessageProperty, testMethod.TestDataSourceIgnoreMessage);
            }
        }

        testCase.LineNumber = element.DeclaringLineNumber ?? -1;
        testCase.CodeFilePath = element.DeclaringFilePath;

        testCase.Id = GenerateSerializedDataStrategyTestId(element, testFullName);

        return testCase;
    }

    /// <summary>
    /// Computes the stable test identifier for a <see cref="UnitTestElement"/> without materializing a VSTest
    /// <see cref="TestCase"/>. This is the same identifier surfaced as <see cref="TestCase.Id"/> by
    /// <see cref="ToTestCase"/>, exposed neutrally so the native Microsoft.Testing.Platform integration can build
    /// a <c>TestNode</c> UID without depending on the VSTest object model.
    /// </summary>
    /// <param name="element">The test element to identify.</param>
    /// <returns>The stable, versioned test identifier.</returns>
    internal static Guid GetTestId(this UnitTestElement element)
    {
        if (element.CachedTestNodeUid is { } cachedTestId)
        {
            return cachedTestId;
        }

        TestMethod testMethod = element.TestMethod;
        string testFullName = $"{testMethod.FullClassName}.{testMethod.Name}";
        Guid testId = GenerateSerializedDataStrategyTestId(element, testFullName);
        element.CachedTestNodeUid = testId;
        return testId;
    }

    private static Guid GenerateSerializedDataStrategyTestId(UnitTestElement element, string testFullName)
    {
        TestMethod testMethod = element.TestMethod;

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
        string fileNameOrFilePath = testMethod.AssemblyName;
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
        if (testMethod.ParameterTypes is not null)
        {
            hash.Append(OpenParen);
            hash.Append(Encoding.Unicode.GetBytes(testMethod.ParameterTypes));
            hash.Append(CloseParen);
        }

        if (testMethod.DataType != DynamicDataType.None)
        {
            hash.Append(OpenBracket);
            hash.Append(Encoding.Unicode.GetBytes(testMethod.TestCaseIndex.ToString(CultureInfo.InvariantCulture)));
            hash.Append(CloseBracket);
        }

        byte[] hashBytes = hash.GetCurrentHash();

        return VersionedGuidFromHash(hashBytes, hashVersion);
    }

    internal /* for testing */ static Guid VersionedGuidFromHash(byte[] hashBytes, byte hashVersion)
    {
        int firstByte = 0;
        int versionByte = 6;

        // We set first 4 bits to 0001, which is our version. Increase this when we change the hashing algorithm.
        //
        // Note: The logic below is operating on int32, because bitwise operators are not defined for byte in C#. But casting
        // does not affect endianness, so we can safely assume the byte data are at the end of the int.
        // We also don't care to specify the whole expansion of the int in the bit masks, which effectively ignores all the data in the int
        // before the last byte data, but that is okay, they will always be zero.
        hashBytes[firstByte] = (byte)((hashBytes[firstByte] & 0b0000_1111) | (hashVersion << 4));
        // We set first 4bits 7th byte to 8 to indicate that this is a version 8 UUID. https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-8
        hashBytes[versionByte] = (byte)((hashBytes[versionByte] & 0b0000_1111) | 0b1000_0000);

        // We set the first 2 bits of nineth byte to 0b10. In the guid this is stored as byte, so we don't care about endiannes.
        int variantByte = 8;
        hashBytes[variantByte] = (byte)((hashBytes[variantByte] & 0b0011_1111) | 0b1000_0000);

        Guid guid;

        // On .NET Framework we cannot specify the endianness of the Guid because the new Guid(hashBytes, bigEndian: true); api is not available.
        // Instead we construct the int and short values manually, doing what the constructor would do, to put the bytes in big-endian order.
        guid = new Guid(
            (hashBytes[0] << 24) | (hashBytes[1] << 16) | (hashBytes[2] << 8) | hashBytes[3],
            (short)((hashBytes[4] << 8) | hashBytes[5]),
            (short)((hashBytes[6] << 8) | hashBytes[7]),
            hashBytes[8], hashBytes[9], hashBytes[10], hashBytes[11], hashBytes[12], hashBytes[13], hashBytes[14], hashBytes[15]);

#if DEBUG && NET9_0_OR_GREATER
        // Version property is only available on .NET 9 and later.
        Debug.Assert(guid.Version == 8, $"Expected Guid version to be 8, but it was {guid.Version}");
        // The field represents the 4 bit value, but according to the specification only the first 2 bits are used for UUID v8.
        // So we shift 2 bits to the right to get the actual variant value.
        int variant = guid.Variant >> 2;
        Debug.Assert(variant == 2, $"Expected Guid variant to be 2, but it was {variant}");
#endif
        return guid;
    }
}
