// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;

    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class PlatformServiceProviderTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        [TestMethod]
        public void ProviderServiceInstanceShouldReturnAnObjectOfItselfByDefault()
        {
            Assert.IsInstanceOfType(PlatformServiceProvider.Instance, typeof(PlatformServiceProvider));
        }

        [TestMethod]
        public void ProviderServiceInstanceShouldReturnTheInstanceSet()
        {
            // If this test fails most other tests would too since this
            // defines our mocking for the Service provider.
            PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
            Assert.IsInstanceOfType(PlatformServiceProvider.Instance, typeof(TestablePlatformServiceProvider));
        }

        [TestMethod]
        public void TestSourceShouldReturnANonNullInstance()
        {
            Assert.IsNotNull(PlatformServiceProvider.Instance);
        }

        [TestMethod]
        public void TestSourceShouldReturnAValidTestSource()
        {
            Assert.IsInstanceOfType(PlatformServiceProvider.Instance.TestSource, typeof(TestSource));
        }

        [TestMethod]
        public void TestSourceShouldBeCached()
        {
            var testSourceInstance = PlatformServiceProvider.Instance.TestSource;

            Assert.IsNotNull(testSourceInstance);
            Assert.AreEqual(testSourceInstance, PlatformServiceProvider.Instance.TestSource);
        }

        [TestMethod]
        public void ReflectionOperationsShouldReturnAValidInstance()
        {
            Assert.IsInstanceOfType(PlatformServiceProvider.Instance.ReflectionOperations, typeof(ReflectionOperations));
        }

        [TestMethod]
        public void ReflectionOperationsShouldBeCached()
        {
            var reflectionOperationsInstance = PlatformServiceProvider.Instance.ReflectionOperations;

            Assert.IsNotNull(reflectionOperationsInstance);
            Assert.AreEqual(reflectionOperationsInstance, PlatformServiceProvider.Instance.ReflectionOperations);
        }

        [TestMethod]
        public void GetTestContextShouldReturnAValidTestContext()
        {
            // Arrange.
            var testMethod = new Mock<Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod>();
            var writer = new StringWriter();
            var properties = new Dictionary<string, object> { { "prop", "value" } };
            testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
            testMethod.Setup(tm => tm.Name).Returns("M");

            // Act.
            var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod.Object, writer, properties);

            // Assert.
            Assert.AreEqual("A.C.M", testContext.Context.FullyQualifiedTestClassName);
            Assert.AreEqual("M", testContext.Context.TestName);
            Assert.IsTrue(testContext.Context.Properties.Contains(properties.ToArray()[0].Key));
            Assert.IsTrue(((IDictionary<string, object>)testContext.Context.Properties).Contains(properties.ToArray()[0]));
        }
    }
}
