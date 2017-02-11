// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;

    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeEnumeratorTests
    {
        private Mock<ReflectHelper> mockReflectHelper;

        private Mock<TestMethodValidator> mockTestMethodValidator;
        private Mock<TypeValidator> mockTypeValidator;

        private ICollection<string> warnings;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.mockTypeValidator = new Mock<TypeValidator>(MockBehavior.Default, this.mockReflectHelper.Object);
            this.mockTestMethodValidator = new Mock<TestMethodValidator>(MockBehavior.Default, this.mockReflectHelper.Object);
            this.warnings = new List<string>();

            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        #region Enumerate tests

        [TestMethod]
        public void EnumerateShouldReturnNullIfTypeIsNotValid()
        {
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(IDummyInterface), string.Empty);
            Assert.IsNull(typeEnumerator.Enumerate(out this.warnings));
        }

        [TestMethod]
        public void EnumerateShouldReturnEmptyCollectionWhenNoValidTestMethodsExist()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), string.Empty);

            var tests = typeEnumerator.Enumerate(out this.warnings);
            
            Assert.IsNotNull(tests);
            Assert.AreEqual(0, tests.Count);
        }

        #endregion

        #region GetTests tests

        [TestMethod]
        public void GetTestsShouldReturnDeclaredTestMethods()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            var methodCount = typeof(DummyBaseTestClass).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).Count();
            Assert.IsNotNull(tests);
            Assert.AreEqual(methodCount, tests.Count);
        }

        [TestMethod]
        public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            var methodCount = typeof(DummyDerivedTestClass).GetMethods(BindingFlags.Instance | BindingFlags.Public).Count(m => m.DeclaringType.Assembly == typeof(DummyDerivedTestClass).Assembly);
            Assert.IsNotNull(tests);
            Assert.AreEqual(methodCount, tests.Count);
        }

        [TestMethod]
        public void GetTestsShouldNotReturnBaseTestMethodsFromAnotherAssembly()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            // This would return basic object methods like ToString() as well.
            var methodCount =
                typeof(DummyDerivedTestClass).GetMethods(BindingFlags.Instance | BindingFlags.Public).Count();
            Assert.IsNotNull(tests);
            Assert.IsTrue(methodCount > tests.Count);
        }

        #endregion

        #region GetTestFromMethod tests

        [TestMethod]
        public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

            var testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType"), this.warnings);

            Assert.IsNotNull(testElement);
            Assert.AreEqual("MethodWithVoidReturnType", testElement.TestMethod.Name);
            Assert.AreEqual(typeof(DummyTestClass).FullName, testElement.TestMethod.FullClassName);
            Assert.AreEqual("DummyAssemblyName", testElement.TestMethod.AssemblyName);
            Assert.IsFalse(testElement.TestMethod.IsAsync);
        }

        [TestMethod]
        public void GetTestFromMethodShouldInitiateAsyncTypeNameCorrectly()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType");

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            var expectedAsyncTaskName =
                (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute)
                    .StateMachineType.FullName;

            Assert.IsNotNull(testElement);
            Assert.AreEqual(expectedAsyncTaskName, testElement.AsyncTypeName);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetIgnoredPropertyToTrueIfSetOnTestClass()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(true);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(false);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);
            
            Assert.IsNotNull(testElement);
            Assert.IsTrue(testElement.Ignored);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetIgnoredPropertyToTrueIfSetOnTestMethod()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(false);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(true);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsTrue(testElement.Ignored);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetIgnoredPropertyToFalseIfNotSetOnTestClassAndTestMethod()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(false);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(false);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsFalse(testElement.Ignored);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetTestCategory()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testCategories = new string[] { "foo", "bar" };

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetCategories(methodInfo)).Returns(testCategories);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            CollectionAssert.AreEqual(testCategories, testElement.TestCategory);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestProperties()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
            var ownerTrait = new Trait("owner", "mike");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
            this.mockReflectHelper.Setup(rh => rh.GetTestOwnerAsTraits(methodInfo)).Returns(ownerTrait);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            testProperties.Add(ownerTrait);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
            var priorityTrait = new Trait("Priority", "1");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
            this.mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);
            this.mockReflectHelper.Setup(rh => rh.GetTestPriorityAsTraits(1)).Returns(priorityTrait);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            testProperties.Add(priorityTrait);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetPriority()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.AreEqual(1, testElement.Priority);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetCustomAttributes(It.IsAny<MemberInfo>(), typeof(UTF.DeploymentItemAttribute))).Returns(new Attribute[] { });
            this.testablePlatformServiceProvider.MockTestDeployment.Setup(
                td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), this.warnings))
                .Returns((KeyValuePair<string, string>[]) null);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsNull(testElement.DeploymentItems);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDeploymentItems()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var deploymentItems = new[] { new KeyValuePair<string, string>("C:\\temp", string.Empty) };

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetCustomAttributes(typeof(DummyTestClass).GetTypeInfo(), typeof(UTF.DeploymentItemAttribute))).Returns(new Attribute[] { new UTF.DeploymentItemAttribute("C:\\temp") });
            this.mockReflectHelper.Setup(rh => rh.GetCustomAttributes(methodInfo, typeof(UTF.DeploymentItemAttribute))).Returns(new Attribute[] { });
            this.testablePlatformServiceProvider.MockTestDeployment.Setup(
                td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), this.warnings)).Returns(deploymentItems);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsNotNull(testElement.DeploymentItems);
            CollectionAssert.AreEqual(deploymentItems, testElement.DeploymentItems.ToArray());
        }

        #endregion

        #region IsIgnoreAttributeOnTestClass tests

        [TestMethod]
        public void IsIgnoreAttributeOnTestClassReturnsTrueIfIgnoreIsSetOnTestClass()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);

            Assert.IsTrue(typeEnumerator.IsIgnoreAttributeOnTestClass);
        }

        [TestMethod]
        public void IsIgnoreAttributeOnTestClassReturnsFalseIfIgnoreIsNotSetOnTestClass()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);

            Assert.IsFalse(typeEnumerator.IsIgnoreAttributeOnTestClass);
        }

        [TestMethod]
        public void IsIgnoreAttributeOnTestClassShouldBeCached()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(true);

            Assert.IsTrue(typeEnumerator.IsIgnoreAttributeOnTestClass);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), It.IsAny<bool>())).Returns(false);
            Assert.IsTrue(typeEnumerator.IsIgnoreAttributeOnTestClass);
        }

        #endregion

        #region private methods

        private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod)
        {
            this.mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
                .Returns(isValidTestClass);
            this.mockTestMethodValidator.Setup(
                tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
        }

        private TypeEnumerator GetTypeEnumeratorInstance(Type type, string assemblyName)
        {
            return new TypeEnumerator(
                type, 
                assemblyName, 
                this.mockReflectHelper.Object, 
                this.mockTypeValidator.Object, 
                this.mockTestMethodValidator.Object);
        }

        #endregion
    }

    #region Dummy Test Types

    public class DummyBaseTestClass
    {
        public void TestMethod()
        {
        }
    }

    public class DummyDerivedTestClass : DummyBaseTestClass
    {
        public void TestMethod2()
        {
        }
    }

    #endregion 
}
