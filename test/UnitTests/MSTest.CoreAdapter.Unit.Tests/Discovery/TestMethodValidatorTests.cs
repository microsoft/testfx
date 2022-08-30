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
    private TestMethodValidator _testMethodValidator;
    private Mock<ReflectHelper> _mockReflectHelper;
    private List<string> _warnings;

    private Mock<MethodInfo> _mockMethodInfo;
    private Type _type;

    [TestInitialize]
    public void TestInit()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object);
        _warnings = new List<string>();

        _mockMethodInfo = new Mock<MethodInfo>();
        _type = typeof(TestMethodValidatorTests);
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForMethodsWithoutATestMethodAttributeOrItsDerivedAttributes()
    {
        _mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined(It.IsAny<MemberInfo>(), typeof(UTF.TestMethodAttribute), false)).Returns(false);
        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForGenericTestMethodDefinitions()
    {
        SetupTestMethod();
        _mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        _mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        _mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReportWarningsForGenericTestMethodDefinitions()
    {
        SetupTestMethod();
        _mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
        _mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
        _mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

        _testMethodValidator.IsValidTestMethod(_mockMethodInfo.Object, _type, _warnings);

        Assert.AreEqual(1, _warnings.Count);
        CollectionAssert.Contains(_warnings, string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, "DummyTestClass", "DummyTestMethod"));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForNonPublicMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForAbstractMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AbstractTestMethod",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForStaticMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "StaticTestMethod",
            BindingFlags.Static | BindingFlags.Public);

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForGenericTestMethods()
    {
        SetupTestMethod();
        Action action = () => new DummyTestClassWithGenericMethods().GenericMethod<int>();

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(action.Method, typeof(DummyTestClassWithGenericMethods), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForAsyncMethodsWithNonTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnFalseForMethodsWithNonVoidReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithIntReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsFalse(_testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "AsyncMethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForNonAsyncMethodsWithTaskReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithTaskReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    [TestMethod]
    public void IsValidTestMethodShouldReturnTrueForMethodsWithVoidReturnType()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "MethodWithVoidReturnType",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.IsTrue(_testMethodValidator.IsValidTestMethod(methodInfo, _type, _warnings));
    }

    #region Discovery of internals enabled

    [TestMethod]
    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnTrueForInternalMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "InternalTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object, true);

        Assert.IsTrue(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    [TestMethod]
    public void WhenDiscoveryOfInternalsIsEnabledIsValidTestMethodShouldReturnFalseForPrivateMethods()
    {
        SetupTestMethod();
        var methodInfo = typeof(DummyTestClass).GetMethod(
            "PrivateTestMethod",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var testMethodValidator = new TestMethodValidator(_mockReflectHelper.Object, true);

        Assert.IsFalse(testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), _warnings));
    }

    #endregion

    private void SetupTestMethod()
    {
        _mockReflectHelper.Setup(
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
