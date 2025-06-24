// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Enumerates through an assembly to get a set of test methods.
/// </summary>
internal sealed class AssemblyEnumeratorWrapper
{
    /// <summary>
    /// Assembly name for UTF.
    /// </summary>
    private static readonly AssemblyName UnitTestFrameworkAssemblyName = typeof(TestMethodAttribute).Assembly.GetName();

    /// <summary>
    /// Gets test elements from an assembly.
    /// </summary>
    /// <param name="assemblyFileName"> The assembly file name. </param>
    /// <param name="runSettings"> The run Settings. </param>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> A collection of test elements. </returns>
    internal static ICollection<UnitTestElement>? GetTests(string? assemblyFileName, IRunSettings? runSettings, out List<string> warnings)
    {
        warnings = [];

        if (StringEx.IsNullOrEmpty(assemblyFileName))
        {
            return null;
        }

        string fullFilePath = PlatformServiceProvider.Instance.FileOperations.GetFullFilePath(assemblyFileName);

        if (!PlatformServiceProvider.Instance.FileOperations.DoesFileExist(fullFilePath))
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, fullFilePath);
            throw new FileNotFoundException(message);
        }

        if (!PlatformServiceProvider.Instance.TestSource.IsAssemblyReferenced(UnitTestFrameworkAssemblyName, fullFilePath))
        {
            return null;
        }

        // Load the assembly in isolation if required.
        AssemblyEnumerationResult result = GetTestsInIsolation(fullFilePath, runSettings);
        warnings.AddRange(result.Warnings);
        return result.TestElements;
    }

    private static AssemblyEnumerationResult GetTestsInIsolation(string fullFilePath, IRunSettings? runSettings)
    {
        using MSTestAdapter.PlatformServices.Interface.ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(fullFilePath, runSettings, frameworkHandle: null);

        // Create an instance of a type defined in adapter so that adapter gets loaded in the child app domain
        var assemblyEnumerator = (AssemblyEnumerator)isolationHost.CreateInstanceForType(typeof(AssemblyEnumerator), [MSTestSettings.CurrentSettings])!;

        // This might not be supported if an older version of Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
        // assembly is already loaded into the App Domain.
        string? xml = null;
        try
        {
            xml = runSettings?.SettingsXml;
        }
        catch
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.OlderTFMVersionFound);
        }

        // This method runs inside of appdomain, when appdomains are available and enabled.
        // Be careful how you pass data from the method. We were previously passing in a collection
        // of strings normally (by reference), and we were mutating that collection in the appdomain.
        // But this does not mutate the collection outside of appdomain, so we lost all warnings that happened inside.
        return assemblyEnumerator.EnumerateAssembly(fullFilePath);
    }
}
