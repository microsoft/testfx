// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace UwpMtpApp;

[TestClass]
public sealed class UnitTests
{
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
