// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.ApplicationModel;

namespace WinUIMtpAppContainerApp;

[TestClass]
public sealed class UnitTests
{
    [TestMethod]
    public void PackageIdentityAndSandboxMatchManifest()
    {
        Package package = Package.Current;

        Assert.AreEqual("MSTestWinUIAppContainerSample", package.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(package.Id.FamilyName));
        Assert.AreEqual($"{package.Id.FamilyName}!App", AppInfo.Current.AppUserModelId);
        Assert.IsTrue(IsCurrentProcessAppContainer());
    }

    [UITestMethod]
    public void UITestMethodRunsOnTheWinUIDispatcher()
    {
        var grid = new Grid();

        Assert.IsNotNull(grid.DispatcherQueue);
        Assert.IsTrue(grid.DispatcherQueue.HasThreadAccess);
    }

    private static bool IsCurrentProcessAppContainer()
    {
        using Process process = Process.GetCurrentProcess();
        if (!OpenProcessToken(process.Handle, 0x0008, out SafeAccessTokenHandle token))
        {
            throw new InvalidOperationException($"OpenProcessToken failed: {Marshal.GetLastWin32Error()}.");
        }

        using (token)
        {
            int isAppContainer = 0;
            int returnLength = 0;
            if (!GetTokenInformation(token.DangerousGetHandle(), 29, ref isAppContainer, sizeof(int), ref returnLength))
            {
                throw new InvalidOperationException($"GetTokenInformation(TokenIsAppContainer) failed: {Marshal.GetLastWin32Error()}.");
            }

            return isAppContainer != 0;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        nint tokenHandle,
        int tokenInformationClass,
        ref int tokenInformation,
        int tokenInformationLength,
        ref int returnLength);
}
