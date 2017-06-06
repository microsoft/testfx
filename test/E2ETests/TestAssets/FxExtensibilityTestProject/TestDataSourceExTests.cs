using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FxExtensibilityTestProject
{
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestTools.UnitTesting.Interfaces;

    [TestClass]
    public class TestDataSourceExTests
    {
        [TestMethod]
        [CustomTestDataSource]
        public void CustomTestDataSourceTestMethod1(int a, int b, int c)
        {
            Assert.AreEqual(1, a % 3);
            Assert.AreEqual(2, b % 3);
            Assert.AreEqual(0, c % 3);
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CustomTestDataSource : TestDataSource
    {
        public override IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            return new[] { new object[] { 1, 2, 3 }, new object[] { 4, 5, 6 } };
        }
    }
}
