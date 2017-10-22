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
    using global::MSTestAdapter.TestUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
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
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: false, isMethodFromSameAssembly: true);
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
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyBaseTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            Assert.IsNotNull(tests);
            Assert.IsTrue(
                tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
                "DummyBaseTestClass declares BaseTestMethod directly so it should always be discovered.");
        }

        [TestMethod]
        public void GetTestsShouldReturnBaseTestMethodsInSameAssembly()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            Assert.IsNotNull(tests);
            Assert.IsTrue(
                tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
                "DummyDerivedTestClass inherits DummyBaseTestClass from same assembly. BestTestMethod from DummyBaseTestClass should be discovered.");
        }

        [TestMethod]
        public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByDefault()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);

            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            Assert.IsNotNull(tests);
            Assert.IsTrue(
                tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
                "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered by default.");
        }

        [TestMethod]
        public void GetTestsShouldReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
        {
            string runSettingxml =
            @"<RunSettings>   
                <MSTestV2>
                  <CaptureTraceOutput>true</CaptureTraceOutput>
                  <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>true</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

            var mockRunContext = new Mock<IRunContext>();
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            MSTestSettings.PopulateSettings(mockRunContext.Object);
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            Assert.IsNotNull(tests);
            Assert.IsTrue(
                tests.Any(t => t.TestMethod.Name == "BaseTestMethod"),
                "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = truem.");
        }

        [TestMethod]
        public void GetTestsShouldNotReturnBaseTestMethodsFromAnotherAssemblyByConfiguration()
        {
            string runSettingxml =
                @"<RunSettings>   
                <MSTestV2>
                  <CaptureTraceOutput>true</CaptureTraceOutput>
                  <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
                  <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
              </MSTestV2>
            </RunSettings>";

            var mockRunContext = new Mock<IRunContext>();
            var mockRunSettings = new Mock<IRunSettings>();
            mockRunContext.Setup(dc => dc.RunSettings).Returns(mockRunSettings.Object);
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            MSTestSettings.PopulateSettings(mockRunContext.Object);
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: false);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyDerivedTestClass), Assembly.GetExecutingAssembly().FullName);

            var tests = typeEnumerator.Enumerate(out this.warnings);

            Assert.IsNotNull(tests);
            Assert.IsTrue(
                tests.All(t => t.TestMethod.Name != "BaseTestMethod"),
                "DummyDerivedFromRemoteTestClass inherits DummyRemoteBaseTestClass from different assembly. BestTestMethod from DummyRemoteBaseTestClass should not be discovered when RunSettings MSTestV2 specifies EnableBaseClassTestMethodsFromOtherAssemblies = false.");
        }

        #endregion

        #region GetTestFromMethod tests

        [TestMethod]
        public void GetTestFromMethodShouldInitiateTestMethodWithCorrectParameters()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");

            var testElement = typeEnumerator.GetTestFromMethod(typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType"), true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.AreEqual("MethodWithVoidReturnType", testElement.TestMethod.Name);
            Assert.AreEqual(typeof(DummyTestClass).FullName, testElement.TestMethod.FullClassName);
            Assert.AreEqual("DummyAssemblyName", testElement.TestMethod.AssemblyName);
            Assert.IsFalse(testElement.TestMethod.IsAsync);
        }

        [TestMethod]
        public void GetTestFromMethodShouldInitializeAsyncTypeNameCorrectly()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("AsyncMethodWithTaskReturnType");

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            var expectedAsyncTaskName =
                (methodInfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) as AsyncStateMachineAttribute)
                    .StateMachineType.FullName;

            Assert.IsNotNull(testElement);
            Assert.AreEqual(expectedAsyncTaskName, testElement.AsyncTypeName);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetIgnoredPropertyToFalseIfNotSetOnTestClassAndTestMethod()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClass), typeof(UTF.IgnoreAttribute), false)).Returns(false);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.IgnoreAttribute), false)).Returns(false);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsFalse(testElement.Ignored);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetTestCategory()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testCategories = new string[] { "foo", "bar" };

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetCategories(methodInfo)).Returns(testCategories);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            CollectionAssert.AreEqual(testCategories, testElement.TestCategory);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDoNotParallelize()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetCustomAttributes(It.IsAny<MemberInfo>(), typeof(UTF.DoNotParallelizeAttribute))).Returns(new[] { new UTF.DoNotParallelizeAttribute() });

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsTrue(testElement.DoNotParallelize);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestProperties()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestOwnerPropertyIfPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
            var ownerTrait = new Trait("owner", "mike");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
            this.mockReflectHelper.Setup(rh => rh.GetTestOwnerAsTraits(methodInfo)).Returns(ownerTrait);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            testProperties.Add(ownerTrait);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldFillTraitsWithTestPriorityPropertyIfPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var testProperties = new List<Trait> { new Trait("foo", "bar"), new Trait("fooprime", "barprime") };
            var priorityTrait = new Trait("Priority", "1");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetTestPropertiesAsTraits(methodInfo)).Returns(testProperties);
            this.mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);
            this.mockReflectHelper.Setup(rh => rh.GetTestPriorityAsTraits(1)).Returns(priorityTrait);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            testProperties.Add(priorityTrait);
            CollectionAssert.AreEqual(testProperties, testElement.Traits);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetPriority()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.mockReflectHelper.Setup(rh => rh.GetPriority(methodInfo)).Returns(1);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.AreEqual(1, testElement.Priority);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDeploymentItemsToNullIfNotPresent()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            this.testablePlatformServiceProvider.MockTestDeployment.Setup(
                td => td.GetDeploymentItems(It.IsAny<MethodInfo>(), It.IsAny<Type>(), this.warnings))
                .Returns((KeyValuePair<string, string>[])null);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsNull(testElement.DeploymentItems);
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDeploymentItems()
        {
            this.SetupTestClassAndTestMethods(isValidTestClass: true, isValidTestMethod: true, isMethodFromSameAssembly: true);
            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");
            var deploymentItems = new[] { new KeyValuePair<string, string>("C:\\temp", string.Empty) };

            // Setup mocks
            this.testablePlatformServiceProvider.MockTestDeployment.Setup(
                td => td.GetDeploymentItems(methodInfo, typeof(DummyTestClass), this.warnings)).Returns(deploymentItems);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, true, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.IsNotNull(testElement.DeploymentItems);
            CollectionAssert.AreEqual(deploymentItems, testElement.DeploymentItems.ToArray());
        }

        [TestMethod]
        public void GetTestFromMethodShouldSetDeclaringAssemblyName()
        {
            const bool isMethodFromSameAssemly = false;

            TypeEnumerator typeEnumerator = this.GetTypeEnumeratorInstance(typeof(DummyTestClass), "DummyAssemblyName");
            var methodInfo = typeof(DummyTestClass).GetMethod("MethodWithVoidReturnType");

            // Setup mocks
            string otherAssemblyName = "ADifferentAssembly";
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetAssemblyPath(It.IsAny<Assembly>()))
                .Returns(otherAssemblyName);

            var testElement = typeEnumerator.GetTestFromMethod(methodInfo, isMethodFromSameAssemly, this.warnings);

            Assert.IsNotNull(testElement);
            Assert.AreEqual(otherAssemblyName, testElement.TestMethod.DeclaringAssemblyName);
        }

        #endregion

        #region private methods

        private void SetupTestClassAndTestMethods(bool isValidTestClass, bool isValidTestMethod, bool isMethodFromSameAssembly)
        {
            this.mockTypeValidator.Setup(tv => tv.IsValidTestClass(It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
                .Returns(isValidTestClass);
            this.mockTestMethodValidator.Setup(
                tmv => tmv.IsValidTestMethod(It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>())).Returns(isValidTestMethod);
            this.mockReflectHelper.Setup(
                rh => rh.IsMethodDeclaredInSameAssemblyAsType(It.IsAny<MethodInfo>(), It.IsAny<Type>())).Returns(isMethodFromSameAssembly);
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
        public void BaseTestMethod()
        {
        }
    }

    public class DummyDerivedTestClass : DummyBaseTestClass
    {
        public void DerivedTestMethod()
        {
        }
    }

    #endregion
}
