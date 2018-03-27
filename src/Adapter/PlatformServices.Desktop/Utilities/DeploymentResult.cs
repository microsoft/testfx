// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

    internal class DeploymentResult
    {
        public DeploymentResult(bool success, TestRunDirectories directories)
        {
            this.Success = success;
            this.RunDirectories = directories;
        }

        /// <summary>
        /// Gets a value indicating whether the deployment was successful
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Gets a value for the RunDirectories created for the deployment
        /// </summary>
        public TestRunDirectories RunDirectories { get; private set; }
    }
}
