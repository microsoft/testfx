// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class TestMethodValidatorTests : TestContainer
{
    private readonly TestMethodValidator _testMethodValidator;
    private readonly ReflectHelper _reflectHelper;
    private readonly List<string> _warnings;

    private readonly Mock<MethodInfo> _mockMethodInfo;
    private readonly Type _type;

    public TestMethodValidatorTests()
    {
        _reflectHelper = new ReflectHelper();
        _testMethodValidator = new TestMethodValidator(_reflectHelper);
        _warnings = [];

        // This is only used for rendering warnings.
        _type = typeof(TestMethodValidatorTests);
    }

    public void IsValidTestMethodShouldReturnFalseForMethodsWithoutATestMethodAttributeOrItsDerivedAttributes()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.MethodWithoutTestAttribute));
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForGenericTestMethodDefinitions()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.GenericTestMethod));
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReportWarningsForGenericTestMethodDefinitions()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.GenericTestMethod));
        _testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings);

        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, typeof(InvalidTestMethods).FullName, nameof(InvalidTestMethods.GenericTestMethod))));
    }

    public void IsValidTestMethodShouldReturnFalseForNonPublicMethods()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.InternalTestMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForAbstractMethods()
    {
        MethodInfo methodInfo = typeof(InvalidAbstractTestMethods).GetMethod(nameof(InvalidAbstractTestMethods.AbstractTestMethod));
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForStaticMethods()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.StaticTestMethod), BindingFlags.Public | BindingFlags.Static);
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForAsyncMethodsWithNonTaskReturnType()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.AsyncTestMethodWithVoidReturnType));
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForMethodsWithNonVoidReturnType()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.TestMethodWithIntReturnType));
        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForAsyncMethodsWithTaskReturnType()
    {
        MethodInfo methodInfo = typeof(ValidTestMethods).GetMethod(nameof(ValidTestMethods.AsyncTestMethodWithTaskReturnType));
        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForNonAsyncMethodsWithTaskReturnType()
    {
        MethodInfo methodInfo = typeof(ValidTestMethods).GetMethod(nameof(ValidTestMethods.SyncTestMethodWithTaskReturnType));
        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForMethodsWithVoidReturnType()
    {
        MethodInfo methodInfo = typeof(ValidTestMethods).GetMethod(nameof(ValidTestMethods.TestMethodWithVoidReturnType));
        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    #region Discovery of internals enabled

    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnTrueForInternalMethods()
    {
        // By default this method won't be detected, but will
        // be detected if you enable discover internals.
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(nameof(InvalidTestMethods.InternalTestMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        var testMethodValidator = new TestMethodValidator(_reflectHelper, discoverInternals: true);

        Verify(testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnFalseForPrivateMethods()
    {
        MethodInfo methodInfo = typeof(InvalidTestMethods).GetMethod(
            InvalidTestMethods.PrivateTestMethodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(_reflectHelper, true);

        Verify(!testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    #endregion
}
