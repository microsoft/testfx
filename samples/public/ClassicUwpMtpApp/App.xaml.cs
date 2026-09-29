// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;

namespace ClassicUwpMtpApp
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            Window.Current.Activate();
            try
            {
                Environment.ExitCode = await global::MicrosoftTestingPlatformApplication.RunAsync(args.Arguments);
            }
            finally
            {
                Exit();
            }
        }
    }
}
