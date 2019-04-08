// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    internal class Constants
    {
        public static readonly TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal const string DllExtension = ".dll";
        internal const string ExeExtension = ".exe";
        internal const string AppxPackageExtension = ".appx";

        private const string DeploymentItemsLabel = "DeploymentItems";
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
