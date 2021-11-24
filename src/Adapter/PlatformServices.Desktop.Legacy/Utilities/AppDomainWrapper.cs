﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Security.Policy;

    /// <summary>
    /// Abstraction over the AppDomain APIs.
    /// </summary>
    internal class AppDomainWrapper : IAppDomain
    {
        public AppDomain CreateDomain(string friendlyName, Evidence securityInfo, AppDomainSetup info)
        {
            return AppDomain.CreateDomain(friendlyName, securityInfo, info);
        }

        public void Unload(AppDomain appDomain)
        {
            AppDomain.Unload(appDomain);
        }
    }
}
