// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UWP.UnitTests
{
    extern alias FrameworkV1;
    
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The universal test source host tests.
    /// </summary>
    [TestClass]
    public class UniversalTestSourceHostTests
    {
        private TestSourceHost testSourceHost;

        /// <summary>
        /// The test initialization.
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            this.testSourceHost = new TestSourceHost(null, null);
        }

        /// <summary>
        /// The create instance for type creates an instance of a given type through default constructor.
        /// </summary>
        [TestMethod]
        public void CreateInstanceForTypeCreatesAnInstanceOfAGivenTypeThroughDefaultConstructor()
        {
            var type = this.testSourceHost.CreateInstanceForType(typeof(DummyType), null) as DummyType;

            Assert.IsNotNull(type);
            Assert.IsTrue(type.IsDefaultConstructorCalled);
        }
    }

    /// <summary>
    /// The dummy type.
    /// </summary>
    public class DummyType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DummyType"/> class.
        /// </summary>
        public DummyType()
        {
            this.IsDefaultConstructorCalled = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether is default constructor called.
        /// </summary>
        public bool IsDefaultConstructorCalled { get; set; }
    }
}
