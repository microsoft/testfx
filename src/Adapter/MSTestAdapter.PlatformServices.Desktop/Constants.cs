// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    internal class Constants
    {
        internal const string DllExtension = ".dll";
        internal const string ExeExtension = ".exe";
        internal const string PhoneAppxPackageExtension = ".appx";

        // These are tied to a specific VS version. Can be changed to have a list of supported version instead.
        internal const string VisualStudioRootRegKey32ForDev14 = @"SOFTWARE\Microsoft\VisualStudio\" + VisualStudioVersion;
        internal const string VisualStudioRootRegKey64ForDev14 = @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio\" + VisualStudioVersion;

        internal const string VisualStudioVersion = "14.0";

        private const string DeploymentItemsLabel = "DeploymentItems";

        public static readonly TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer2.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

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
    }
}
