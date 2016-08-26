// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="PlatformServiceProviderTests.cs">
//   
// </copyright>
// 
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using Moq;

    [TestClass]
    public class PlatformServiceProviderTests 
    {
        [TestInitialize]
        public void TestInit()
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
            Assert.IsTrue(testContext.Context.Properties.Contains(properties.ToArray()[0]));
        }
    }
}
