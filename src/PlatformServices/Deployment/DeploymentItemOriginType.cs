// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment
{
    using System;

    /// <summary>
    /// Specifies type of deployment item origin, where the item comes from.
    /// </summary>
    [Serializable]
    internal enum DeploymentItemOriginType
    {
        /// <summary>
        /// A per test deployment item.
        /// </summary>
        PerTestDeployment,

        /// <summary>
        /// A test storage.
        /// </summary>
        TestStorage,

        /// <summary>
        /// A dependency item.
        /// </summary>
        Dependency,

        /// <summary>
        /// A satellite assembly.
        /// </summary>
        Satellite,
    }
}
