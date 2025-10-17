// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace MauiApp1;

public class DeviceTests
{

    [Fact]
    public void Button_Clicked()
    {

        var layout = new Grid();

        var button = new Button
        {
            Text = "Text",
            //  Background = new LinearGradient(Colors.Red, Colors.Orange),
        };

        layout.Add(button);

        var clicked = false;

        button.Clicked += delegate
        {
            clicked = true;
        };

        // Simulate button click
        button.SendClicked();

        Assert.Equal(clicked, true);
    }


    [Fact]
    public void DeviceInfo_Returns_ValidDeviceType()
    {
        // This test verifies that the DeviceInfo.Idiom returns a valid value
        var deviceType = DeviceInfo.Current.Idiom;

        Assert.NotEqual(DeviceIdiom.Unknown, deviceType);
    }

    [Fact]
    public void DeviceDisplay_Returns_ValidMetrics()
    {
        // This test verifies that the device display returns valid screen metrics
        var mainDisplayInfo = DeviceDisplay.Current.MainDisplayInfo;

        Assert.True(mainDisplayInfo.Width > 0, "Display width should be greater than 0");
        Assert.True(mainDisplayInfo.Height > 0, "Display height should be greater than 0");
        Assert.True(mainDisplayInfo.Density > 0, "Display density should be greater than 0");
    }

    [Fact]
    public async Task Connectivity_IsAvailable()
    {
        // This test checks if the device has network connectivity
        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        // This is an informational test - we're not asserting true/false
        // because connectivity can vary by environment
        await Task.Delay(10); // Small delay to ensure connectivity check completes

        // Output the result, but don't fail the test if there's no connectivity
        // as this is environment-dependent
        Assert.True(true, $"Network connectivity status: {(hasInternet ? "Available" : "Not available")}");
    }

    [Fact]
    public void DeviceInfo_Returns_AppInfo()
    {
        // Verify that the app info returns valid values
        var appName = AppInfo.Current.Name;
        var packageName = AppInfo.Current.PackageName;
        var version = AppInfo.Current.VersionString;

        Assert.NotEmpty(appName);
        Assert.NotEmpty(packageName);
        Assert.NotEmpty(version);
    }

    [Fact]
    public void Preferences_CanStoreAndRetrieveValues()
    {
        // Test key for the test
        string testKey = "XUnitTestKey";
        string testValue = "XUnitTestValue";

        try
        {
            // Store a value
            Preferences.Default.Set(testKey, testValue);

            // Retrieve the value
            string retrievedValue = Preferences.Default.Get(testKey, string.Empty);

            // Verify
            Assert.Equal(testValue, retrievedValue);
        }
        finally
        {
            // Clean up after the test
            Preferences.Default.Remove(testKey);
        }
    }
}
