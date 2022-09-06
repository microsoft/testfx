// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#if WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

/// <summary>
/// This platform service is responsible for any data or operations to validate
/// the test sources provided to the adapter.
/// </summary>
public class TestSource : ITestSource
{
#if WINDOWS_UWP || WIN_UI
    private const string SystemAssembliesPrefix = "system.";

    private static readonly IEnumerable<string> ExecutableExtensions = new HashSet<string>()
    {
         Constants.ExeExtension,

         // Required only for store 8.1. In future if that support is needed, uncomment this.
         // Constants.DllExtension
    };

    private static readonly HashSet<string> SystemAssemblies = new(new string[]
    {
        "MICROSOFT.CSHARP.DLL",
        "MICROSOFT.VISUALBASIC.DLL",
        "CLRCOMPRESSION.DLL",
    });

    // Well known platform assemblies.
    private static readonly HashSet<string> PlatformAssemblies = new(new string[]
    {
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTFRAMEWORK.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTFRAMEWORK.EXTENSIONS.CORE.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.CORE.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.COMMON.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTEXECUTOR.CORE.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.EXTENSIONS.MSAPPCONTAINERADAPTER.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.EXTENSIONS.MSPHONEADAPTER.DLL",
        "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.OBJECTMODEL.DLL",
        "VSTEST_EXECUTIONENGINE_PLATFORMBRIDGE.DLL",
        "VSTEST_EXECUTIONENGINE_PLATFORMBRIDGE.WINMD",
        "VSTEST.EXECUTIONENGINE.WINDOWSPHONE.DLL",
    });
#endif

    /// <summary>
    /// Gets the set of valid extensions for sources targeting this platform.
    /// </summary>
    public IEnumerable<string> ValidSourceExtensions
    {
        get
        {
            return new List<string>
            {
                Constants.DllExtension,
#if NETFRAMEWORK
                Constants.PhoneAppxPackageExtension,
#elif WINDOWS_UWP || WIN_UI
                Constants.AppxPackageExtension,
#endif
                Constants.ExeExtension
            };
        }
    }

    /// <summary>
    /// Verifies if the assembly provided is referenced by the source.
    /// </summary>
    /// <param name="assemblyName"> The assembly name. </param>
    /// <param name="source"> The source. </param>
    /// <returns> True if the assembly is referenced. </returns>
    public bool IsAssemblyReferenced(AssemblyName assemblyName, string source)
    {
#if NETFRAMEWORK
        // This loads the dll in a different app domain. We can optimize this to load in the current domain since this code could be run in a new app domain anyway.
        bool? utfReference = AssemblyHelper.DoesReferencesAssembly(source, assemblyName);

        // If no reference to UTF don't run discovery. Take conservative approach. If not able to find proceed with discovery.
        if (utfReference.HasValue && utfReference.Value == false)
        {
            return false;
        }

        return true;
#else
        // .NET CORE:
        // There is no way currently in dotnet core to determine referenced assemblies for a source.
        // UWP/WinUI:
        // This code will get hit when Discovery happens during Run Tests.
        // Since Discovery during Discover Tests would have validated the presence of Unit Test Framework as reference,
        // no need to do validation again.
        return true;
#endif
    }

    /// <summary>
    /// Gets the set of sources (dll's/exe's) that contain tests. If a source is a package (appx), return the file (dll/exe) that contains tests from it.
    /// </summary>
    /// <param name="sources"> Sources given to the adapter.  </param>
    /// <returns> Sources that contains tests. <see cref="IEnumerable{T}"/>. </returns>
    public IEnumerable<string> GetTestSources(IEnumerable<string> sources)
    {
#if WINDOWS_UWP
        if (ContainsAppxSource(sources))
        {
            List<string> newSources = new();

            var fileSearchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFilesAsync().AsTask();
            fileSearchTask.Wait();
            foreach (var filePath in fileSearchTask.Result)
            {
                var fileName = filePath.Name;
                var isExtSupported =
                    ExecutableExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                if (isExtSupported && !fileName.StartsWith(SystemAssembliesPrefix, StringComparison.OrdinalIgnoreCase)
                        && !PlatformAssemblies.Contains(fileName.ToUpperInvariant())
                        && !SystemAssemblies.Contains(fileName.ToUpperInvariant()))
                {
                    // Required only for store 8.1
                    // If a source package(appx) has both dll and exe files that contains tests, then add any one of them and not both.
                    // if((fileName.EndsWith(Constants.ExeExtension) && !newSources.Contains(Path.GetFileNameWithoutExtension(fileName) + Constants.DllExtension))
                    //    || (fileName.EndsWith(Constants.DllExtension) && !newSources.Contains(Path.GetFileNameWithoutExtension(fileName) + Constants.ExeExtension)))
                    newSources.Add(fileName);
                }
            }

            return newSources;
        }
#elif WIN_UI
        string appxSource;
        if ((appxSource = FindAppxSource(sources)) != null)
        {
            var appxSourceDirectory = Path.GetDirectoryName(appxSource);

            List<string> newSources = new();

            var files = Directory.GetFiles(AppModel.GetCurrentPackagePath());
            foreach (var filePath in files)
            {
                var isExtSupported = ExecutableExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                if (isExtSupported && !filePath.StartsWith(SystemAssembliesPrefix, StringComparison.OrdinalIgnoreCase)
                        && !PlatformAssemblies.Contains(filePath.ToUpperInvariant())
                        && !SystemAssemblies.Contains(filePath.ToUpperInvariant()))
                {
                    // WinUI Desktop uses .NET 6, which builds both a .dll and an .exe.
                    // The manifest will provide the .exe, but the tests are inside the .dll, so we replace the name here.
                    newSources.Add(Path.Combine(appxSourceDirectory, Path.ChangeExtension(filePath, Constants.DllExtension)));
                }
            }

            return newSources;
        }
#endif

        return sources;
    }

#if WINDOWS_UWP
    /// <summary>
    /// Checks if given list of sources contains any ".appx" source.
    /// </summary>
    /// <param name="sources">The list of sources.</param>
    /// <returns>True if there is an appx source.</returns>
    private bool ContainsAppxSource(IEnumerable<string> sources)
    {
        foreach (string source in sources)
        {
            if (string.Compare(Path.GetExtension(source), Constants.AppxPackageExtension, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }
#endif

#if WIN_UI
        /// <summary>
    /// Checks if given list of sources contains any ".appx" source.
    /// </summary>
    /// <param name="sources">The list of sources.</param>
    /// <returns>True if there is an appx source.</returns>
    private string FindAppxSource(IEnumerable<string> sources)
    {
        foreach (string source in sources)
        {
            if (string.Compare(Path.GetExtension(source), Constants.AppxPackageExtension, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return source;
            }
        }

        return null;
    }
#endif
}
