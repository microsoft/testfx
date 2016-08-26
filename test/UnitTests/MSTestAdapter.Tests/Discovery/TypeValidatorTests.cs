// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="TypeValidatorTests.cs">
//   
// </copyright>
// 
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
    
    using Moq;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeValidatorTests
    {
        #region private variables

        private TypeValidator typeValidator;
        private Mock<ReflectHelper> mockReflectHelper;
        private List<string> warnings;

        #endregion

        #region TestInit

        [TestInitialize]
        public void TestInit()
        {
            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.typeValidator = new TypeValidator(mockReflectHelper.Object);
            this.warnings = new List<string>();
        }

        #endregion

        #region Type is class, TestClassAttribute or attribute derived from TestClassAttribute

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForNonClassTypes()
        {
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(IDummyInterface), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForClassesNotHavingTestClassAttributeOrDerivedAttributeTypes()
        {
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(TypeValidatorTests), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnTrueForClassesMarkedByAnAttributeDerivedFromTestClass()
        {
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(false);
            this.mockReflectHelper.Setup(
                rh => rh.HasAttributeDerivedFrom(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
            Assert.IsTrue(this.typeValidator.IsValidTestClass(typeof(TypeValidatorTests), this.warnings));
        }

        #endregion

        #region Public/Nested public test classes

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForNonPublicTestClasses()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(InternalTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReportWarningForNonPublicTestClasses()
        {
            this.SetupTestClass();
            this.typeValidator.IsValidTestClass(typeof(InternalTestClass), this.warnings);
            Assert.AreEqual(1, this.warnings.Count);
            CollectionAssert.Contains(this.warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(InternalTestClass).FullName));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForNestedNonPublicTestClasses()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReportWarningsForNestedNonPublicTestClasses()
        {
            this.SetupTestClass();
            this.typeValidator.IsValidTestClass(typeof(OuterClass.NestedInternalClass), this.warnings);
            Assert.AreEqual(1, this.warnings.Count);
            CollectionAssert.Contains(this.warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(OuterClass.NestedInternalClass).FullName));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnTrueForPublicTestClasses()
        {
            this.SetupTestClass();
            Assert.IsTrue(this.typeValidator.IsValidTestClass(typeof(PublicTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnTrueForNestedPublicTestClasses()
        {
            this.SetupTestClass();
            Assert.IsTrue(this.typeValidator.IsValidTestClass(typeof(OuterClass.NestedPublicClass), this.warnings));
        }

        #endregion

        #region Generic types

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForNonAbstractGenericTypes()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(GenericClass<>), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReportWarningsForNonAbstractGenericTypes()
        {
            this.SetupTestClass();
            this.typeValidator.IsValidTestClass(typeof(GenericClass<>), this.warnings);
            Assert.AreEqual(1, this.warnings.Count);
            CollectionAssert.Contains(this.warnings, string.Format(Resource.UTA_ErrorNonPublicTestClass, typeof(GenericClass<>).FullName));
        }

        #endregion

        #region TestContext signature

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForTestClassesWithInvalidTestContextSignature()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(ClassWithTestContextGetterOnly), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldReportWarningsForTestClassesWithInvalidTestContextSignature()
        {
            this.SetupTestClass();
            this.typeValidator.IsValidTestClass(typeof (ClassWithTestContextGetterOnly), this.warnings);
            Assert.AreEqual(1, this.warnings.Count);
            CollectionAssert.Contains(this.warnings, string.Format(Resource.UTA_ErrorInValidTestContextSignature, typeof(ClassWithTestContextGetterOnly).FullName));
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnTrueForTestClassesWithValidTestContextSignature()
        {
            this.SetupTestClass();
            Assert.IsTrue(this.typeValidator.IsValidTestClass(typeof(ClassWithTestContext), this.warnings));
        }

        #endregion

        #region Abstract Test classes

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForAbstractTestClasses()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(AbstractTestClass), this.warnings));
        }

        [TestMethod]
        public void IsValidTestClassShouldNotReportWarningsForAbstractTestClasses()
        {
            this.SetupTestClass();
            this.typeValidator.IsValidTestClass(typeof(AbstractTestClass), this.warnings);
            Assert.AreEqual(0, this.warnings.Count);
        }

        [TestMethod]
        public void IsValidTestClassShouldReturnFalseForGenericAbstractTestClasses()
        {
            this.SetupTestClass();
            Assert.IsFalse(this.typeValidator.IsValidTestClass(typeof(AbstractGenericClass<>), this.warnings));
            Assert.AreEqual(0, this.warnings.Count);
        }

        #endregion

        #region HasCorrectTestContext tests

        [TestMethod]
        public void HasCorrectTestContextSignatureShouldReturnTrueForClassesWithNoTestContextProperty()
        {
            Assert.IsTrue(this.typeValidator.HasCorrectTestContextSignature(typeof(PublicTestClass)));
        }

        [TestMethod]
        public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithNoSetters()
        {
            Assert.IsFalse(this.typeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextGetterOnly)));
        }
        
        [TestMethod]
        public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithPrivateSetter()
        {
            Assert.IsFalse(this.typeValidator.HasCorrectTestContextSignature(typeof(ClassWithTestContextPrivateSetter)));
        }

        [TestMethod]
        public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithStaticSetter()
        {
            Assert.IsFalse(this.typeValidator.HasCorrectTestContextSignature(typeof(ClassWithStaticTestContext)));
        }

        [TestMethod]
        public void HasCorrectTestContextSignatureShouldReturnFalseForTestContextsWithAbstractSetter()
        {
            Assert.IsFalse(this.typeValidator.HasCorrectTestContextSignature(typeof(ClassWithAbstractTestContext)));
        }

        #endregion

        #region private methods

        private void SetupTestClass()
        {
            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), false)).Returns(true);
        }

        #endregion
    }

    #region Dummy Types

    public interface IDummyInterface
    {
    }

    public abstract class AbstractGenericClass<T>
    {
    }

    public class GenericClass<T>
    {
    }

    public class ClassWithTestContextGetterOnly
    {
        public UTF.TestContext TestContext { get; }
    }

    public class ClassWithTestContextPrivateSetter
    {
        public UTF.TestContext TestContext { get; private set; }
    }

    public class ClassWithStaticTestContext
    {
        public static UTF.TestContext TestContext { get; set; }
    }

    public abstract class ClassWithAbstractTestContext
    {
        public abstract UTF.TestContext TestContext { get; set; }
    }

    public class ClassWithTestContext
    {
        public UTF.TestContext TestContext { get; set; }
    }

    public class PublicTestClass
    {
    }

    public abstract class AbstractTestClass
    {
    }

    public class OuterClass
    {
        public class NestedPublicClass
        {
        }

        internal class NestedInternalClass
        {
        }
    }

    internal class InternalTestClass
    {
    }

    #endregion
}
