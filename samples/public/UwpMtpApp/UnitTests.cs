// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
#if !PACKAGED_APP_TESTING
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;
#if PACKAGED_APP_TESTING
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
#endif

using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace UwpMtpApp;

[TestClass]
public sealed class UnitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TestHostRuns()
    {
        Assert.AreEqual("UwpMtpApp", typeof(UnitTests).Assembly.GetName().Name);
    }

    [TestMethod]
    public void PackageIdentityMatchesManifest()
    {
        Package package = GetCurrentPackageOrInconclusive();
        Assert.AreEqual("MSTestUwpMtpSample", package.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(package.Id.FamilyName));
    }

    [UITestMethod]
    public void UITestMethodRunsOnTheCoreWindowDispatcher()
    {
        _ = GetCurrentPackageOrInconclusive();

        CoreWindow coreWindow = CoreWindow.GetForCurrentThread();
        Assert.IsNotNull(coreWindow);
        Assert.IsTrue(coreWindow.Dispatcher.HasThreadAccess);

        var grid = new Grid();
        Assert.IsNotNull(grid.Dispatcher);
        Assert.IsTrue(grid.Dispatcher.HasThreadAccess);
    }

    [TestMethod]
    [DataRow("en-US", "The parameter should not be null or empty.", DisplayName = "AdapterFailureMessages_AreLocalized_English")]
#if PACKAGED_APP_TESTING
    [DataRow("fr-FR", "The parameter should not be null or empty.", DisplayName = "AdapterFailureMessages_UseNeutralFallbackForFrench")]
#else
    [DataRow("fr-FR", "Le paramètre ne doit pas être null ou vide.", DisplayName = "AdapterFailureMessages_AreLocalized_French")]
#endif
    public void AdapterFailureMessages_RespectSatellitePolicy(string cultureName, string expectedMessage)
    {
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            // Native UWP MTP suppresses adapter satellites; the unpackaged path copies translations.
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
    [DataRow("en-US", "MSTest modern UWP sample", DisplayName = "ApplicationResources_AreLocalized_English")]
    [DataRow("fr-FR", "Exemple UWP moderne MSTest", DisplayName = "ApplicationResources_AreLocalized_French")]
    public void ApplicationResources_AreLocalized(string language, string expectedDescription)
    {
        _ = GetCurrentPackageOrInconclusive();

        var context = new ResourceContext();
        context.QualifierValues["Language"] = language;

        string description = ResourceManager.Current.MainResourceMap
            .GetValue("Resources/AppDescription", context).ValueAsString;

        Assert.AreEqual(expectedDescription, description);
    }

    private static Package GetCurrentPackageOrInconclusive()
    {
        try
        {
            return Package.Current;
        }
        catch (InvalidOperationException)
        {
            Assert.Inconclusive("This assertion requires the packaged AppContainer execution path.");
            throw;
        }
    }
}

#if !PACKAGED_APP_TESTING
public sealed class UITestMethodAttribute : TestMethodAttribute
{
    public UITestMethodAttribute(
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        CoreDispatcher dispatcher;
        try
        {
            dispatcher = CoreApplication.MainView.CoreWindow.Dispatcher;
        }
        catch (InvalidOperationException)
        {
            return [await testMethod.InvokeAsync(null).ConfigureAwait(false)];
        }

        var completion = new TaskCompletionSource<TestResult>();
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        await dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
            async () =>
            {
                try
                {
                    completion.SetResult(await testMethod.InvokeAsync(null).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        return [await completion.Task.ConfigureAwait(false)];
    }
}
#endif
