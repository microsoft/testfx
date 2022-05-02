// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    internal class Constants
    {
        public static readonly TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

        internal const string DllExtension = ".dll";
        internal const string ExeExtension = ".exe";
        internal const string PackageExtension = ".appx";

        private const string DeploymentItemsLabel = "DeploymentItems";

        #if NETFRAMEWORK
        /// <summary>
        ///  Constants for detecting .net framework.
        /// </summary>
        public const string TargetFrameworkAttributeFullName = "System.Runtime.Versioning.TargetFrameworkAttribute";
        public const string DotNetFrameWorkStringPrefix = ".NETFramework,Version=";
        public const string TargetFrameworkName = "TargetFrameworkName";

        /// <summary>
        /// Constants for MSTest in Portable Mode
        /// </summary>
        public const string PortableVsTestLocation = "PortableVsTestLocation";
        public const string PublicAssemblies = "PublicAssemblies";
        public const string PrivateAssemblies = "PrivateAssemblies";

        // These are tied to a specific VS version. Can be changed to have a list of supported version instead.
        internal const string VisualStudioRootRegKey32ForDev14 = @"SOFTWARE\Microsoft\VisualStudio\" + VisualStudioVersion;
        internal const string VisualStudioRootRegKey64ForDev14 = @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio\" + VisualStudioVersion;
        internal const string VisualStudioVersion = "14.0";
        #endif
    }
}
