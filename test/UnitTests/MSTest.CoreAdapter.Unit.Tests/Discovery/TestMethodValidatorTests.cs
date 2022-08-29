// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Moq;
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestMethodValidatorTests
{
    private TestMethodValidator testMethodValidator;
    private Mock<ReflectHelper> mockReflectHelper;
    private List<string> warnings;

    private Mock<MethodInfo> mockMethodInfo;
    private Type type;

    [TestInitialize]
    public void TestInit()
    {
        mockReflectHelper = new Mock<ReflectHelper>();
        testMethodValidator = new TestMethodValidator(mockReflectHelper.Object);
        warnings = new List<string>();

        mockMethodInfo = new Mock<MethodInfo>();
        type = typeof(TestMethodValidatorTests);
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForMethodsWithoutATestMethodAttributeOrItsDerivedAttributes()
    {
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(It.IsAny<MemberInfo>(), typeof(UTF.TestMethodAttribute), false)).Returns(false);
        Assert.IsFalse(testMethodValidator.IsValidTestMethod(mockMethodInfo.Object, type, warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForGenericTestMethodDefinitions()
    {
        SetupTestMethod();
        mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(mockMethodInfo.Object, type, warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReportWarningsForGenericTestMethodDefinitions()
    {
        SetupTestMethod();
        mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        testMethodValidator.IsValidTestMethod(mockMethodInfo.Object, type, warnings);

        Assert.AreEqual(1, warnings.Count);
        CollectionAssert.Contains(warnings, string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, "DummyTestClass", "DummyTestMethod"));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForNonPublicMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForAbstractMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AbstractTestMethod",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForStaticMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "StaticTestMethod",
            BindingFlags.Static | BindingFlags.Public);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForGenericTestMethods()
    {
        SetupTestMethod();
        Action action = () => new DummyTestClassWithGenericMethods().GenericMethod<int>();

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(action.Method, typeof(DummyTestClassWithGenericMethods), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForAsyncMethodsWithNonTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForMethodsWithNonVoidReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithIntReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(testMethodValidator.IsValidTestMethod(methodInfo, type, warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForNonAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(testMethodValidator.IsValidTestMethod(methodInfo, type, warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForMethodsWithVoidReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(testMethodValidator.IsValidTestMethod(methodInfo, type, warnings));
    }

    #region Discovery of internals enabled

    [TestMethod]
    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnTrueForInternalMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(mockReflectHelper.Object, true);

        Assert.IsTrue(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    [TestMethod]
    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnFalseForPrivateMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "PrivateTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(mockReflectHelper.Object, true);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), warnings));
    }

    #endregion

    private void SetupTestMethod()
    {
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(It.IsAny<MemberInfo>(), typeof(UTF.TestMethodAttribute), false)).Returns(true);
    }
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

    public async void AsyncMethodWithVoidReturnType()
    {
        await Task.FromResult(true);
    }

    public async Task AsyncMethodWithTaskReturnType()
    {
        await Task.Delay(TimeSpan.Zero);
    }

    public Task MethodWithTaskReturnType()
    {
        return Task.Delay(TimeSpan.Zero);
    }

    public int MethodWithIntReturnType()
    {
        return 0;
    }

    public void MethodWithVoidReturnType()
    {
    }

    internal void InternalTestMethod()
    {
    }

    private void PrivateTestMethod()
    {
    }
}

#endregion
