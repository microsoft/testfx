// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    /// <summary>
    /// Resolve the test method. The function will try to
    /// find a function that has the method name with 0 parameters. If the function
    /// cannot be found, or a function is found that returns non-void, the result is
    /// set to error.
    /// </summary>
    /// <returns>
    /// The TestMethodInfo for the given test method. Null if the test method could not be found.
    /// </returns>
    private static TestMethodInfo ResolveTestMethodInfo(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        DebugEx.Assert(testMethod != null, "testMethod is Null");
        DebugEx.Assert(testClassInfo != null, "testClassInfo is Null");

        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);

        return new TestMethodInfo(methodInfo, testClassInfo);
    }

    private static DiscoveryTestMethodInfo ResolveTestMethodInfoForDiscovery(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo methodInfo = GetMethodInfoForTestMethod(testMethod, testClassInfo);
        return new DiscoveryTestMethodInfo(methodInfo, testClassInfo);
    }

    /// <summary>
    /// Resolves a method by using the method name.
    /// </summary>
    /// <param name="testMethod"> The test Method. </param>
    /// <param name="testClassInfo"> The test Class Info. </param>
    /// <returns> The <see cref="MethodInfo"/>. </returns>
    private static MethodInfo GetMethodInfoForTestMethod(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo? testMethodInfo = testMethod.HasManagedMethodAndTypeProperties
            ? GetMethodInfoUsingManagedNameHelper(testMethod, testClassInfo)
            : throw ApplicationStateGuard.Unreachable();

        // if correct method is not found, throw appropriate
        // exception about what is wrong.
        if (testMethodInfo == null)
        {
            string errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_MethodDoesNotExists, testMethod.FullClassName, testMethod.Name);
            throw new TypeInspectionException(errorMessage);
        }

        return testMethodInfo;
    }

    private static MethodInfo? GetMethodInfoUsingManagedNameHelper(TestMethod testMethod, TestClassInfo testClassInfo)
    {
        MethodInfo? testMethodInfo = null;
        try
        {
            // testMethod.MethodInfo can be null if 'TestMethod' instance crossed app domain boundaries.
            // This happens on .NET Framework when app domain is enabled, and the MethodInfo is calculated and set during discovery.
            // Then, execution will cause TestMethod to cross to a different app domain, and MethodInfo will be null.
            // In addition, it also happens when deployment items are used and app domain is disabled.
            // We explicitly set it to null in this case because the original MethodInfo calculated during discovery cannot be used because
            // it points to the type loaded from the assembly in bin instead of from deployment directory.
            testMethodInfo = testMethod.MethodInfo ?? ManagedNameHelper.GetMethod(testClassInfo.Parent.Assembly, testMethod.ManagedTypeName!, testMethod.ManagedMethodName!);
        }
        catch (InvalidManagedNameException)
        {
            // A malformed managed name is treated as "method not resolvable"; the caller
            // (GetMethodInfoForTestMethod) will surface a TypeInspectionException with a
            // user-friendly UTA_MethodDoesNotExists message when testMethodInfo is null.
        }

        return testMethodInfo is null
            || !testMethodInfo.HasCorrectTestMethodSignature(true, testClassInfo.Parent.DiscoversInternals)
            ? null
            : testMethodInfo;
    }
}
