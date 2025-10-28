// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal static class Constants
{
#if NETFRAMEWORK
    /// <summary>
    ///  Constants for detecting .net framework.
    /// </summary>
    public const string TargetFrameworkAttributeFullName = "System.Runtime.Versioning.TargetFrameworkAttribute";

    public const string DotNetFrameWorkStringPrefix = ".NETFramework,Version=";

    public const string TargetFrameworkName = "TargetFrameworkName";

    public const string PublicAssemblies = "PublicAssemblies";

    public const string PrivateAssemblies = "PrivateAssemblies";
#endif

    public static readonly TestProperty DeploymentItemsProperty = TestProperty.Register("MSTestDiscoverer.DeploymentItems", DeploymentItemsLabel, typeof(KeyValuePair<string, string>[]), TestPropertyAttributes.Hidden, typeof(TestCase));

    internal const string DllExtension = ".dll";
    internal const string ExeExtension = ".exe";
    internal const string AppxPackageExtension = ".appx";

#if NETFRAMEWORK
    internal const string PhoneAppxPackageExtension = ".appx";

    // These are tied to a specific VS version. Can be changed to have a list of supported version instead.
    internal const string VisualStudioRootRegKey32ForDev14 = @"SOFTWARE\Microsoft\VisualStudio\" + VisualStudioVersion;
    internal const string VisualStudioRootRegKey64ForDev14 = @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio\" + VisualStudioVersion;

    internal const string VisualStudioVersion = "14.0";
#endif

    private const string DeploymentItemsLabel = "DeploymentItems";

    internal const string PublicTypeObsoleteMessage = "We will remove or hide this type starting with v4. If you are using this type, reach out to our team on https://github.com/microsoft/testfx.";
}
