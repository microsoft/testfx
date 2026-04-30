// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Gets the test timeout for the test method.
    /// </summary>
    /// <returns> The timeout value if defined in milliseconds. 0 if not defined. </returns>
    private TimeoutInfo GetTestTimeout()
    {
        DebugEx.Assert(MethodInfo != null, "TestMethod should be non-null");
        TimeoutAttribute? timeoutAttribute = ReflectHelper.Instance.GetFirstAttributeOrDefault<TimeoutAttribute>(MethodInfo);
        if (timeoutAttribute is null)
        {
            return TimeoutInfo.FromTestTimeoutSettings();
        }

        if (!timeoutAttribute.HasCorrectTimeout)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, MethodInfo.DeclaringType!.FullName, MethodInfo.Name);
            throw new TypeInspectionException(message);
        }

        return TimeoutInfo.FromTimeoutAttribute(timeoutAttribute);
    }

    /// <summary>
    /// Provides the Test Method Extension Attribute of the TestClass.
    /// </summary>
    /// <returns>Test Method Attribute.</returns>
    private TestMethodAttribute GetTestMethodAttribute()
    {
        // Get the derived TestMethod attribute from reflection.
        // It should be non-null as it was already validated by IsValidTestMethod.
        TestMethodAttribute testMethodAttribute = ReflectHelper.Instance.GetSingleAttributeOrDefault<TestMethodAttribute>(MethodInfo)!;

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        return Parent.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;
    }

    /// <summary>
    /// Gets the number of retries this test method should make in case of failure.
    /// </summary>
    /// <returns>
    /// The number of retries, which is always greater than or equal to 1.
    /// If RetryAttribute is not present, returns 1.
    /// </returns>
    private RetryBaseAttribute? GetRetryAttribute()
    {
        IEnumerable<RetryBaseAttribute> attributes = ReflectHelper.Instance.GetAttributes<RetryBaseAttribute>(MethodInfo);
        using IEnumerator<RetryBaseAttribute> enumerator = attributes.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        RetryBaseAttribute attribute = enumerator.Current;

        if (enumerator.MoveNext())
        {
            ThrowMultipleAttributesException(nameof(RetryBaseAttribute));
        }

        return attribute;
    }

    [DoesNotReturn]
    private void ThrowMultipleAttributesException(string attributeName)
    {
        // Note: even if the given attribute has AllowMultiple = false, we can
        // still reach here if a derived attribute authored by the user re-defines AttributeUsage
        string errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_MultipleAttributesOnTestMethod,
            Parent.ClassType.FullName,
            MethodInfo.Name,
            attributeName);
        throw new TypeInspectionException(errorMessage);
    }
}
