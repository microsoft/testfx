// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace ClassicUwpMtpApp
{
    [TestClass]
    public sealed class UnitTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void PackageIdentityMatchesManifest()
        {
            Assert.AreEqual("MSTestClassicUwpMtpSample", Package.Current.Id.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName));
        }

        [UITestMethod]
        public void UITestMethodRunsOnTheCoreWindowDispatcher()
        {
            CoreWindow coreWindow = CoreWindow.GetForCurrentThread();
            Assert.IsNotNull(coreWindow);
            Assert.IsTrue(coreWindow.Dispatcher.HasThreadAccess);

            var grid = new Grid();
            Assert.IsNotNull(grid.Dispatcher);
            Assert.IsTrue(grid.Dispatcher.HasThreadAccess);
        }

        [TestMethod]
        [DataRow("en-US", "The parameter should not be null or empty.", DisplayName = "AdapterFailureMessages_AreLocalized_English")]
        [DataRow("fr-FR", "The parameter should not be null or empty.", DisplayName = "AdapterFailureMessages_UseNeutralFallbackForFrench")]
        public void AdapterFailureMessages_RespectSatellitePolicy(string cultureName, string expectedMessage)
        {
            CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

                // Native UWP MTP suppresses adapter satellite copying; preserve its neutral fallback.
                // This public API reads the adapter's real platform-services resources before any file I/O.
                ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => TestContext.AddResultFile(string.Empty));
                Assert.AreEqual("fileName", exception.ParamName);
                Assert.StartsWith(expectedMessage, exception.Message);
            }
            finally
            {
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }

        [TestMethod]
        [DataRow("en-US", "MSTest classic UWP sample", DisplayName = "ApplicationResources_AreLocalized_English")]
        [DataRow("fr-FR", "Exemple UWP classique MSTest", DisplayName = "ApplicationResources_AreLocalized_French")]
        public void ApplicationResources_AreLocalized(string language, string expectedDescription)
        {
            var context = new ResourceContext();
            context.QualifierValues["Language"] = language;

            string description = ResourceManager.Current.MainResourceMap
                .GetValue("Resources/AppDescription", context).ValueAsString;

            Assert.AreEqual(expectedDescription, description);
        }
    }
}
