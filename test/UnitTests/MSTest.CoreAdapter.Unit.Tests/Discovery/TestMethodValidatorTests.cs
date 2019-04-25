// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery
{
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
            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.testMethodValidator = new TestMethodValidator(this.mockReflectHelper.Object);
            this.warnings = new List<string>();

            this.mockMethodInfo = new Mock<MethodInfo>();
            this.type = typeof(TestMethodValidatorTests);
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForMethodsWithoutATestMethodAttributeOrItsDerivedAttributes()
        {
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(It.IsAny<MemberInfo>(), typeof(UTF.TestMethodAttribute), false)).Returns(false);
            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(this.mockMethodInfo.Object, this.type, this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForGenericTestMethodDefinitions()
        {
            this.SetupTestMethod();
            this.mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
            this.mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
            this.mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(this.mockMethodInfo.Object, this.type, this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReportWarningsForGenericTestMethodDefinitions()
        {
            this.SetupTestMethod();
            this.mockMethodInfo.Setup(mi => mi.IsGenericMethodDefinition).Returns(true);
            this.mockMethodInfo.Setup(mi => mi.DeclaringType.FullName).Returns("DummyTestClass");
            this.mockMethodInfo.Setup(mi => mi.Name).Returns("DummyTestMethod");

            this.testMethodValidator.IsValidTestMethod(this.mockMethodInfo.Object, this.type, this.warnings);

            Assert.AreEqual(1, this.warnings.Count);
            CollectionAssert.Contains(this.warnings, string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, "DummyTestClass", "DummyTestMethod"));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForNonPublicMethods()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "InternalTestMethod",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForAbstractMethods()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "AbstractTestMethod",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForStaticMethods()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "StaticTestMethod",
                BindingFlags.Static | BindingFlags.Public);

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForGenericTestMethods()
        {
            this.SetupTestMethod();
            Action action = () => new DummyTestClassWithGenericMethods().GenericMethod<int>();

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(action.Method, typeof(DummyTestClassWithGenericMethods), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForAsyncMethodsWithNonTaskReturnType()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "AsyncMethodWithVoidReturnType",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnFalseForMethodsWithNonVoidReturnType()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "MethodWithIntReturnType",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsFalse(this.testMethodValidator.IsValidTestMethod(methodInfo, typeof(DummyTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnTrueForAsyncMethodsWithTaskReturnType()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "AsyncMethodWithTaskReturnType",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsTrue(this.testMethodValidator.IsValidTestMethod(methodInfo, this.type, this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnTrueForNonAsyncMethodsWithTaskReturnType()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "MethodWithTaskReturnType",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsTrue(this.testMethodValidator.IsValidTestMethod(methodInfo, this.type, this.warnings));
        }

        [TestMethod]
        public void IsValidTestMethodShouldReturnTrueForMethodsWithVoidReturnType()
        {
            this.SetupTestMethod();
            var methodInfo = typeof(DummyTestClass).GetMethod(
                "MethodWithVoidReturnType",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsTrue(this.testMethodValidator.IsValidTestMethod(methodInfo, this.type, this.warnings));
        }

        private void SetupTestMethod()
        {
            this.mockReflectHelper.Setup(
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
    }

    #endregion
}
