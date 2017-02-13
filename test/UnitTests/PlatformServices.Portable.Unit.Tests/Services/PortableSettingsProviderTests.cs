// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Portable.Tests.Services
{
    extern alias FrameworkV1;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Moq;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class MSTestSettingsProviderTests
    {
        [TestMethod]
        public void GetPropertiesShouldReturnEmptyDictionary()
        {
            MSTestSettingsProvider settings = new MSTestSettingsProvider();

            Assert.AreEqual(0, settings.GetProperties(It.IsAny<string>()).Count);
        }
    }
}
