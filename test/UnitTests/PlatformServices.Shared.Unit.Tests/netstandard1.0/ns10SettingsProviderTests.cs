// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_0
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Moq;

    [TestClass]
    public class SettingsProviderTests
    {
        [TestMethod]
        public void GetPropertiesShouldReturnEmptyDictionary()
        {
            MSTestSettingsProvider settings = new MSTestSettingsProvider();

            Assert.AreEqual(0, settings.GetProperties(It.IsAny<string>()).Count);
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
