// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class TestMethodValidatorTests : TestContainer
{
    private readonly TestMethodValidator _testMethodValidator;
    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly List<string> _warnings;

    private readonly Mock<MethodInfo> _mockMethodInfo;
    private readonly Type _type;

    public TestMethodValidatorTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object);
        _warnings = [];

        _mockMethodInfo = new Mock<MethodInfo>();
        _type = typeof(TestMethodValidatorTests);
    }

    public void IsValidTestMethodShouldReturnFalseForMethodsWithoutATestMethodAttributeOrItsDerivedAttributes()
    {
        _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestMethodAttribute>(It.IsAny<MemberInfo>(), false)).Returns(false);
        Verify(!_testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForGenericTestMethodDefinitions()
    {
        SetupTestMethod();
        _mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        _mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        _mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        Verify(!_testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings));
    }

    public void IsValidTestMethodShouldReportWarningsForGenericTestMethodDefinitions()
    {
        SetupTestMethod();

        _mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        _mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        _mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        _testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings);

        Verify(_warnings.Count == 1);
        Verify(_warnings.Contains(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, "DummyTestClass", "DummyTestMethod")));
    }

    public void IsValidTestMethodShouldReturnFalseForNonPublicMethods()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForAbstractMethods()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "AbstractTestMethod",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForStaticMethods()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "StaticTestMethod",
            BindingFlags.Static | BindingFlags.Public);

        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForGenericTestMethods()
    {
        SetupTestMethod();
        Action action = () => new DummyTestClassWithGenericMethods().GenericMethod<int>();

        Verify(!_testMethodValidator.IsValidTestMethod(action.Method, typeof(DummyTestClassWithGenericMethods), _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForAsyncMethodsWithNonTaskReturnType()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void IsValidTestMethodShouldReturnFalseForMethodsWithNonVoidReturnType()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithIntReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(!_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForNonAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    public void IsValidTestMethodShouldReturnTrueForMethodsWithVoidReturnType()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Verify(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    #region Discovery of internals enabled

    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnTrueForInternalMethods()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object, true);

        Verify(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnFalseForPrivateMethods()
    {
        SetupTestMethod();
        MethodInfo methodInfo = typeof(DummyTestClass).GetMethod(
            "PrivateTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object, true);

        Verify(!testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    #endregion

    private void SetupTestMethod() => _mockReflectHelper.Setup(
            rh => rh.IsDerivedAttributeDefined<UTF.TestMethodAttribute>(It.IsAny<MemberInfo>(), false)).Returns(true);
}

#region Dummy types

public class DummyTestClassWithGenericMethods
{
    public void GenericMethod<T>()
    {
    }
}

internal abstract class DummyTestClass
{
    public static void StaticTestMethod()
    {
    }

    public abstract void AbstractTestMethod();

    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Done on purpose")]
    public async void AsyncMethodWithVoidReturnType() => await Task.FromResult(true);

    public async Task AsyncMethodWithTaskReturnType() => await Task.Delay(TimeSpan.Zero);

    public Task MethodWithTaskReturnType() => Task.Delay(TimeSpan.Zero);

    public int MethodWithIntReturnType() => 0;

    public void MethodWithVoidReturnType()
    {
    }

    internal void InternalTestMethod()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void PrivateTestMethod()
#pragma warning restore IDE0051 // Remove unused private members
    {
    }
}

#endregion
