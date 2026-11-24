// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Security.Policy;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Abstraction over the AppDomain APIs.
/// </summary>
internal sealed class AppDomainWrapper : IAppDomain
{
    public AppDomain CreateDomain(string friendlyName, Evidence securityInfo, AppDomainSetup info)
        => AppDomain.CreateDomain(friendlyName, securityInfo, info);

    public void Unload(AppDomain appDomain)
        => AppDomain.Unload(appDomain);
}

#endif
