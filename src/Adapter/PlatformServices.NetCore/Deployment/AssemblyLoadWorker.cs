// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Utility function for Assembly related info
    /// The caller is supposed to create AppDomain and create instance of given class in there.
    /// </summary>
    internal class AssemblyLoadWorker : MarshalByRefObject
    {
        private AssemblyUtility assemblyUtility;

        public AssemblyLoadWorker()
            : this(new AssemblyUtility())
        {
        }

        internal AssemblyLoadWorker(AssemblyUtility assemblyUtility)
        {
            this.assemblyUtility = assemblyUtility;
        }

        /// <summary>
        /// initialize the lifetime service.
        /// </summary>
        /// <returns> The <see cref="object"/>. </returns>
        public override object InitializeLifetimeService()
        {
            // Infinite.
            return null;
        }
    }
}
