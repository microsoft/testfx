// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.PlatformServices.Interface;
using MSTest.PlatformServices.ObjectModel;

namespace MSTest.PlatformServices.Discovery;

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
    /// <returns> A collection of test elements. </returns>
    internal static ICollection<UnitTestElement>? GetTests(string? assemblyFileName, IRunSettings? runSettings, ITestSourceHandler testSourceHandler, out List<string> warnings)
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

        if (!testSourceHandler.IsAssemblyReferenced(UnitTestFrameworkAssemblyName, fullFilePath))
        {
            return null;
        }

        try
        {
            // Load the assembly in isolation if required.
            AssemblyEnumerationResult result = GetTestsInIsolation(fullFilePath, runSettings);
            warnings.AddRange(result.Warnings);
            return result.TestElements;
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions != null)
            {
                if (ex.LoaderExceptions.Length == 1 && ex.LoaderExceptions[0] is { } singleLoaderException)
                {
                    // This exception might be more clear than the ReflectionTypeLoadException, so we throw it.
                    throw singleLoaderException;
                }

                // If we have multiple loader exceptions, we log them all as errors, and then throw the original exception.
                foreach (Exception? loaderEx in ex.LoaderExceptions)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogError("{0}", loaderEx);
                }
            }

            throw;
        }
        catch (BadImageFormatException)
        {
            if (!IsManagedAssembly(fullFilePath))
            {
                // Ignore BadImageFormatException when loading native dll in managed adapter.
                return null;
            }

            throw;
        }
    }

    private static AssemblyEnumerationResult GetTestsInIsolation(string fullFilePath, IRunSettings? runSettings)
    {
        using MSTest.PlatformServices.Interface.ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(fullFilePath, runSettings, frameworkHandle: null);

        // Create an instance of a type defined in adapter so that adapter gets loaded in the child app domain
        var assemblyEnumerator = (AssemblyEnumerator)isolationHost.CreateInstanceForType(typeof(AssemblyEnumerator), [MSTestSettings.CurrentSettings])!;

        // This might not be supported if an older version of MSTest.PlatformServices
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

    private static bool IsManagedAssembly(string fileName)
    {
        // Copy from https://stackoverflow.com/a/15608028/5108631
        using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);
        if (fileStream.Length < 64)
        {
            return false;
        }

        // PE Header starts @ 0x3C (60). Its a 4 byte header.
        fileStream.Position = 0x3C;
        uint peHeaderPointer = binaryReader.ReadUInt32();
        if (peHeaderPointer == 0)
        {
            peHeaderPointer = 0x80;
        }

        // Ensure there is at least enough room for the following structures:
        //     24 byte PE Signature & Header
        //     28 byte Standard Fields         (24 bytes for PE32+)
        //     68 byte NT Fields               (88 bytes for PE32+)
        // >= 128 byte Data Dictionary Table
        if (peHeaderPointer > fileStream.Length - 256)
        {
            return false;
        }

        // Check the PE signature.  Should equal 'PE\0\0'.
        fileStream.Position = peHeaderPointer;
        uint peHeaderSignature = binaryReader.ReadUInt32();
        if (peHeaderSignature != 0x00004550)
        {
            return false;
        }

        // skip over the PEHeader fields
        fileStream.Position += 20;

        const ushort PE32 = 0x10b;
        const ushort PE32Plus = 0x20b;

        // Read PE magic number from Standard Fields to determine format.
        ushort peFormat = binaryReader.ReadUInt16();
        if (peFormat is not PE32 and not PE32Plus)
        {
            return false;
        }

        // Read the 15th Data Dictionary RVA field which contains the CLI header RVA.
        // When this is non-zero then the file contains CLI data otherwise not.
        ushort dataDictionaryStart = (ushort)(peHeaderPointer + (peFormat == PE32 ? 232 : 248));
        fileStream.Position = dataDictionaryStart;

        uint cliHeaderRva = binaryReader.ReadUInt32();
        return cliHeaderRva != 0;
    }
}
