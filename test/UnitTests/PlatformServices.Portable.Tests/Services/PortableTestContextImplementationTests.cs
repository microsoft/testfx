
namespace MSTestAdapter.PlatformServices.Portable.Tests.Services
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UnitTestOutcome = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;

    [TestClass]
    public class TestContextImplementationTests
    {
        private Mock<ITestMethod> testMethod;

        private IDictionary<string, object> properties;

        private TestContextImplementation testContextImplementation;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<ITestMethod>();
            this.properties = new Dictionary<string, object>();
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeProperties()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeDefaultProperties()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);

            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("FullyQualifiedTestClassName", "A.C.M"));
            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("TestName", "M"));
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnDefaultOutcome()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual(UnitTestOutcome.Failed, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnOutcomeSet()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

            Assert.AreEqual(UnitTestOutcome.InProgress, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("A.C.M", this.testContextImplementation.FullyQualifiedTestClassName);
        }

        [TestMethod]
        public void TestNameShouldReturnTestMethodsName()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("M", this.testContextImplementation.TestName);
        }

        [TestMethod]
        public void PropertiesShouldReturnPropertiesPassedToTestContext()
        {
            var property1 = new KeyValuePair<string, object>("IntProperty", 1);
            var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

            this.properties.Add(property1);
            this.properties.Add(property2);

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            CollectionAssert.Contains(this.testContextImplementation.Properties.ToList(), property1);
            CollectionAssert.Contains(this.testContextImplementation.Properties.ToList(), property2);
        }

        [TestMethod]
        public void ContextShouldReturnTestContextObject()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Context);
            Assert.AreEqual("M", this.testContextImplementation.Context.TestName);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;
            
            Assert.IsTrue(this.testContextImplementation.TryGetPropertyValue("TestName", out propValue));
            Assert.AreEqual("M", propValue);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;

            Assert.IsFalse(this.testContextImplementation.TryGetPropertyValue("Random", out propValue));
            Assert.IsNull(propValue);
        }

        [TestMethod]
        public void AddPropertyShouldAddPropertiesToThePropertyBag()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("SomeNewProperty", "SomeValue"));
        }
    }
}
