// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal static class EngineConstants
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

    internal const string DllExtension = ".dll";
    internal const string ExeExtension = ".exe";
    internal const string AppxPackageExtension = ".appx";

#if NETFRAMEWORK
    internal const string PhoneAppxPackageExtension = ".appx";
#endif

    /// <summary>
    /// The 3rd level entry (class) name in the hierarchy array.
    /// </summary>
    internal const string AssemblyFixturesHierarchyClassName = "[Assembly]";

    /// <summary>
    /// Assembly initialize.
    /// </summary>
    internal const string AssemblyInitializeFixtureTrait = "AssemblyInitialize";

    /// <summary>
    /// Assembly cleanup.
    /// </summary>
    internal const string AssemblyCleanupFixtureTrait = "AssemblyCleanup";

    /// <summary>
    /// Class initialize.
    /// </summary>
    internal const string ClassInitializeFixtureTrait = "ClassInitialize";

    /// <summary>
    /// Class cleanup.
    /// </summary>
    internal const string ClassCleanupFixtureTrait = "ClassCleanup";

    /// <summary>
    /// Uri of the MSTest executor.
    /// </summary>
    internal const string ExecutorUriString = "executor://MSTestAdapter/v4";

    /// <summary>
    /// The executor uri for this adapter.
    /// </summary>
    internal static readonly Uri ExecutorUri = new(ExecutorUriString);
}
