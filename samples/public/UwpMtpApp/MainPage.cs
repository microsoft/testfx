// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Windows.UI.Xaml.Controls;

namespace UwpMtpApp;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
#if PACKAGED_APP_TESTING
        InitializeComponent();
#else
        var grid = new Grid();
        grid.Children.Add(new TextBlock { Text = "MSTest modern UWP sample" });
        Content = grid;
#endif
    }
}
