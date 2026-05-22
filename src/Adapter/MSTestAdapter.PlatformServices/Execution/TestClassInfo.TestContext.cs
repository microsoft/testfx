// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestClassInfo
{
    /// <summary>
    /// Test context property name.
    /// </summary>
    private const string TestContextPropertyName = "TestContext";

    /// <summary>
    /// Resolves the test context property.
    /// </summary>
    /// <param name="classType"> The class Type. </param>
    /// <returns> The <see cref="PropertyInfo"/> for TestContext property. Null if not defined. </returns>
    private static PropertyInfo? ResolveTestContext(Type classType)
    {
        try
        {
            PropertyInfo? testContextProperty = PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeProperty(classType, TestContextPropertyName, includeNonPublic: false);
            if (testContextProperty == null)
            {
                // that's okay may be the property was not defined
                return null;
            }

            // check if testContextProperty is of correct type
            if (!string.Equals(testContextProperty.PropertyType.FullName, typeof(TestContext).FullName, StringComparison.Ordinal))
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextTypeMismatchLoadError, classType.FullName);
                throw new TypeInspectionException(errorMessage);
            }

            return testContextProperty;
        }
        catch (AmbiguousMatchException ex)
        {
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextLoadError, classType.FullName, ex.Message);
            throw new TypeInspectionException(errorMessage);
        }
    }
}
