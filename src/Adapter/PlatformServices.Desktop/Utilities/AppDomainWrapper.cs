// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Security.Policy;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction over the AppDomain APIs.
    /// </summary>
    internal class AppDomainWrapper : IAppDomain
    {
        private const int QueryTimeout = 1000; // in ms

        public AppDomain CreateDomain(string friendlyName, Evidence securityInfo, AppDomainSetup info)
        {
            return AppDomain.CreateDomain(friendlyName, securityInfo, info);
        }

        public void Unload(AppDomain appDomain)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;

            var task = Task.Run(() => AppDomain.Unload(appDomain), token);

            // AppDomain.Unload() could take indeterminate amount of time where test code isn't properly cleaned up.
            // Adapter should Cancel the Unload() operation after a specific timeout.
            if (!task.Wait(QueryTimeout))
            {
                tokenSource.Cancel();
            }
        }
    }
}
