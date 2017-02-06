
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;

    using System;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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
