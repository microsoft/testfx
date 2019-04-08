// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_1
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    [TestClass]
    public class TestSourceHostTests
    {
        private TestSourceHost testSourceHost;

        [TestInitialize]
        public void TestInit()
        {
            this.testSourceHost = new TestSourceHost(null, null, null);
        }

        [TestMethod]
        public void CreateInstanceForTypeCreatesAnInstanceOfAGivenTypeThroughDefaultConstructor()
        {
            var type = this.testSourceHost.CreateInstanceForType(typeof(DummyType), null) as DummyType;

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

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
