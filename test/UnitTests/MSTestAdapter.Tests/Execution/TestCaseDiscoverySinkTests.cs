
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    using System;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    [TestClass]
    public class TestCaseDiscoverySinkTests
    {
        private TestCaseDiscoverySink testCaseDiscoverySink;

        [TestInitialize]
        public void TestInit()
        {
            this.testCaseDiscoverySink = new TestCaseDiscoverySink();
        }

        [TestMethod]
        public void TestCaseDiscoverySinkConstructorShouldInitializeTests()
        {
            Assert.IsNotNull(this.testCaseDiscoverySink.Tests);
            Assert.AreEqual(0, this.testCaseDiscoverySink.Tests.Count);
        }

        [TestMethod]
        public void SendTestCaseShouldNotAddTestIfTestCaseIsNull()
        {
            this.testCaseDiscoverySink.SendTestCase(null);

            Assert.IsNotNull(this.testCaseDiscoverySink.Tests);
            Assert.AreEqual(0, this.testCaseDiscoverySink.Tests.Count);
        }

        [TestMethod]
        public void SendTestCaseShouldAddTheTestCaseToTests()
        {
            TestCase tc = new TestCase("T", new Uri("executor://TestExecutorUri"), "A");
            this.testCaseDiscoverySink.SendTestCase(tc);

            Assert.IsNotNull(this.testCaseDiscoverySink.Tests);
            Assert.AreEqual(1, this.testCaseDiscoverySink.Tests.Count);
            Assert.AreEqual(tc, this.testCaseDiscoverySink.Tests.ToArray()[0]);
        }
    }
}
