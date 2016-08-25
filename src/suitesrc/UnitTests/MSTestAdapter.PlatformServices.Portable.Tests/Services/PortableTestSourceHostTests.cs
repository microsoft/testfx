// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Portable.Tests
{
    extern alias FrameworkV1;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class PortableTestSourceHostTests
    {
        private TestSourceHost testSourceHost;

        [TestInitialize]
        public void TestInit()
        {
            this.testSourceHost = new TestSourceHost();
        }

        [TestMethod]
        public void CreateInstanceForTypeCreatesAnInstanceOfAGivenTypeThroughDefaultConstructor()
        {
            var type = this.testSourceHost.CreateInstanceForType(typeof(DummyType), null, null, null) as DummyType;

            Assert.IsNotNull(type);
            Assert.IsTrue(type.IsDefaultConstructorCalled);
        }
    }

    public class DummyType
    {
        public DummyType()
        {
            this.IsDefaultConstructorCalled = true;
        }

        public bool IsDefaultConstructorCalled { get; set; }
    }
}
